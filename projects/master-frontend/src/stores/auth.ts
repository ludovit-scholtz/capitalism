import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import type { MasterAuthPayload, MasterPlayerProfile, SubscriptionInfo } from '@/lib/masterApi'
import {
  claimStartupPack,
  fetchMe,
  fetchMySubscription,
  loginAccount,
  probeGameAdminAccess,
  prolongSubscription,
  registerAccount,
} from '@/lib/masterApi'

const EXPIRES_KEY = 'master_auth_expires'
const AUTH_PROVIDER_KEY = 'master_auth_provider'
const OIDC_STATE_KEY = 'master_biatec_oidc_state'
const OIDC_LOGOUT_STATE_KEY = 'master_biatec_oidc_logout_state'
const AUTH_PROVIDER_LOCAL = 'local'
const AUTH_PROVIDER_BIATEC = 'biatec_oidc'
const COOKIE_SESSION_SENTINEL = 'cookie-session'
const BIATEC_OIDC_AUTHORIZE_URL =
  import.meta.env.VITE_BIATEC_OIDC_AUTHORIZE_URL || 'https://google.biatec.io/authorize'
const BIATEC_OIDC_END_SESSION_URL = import.meta.env.VITE_BIATEC_OIDC_END_SESSION_URL || ''
const BIATEC_OIDC_CLIENT_ID = import.meta.env.VITE_BIATEC_OIDC_CLIENT_ID || 'capitalism-master'
const BIATEC_OIDC_REDIRECT_URI = import.meta.env.VITE_BIATEC_OIDC_REDIRECT_URI
const BIATEC_OIDC_SCOPE = import.meta.env.VITE_BIATEC_OIDC_SCOPE || 'openid'
const BIATEC_OIDC_AUDIENCE = import.meta.env.VITE_BIATEC_OIDC_AUDIENCE || BIATEC_OIDC_CLIENT_ID
const BIATEC_OIDC_ALLOWED_ISSUERS = (
  import.meta.env.VITE_BIATEC_OIDC_ALLOWED_ISSUERS ||
  'https://google.biatec.io,https://google.biatec.io'
)
  .split(',')
  .map((entry) => entry.trim())
  .filter((entry) => entry.length > 0)
const TOKEN_RENEW_BEFORE_MS = 60 * 1000
const API_BASE_URL = (import.meta.env.VITE_GRAPHQL_URL || 'http://localhost:44364/graphql').replace(
  /\/graphql\/?$/,
  '',
)

interface OidcStateRecord {
  state: string
  nonce: string
  redirectPath: string
}

interface BiatecCallbackSession {
  token: string
  expiresAtUtc: string
  redirectPath: string
}

interface BiatecSignInOptions {
  silentPrompt?: boolean
  prompt?: string
}

interface LogoutOptions {
  federated?: boolean
}

function normalizeRedirectPath(redirectPath: string | null | undefined) {
  if (!redirectPath || !redirectPath.startsWith('/')) {
    return '/'
  }

  return redirectPath
}

function generateOidcRandom(length = 32) {
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'
  const bytes = new Uint8Array(length)
  crypto.getRandomValues(bytes)
  let result = ''
  for (const value of bytes) {
    result += chars[value % chars.length]
  }
  return result
}

function createLogoutState() {
  const state = generateOidcRandom(32)
  sessionStorage.setItem(OIDC_LOGOUT_STATE_KEY, state)
  return state
}

function parseJwtPayload(token: string): Record<string, unknown> {
  const [, payload] = token.split('.')
  if (!payload) {
    throw new Error('Invalid token payload.')
  }

  const normalized = payload.replace(/-/g, '+').replace(/_/g, '/')
  const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=')
  const decoded = atob(padded)
  return JSON.parse(decoded) as Record<string, unknown>
}

function buildFallbackAdminEmails(): Set<string> {
  const configured = (import.meta.env.VITE_MASTER_ADMIN_EMAILS as string | undefined)
    ?.split(',')
    .map((item) => item.trim().toLowerCase())
    .filter((item) => item.length > 0)

  const defaults = ['admin@events.local']
  return new Set([...(configured ?? []), ...defaults])
}

const fallbackAdminEmails = buildFallbackAdminEmails()

export const useAuthStore = defineStore('masterAuth', () => {
  const player = ref<MasterPlayerProfile | null>(null)
  const subscription = ref<SubscriptionInfo | null>(null)
  const token = ref<string | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const isGameAdmin = ref(false)
  const gameAdminChecked = ref(false)
  let renewalTimer: ReturnType<typeof setTimeout> | null = null

  const isAuthenticated = computed(() => !!token.value)

  function clearRenewalTimer() {
    if (renewalTimer) {
      clearTimeout(renewalTimer)
      renewalTimer = null
    }
  }

  function getConfiguredRedirectUri() {
    if (BIATEC_OIDC_REDIRECT_URI) {
      return BIATEC_OIDC_REDIRECT_URI
    }

    if (typeof window !== 'undefined') {
      return `${window.location.origin}/auth/callback`
    }

    return 'http://localhost:5174/auth/callback'
  }

  function getStoredAuthProvider() {
    return localStorage.getItem(AUTH_PROVIDER_KEY) || AUTH_PROVIDER_LOCAL
  }

  function setStoredAuthProvider(provider: string) {
    localStorage.setItem(AUTH_PROVIDER_KEY, provider)
  }

  function clearStoredOidcState() {
    sessionStorage.removeItem(OIDC_STATE_KEY)
    localStorage.removeItem(OIDC_STATE_KEY)
  }

  function resolveBiatecEndSessionEndpoint() {
    if (BIATEC_OIDC_END_SESSION_URL) {
      return BIATEC_OIDC_END_SESSION_URL
    }

    try {
      const authorizeUrl = new URL(BIATEC_OIDC_AUTHORIZE_URL)
      return `${authorizeUrl.origin}/connect/endsession`
    } catch {
      return ''
    }
  }

  function getPostLogoutRedirectUri() {
    if (typeof window === 'undefined') {
      return 'http://localhost:5174/login'
    }

    return `${window.location.origin}/login`
  }

  function buildBiatecEndSessionUrl(idTokenHint: string | null, postLogoutRedirectUri = getPostLogoutRedirectUri()) {
    if (typeof window === 'undefined') {
      return null
    }

    try {
      const endpoint = resolveBiatecEndSessionEndpoint()
      if (!endpoint) {
        return null
      }

      const logoutUrl = new URL(endpoint)
      const state = createLogoutState()
      logoutUrl.searchParams.set('post_logout_redirect_uri', postLogoutRedirectUri)
      logoutUrl.searchParams.set('client_id', BIATEC_OIDC_CLIENT_ID)
      logoutUrl.searchParams.set('state', state)

      if (idTokenHint) {
        logoutUrl.searchParams.set('id_token_hint', idTokenHint)
      }

      return logoutUrl.toString()
    } catch {
      return null
    }
  }

  function getStoredOidcState(): OidcStateRecord | null {
    const raw = sessionStorage.getItem(OIDC_STATE_KEY) || localStorage.getItem(OIDC_STATE_KEY)
    if (!raw) {
      return null
    }

    try {
      const parsed = JSON.parse(raw) as OidcStateRecord
      if (!parsed.state || !parsed.nonce) {
        return null
      }

      return parsed
    } catch {
      return null
    }
  }

  function normalizeBiatecSignInOptions(options?: boolean | BiatecSignInOptions): BiatecSignInOptions {
    if (typeof options === 'boolean') {
      return { silentPrompt: options }
    }

    return options ?? {}
  }

  function scheduleTokenRenewal(expiresAtUtc: string) {
    clearRenewalTimer()

    if (getStoredAuthProvider() !== AUTH_PROVIDER_BIATEC || typeof window === 'undefined') {
      return
    }

    const expiryMs = new Date(expiresAtUtc).getTime()
    if (Number.isNaN(expiryMs)) {
      return
    }

    const delay = Math.max(5_000, expiryMs - Date.now() - TOKEN_RENEW_BEFORE_MS)
    renewalTimer = setTimeout(() => {
      const currentPath =
        `${window.location.pathname}${window.location.search}${window.location.hash}` || '/'
      startBiatecOidcSignIn(currentPath, true)
    }, delay)
  }

  function getBiatecTokenFromCallback(): BiatecCallbackSession {
    const url = new URL(window.location.href)
    const query = url.searchParams
    const hash = window.location.hash.startsWith('#')
      ? window.location.hash.slice(1)
      : window.location.hash
    const fragment = new URLSearchParams(hash)

    const readParam = (...keys: string[]) => {
      for (const key of keys) {
        const value = query.get(key) ?? fragment.get(key)
        if (value) {
          return value
        }
      }
      return null
    }

    const oidcError = readParam('error')
    if (oidcError) {
      const description = readParam('error_description')
      throw new Error(description || `OIDC login failed: ${oidcError}`)
    }

    const tokenValue = readParam('id_token', 'access_token', 'token', 'jwt')
    const returnedState = readParam('state')
    if (!tokenValue || !returnedState) {
      throw new Error('OIDC callback is missing required parameters.')
    }

    const pendingState = getStoredOidcState()
    if (!pendingState || pendingState.state !== returnedState) {
      throw new Error('OIDC state validation failed. Please try signing in again.')
    }

    const tokenPayload = parseJwtPayload(tokenValue)
    const nonce = typeof tokenPayload.nonce === 'string' ? tokenPayload.nonce : null
    if (nonce && nonce !== pendingState.nonce) {
      throw new Error('OIDC nonce validation failed. Please try signing in again.')
    }

    const issuer = typeof tokenPayload.iss === 'string' ? tokenPayload.iss : null
    if (!issuer || !BIATEC_OIDC_ALLOWED_ISSUERS.includes(issuer)) {
      throw new Error('OIDC issuer validation failed. Please try signing in again.')
    }

    const audienceClaim = tokenPayload.aud
    const audiences = Array.isArray(audienceClaim)
      ? audienceClaim.filter((entry): entry is string => typeof entry === 'string')
      : typeof audienceClaim === 'string'
        ? [audienceClaim]
        : []
    if (!audiences.includes(BIATEC_OIDC_AUDIENCE)) {
      throw new Error('OIDC audience validation failed. Please try signing in again.')
    }

    const exp = typeof tokenPayload.exp === 'number' ? tokenPayload.exp : null
    const expiresIn = Number(readParam('expires_in') || '')
    const expiresAtUtc = exp
      ? new Date(exp * 1000).toISOString()
      : Number.isFinite(expiresIn) && expiresIn > 0
        ? new Date(Date.now() + expiresIn * 1000).toISOString()
        : new Date(Date.now() + 120 * 60 * 1000).toISOString()

    return {
      token: tokenValue,
      expiresAtUtc,
      redirectPath: normalizeRedirectPath(pendingState.redirectPath),
    }
  }

  function resetBiatecSessionForRetry(_reason = 'drive_access') {
    if (typeof window === 'undefined') {
      return false
    }

    const redirectPath = normalizeRedirectPath(getStoredOidcState()?.redirectPath)

    clearRenewalTimer()
    token.value = null
    player.value = null
    subscription.value = null
    isGameAdmin.value = false
    gameAdminChecked.value = false
    localStorage.removeItem(EXPIRES_KEY)
    localStorage.removeItem(AUTH_PROVIDER_KEY)
    clearStoredOidcState()

    // Skip end_session — the Biatec IdP session is still valid.
    // prompt=consent re-shows the Google consent screen without requiring
    // a post_logout_redirect_uri in the server allowlist.
    startBiatecOidcSignIn(redirectPath, { prompt: 'consent' })
    return true
  }

  function initFromStorage() {
    const expires = localStorage.getItem(EXPIRES_KEY)
    const provider = localStorage.getItem(AUTH_PROVIDER_KEY)
    if (provider && expires && new Date(expires) > new Date()) {
      token.value = COOKIE_SESSION_SENTINEL
      scheduleTokenRenewal(expires)
    } else {
      localStorage.removeItem(EXPIRES_KEY)
      localStorage.removeItem(AUTH_PROVIDER_KEY)
    }
  }

  function setSession(auth: MasterAuthPayload, provider = AUTH_PROVIDER_LOCAL) {
    token.value = provider === AUTH_PROVIDER_BIATEC ? auth.token : COOKIE_SESSION_SENTINEL
    player.value = auth.player
    subscription.value = null
    isGameAdmin.value = false
    gameAdminChecked.value = false
    localStorage.setItem(EXPIRES_KEY, auth.expiresAtUtc)
    setStoredAuthProvider(provider)
    scheduleTokenRenewal(auth.expiresAtUtc)
  }

  async function establishCookieSession(tokenValue: string) {
    const response = await fetch(`${API_BASE_URL}/auth/session`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        Authorization: `Bearer ${tokenValue}`,
      },
    })
    if (!response.ok) {
      throw new Error('Failed to establish secure session.')
    }
  }

  async function refreshGameAdminAccess() {
    if (!token.value) {
      isGameAdmin.value = false
      gameAdminChecked.value = true
      return
    }

    const probeAccess = await probeGameAdminAccess(token.value)
    const normalizedEmail = player.value?.email?.trim().toLowerCase()
    const fallbackAccess = normalizedEmail ? fallbackAdminEmails.has(normalizedEmail) : false

    isGameAdmin.value = probeAccess || fallbackAccess
    gameAdminChecked.value = true
  }

  async function register(email: string, displayName: string, password: string) {
    loading.value = true
    error.value = null
    try {
      const auth = await registerAccount(email, displayName, password)
      await establishCookieSession(auth.token)
      setSession(auth)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Registration failed'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function login(email: string, password: string) {
    loading.value = true
    error.value = null
    try {
      const auth = await loginAccount(email, password)
      await establishCookieSession(auth.token)
      setSession(auth)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Login failed'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function fetchProfile() {
    if (!token.value) return
    try {
      player.value = await fetchMe(token.value)
      await refreshGameAdminAccess()
    } catch {
      // token may have expired
      logout()
    }
  }

  function startBiatecOidcSignIn(redirectPath = '/', options?: boolean | BiatecSignInOptions) {
    const normalizedOptions = normalizeBiatecSignInOptions(options)
    const state = generateOidcRandom(32)
    const nonce = generateOidcRandom(32)

    const stateRecord: OidcStateRecord = {
      state,
      nonce,
      redirectPath: normalizeRedirectPath(redirectPath),
    }
    sessionStorage.setItem(OIDC_STATE_KEY, JSON.stringify(stateRecord))

    const authorizeUrl = new URL(BIATEC_OIDC_AUTHORIZE_URL)
    authorizeUrl.searchParams.set('client_id', BIATEC_OIDC_CLIENT_ID)
    authorizeUrl.searchParams.set('redirect_uri', getConfiguredRedirectUri())
    authorizeUrl.searchParams.set('scope', BIATEC_OIDC_SCOPE)
    authorizeUrl.searchParams.set('response_type', 'id_token')
    authorizeUrl.searchParams.set('response_mode', 'query')
    authorizeUrl.searchParams.set('state', state)
    authorizeUrl.searchParams.set('nonce', nonce)
    if (normalizedOptions.prompt) {
      authorizeUrl.searchParams.set('prompt', normalizedOptions.prompt)
    } else if (normalizedOptions.silentPrompt) {
      authorizeUrl.searchParams.set('prompt', 'none')
    }

    window.location.href = authorizeUrl.toString()
  }

  async function completeBiatecOidcSignIn() {
    const callbackSession = getBiatecTokenFromCallback()
      await establishCookieSession(callbackSession.token)
      token.value = callbackSession.token
      player.value = null
    subscription.value = null
    isGameAdmin.value = false
    gameAdminChecked.value = false
      localStorage.setItem(EXPIRES_KEY, callbackSession.expiresAtUtc)
    setStoredAuthProvider(AUTH_PROVIDER_BIATEC)
    scheduleTokenRenewal(callbackSession.expiresAtUtc)

    try {
      player.value = await fetchMe(callbackSession.token)
      await refreshGameAdminAccess()
      clearStoredOidcState()
      await fetchSubscription()
      return callbackSession.redirectPath
    } catch (e: unknown) {
      clearRenewalTimer()
      token.value = null
      player.value = null
      subscription.value = null
      isGameAdmin.value = false
      gameAdminChecked.value = false
      localStorage.removeItem(EXPIRES_KEY)
      localStorage.removeItem(AUTH_PROVIDER_KEY)
      throw e
    }
  }

  async function fetchSubscription() {
    if (!token.value) return
    try {
      subscription.value = await fetchMySubscription(token.value)
    } catch {
      subscription.value = null
    }
  }

  async function prolong(months: number) {
    if (!token.value) return
    loading.value = true
    error.value = null
    try {
      subscription.value = await prolongSubscription(token.value, months)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to prolong subscription'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function claimStartupPackOffer() {
    if (!token.value) return
    loading.value = true
    error.value = null
    try {
      subscription.value = await claimStartupPack(token.value)
      await fetchProfile()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to claim startup pack'
      throw e
    } finally {
      loading.value = false
    }
  }

  function logout(options: LogoutOptions = {}) {
    const shouldFederatedLogout =
      options.federated === true && getStoredAuthProvider() === AUTH_PROVIDER_BIATEC
    const idTokenHint = token.value && token.value !== COOKIE_SESSION_SENTINEL ? token.value : null
    const federatedLogoutUrl = shouldFederatedLogout ? buildBiatecEndSessionUrl(idTokenHint) : null

    void fetch(`${API_BASE_URL}/auth/logout`, {
      method: 'POST',
      credentials: 'include',
    }).catch(() => undefined)

    clearRenewalTimer()
    token.value = null
    player.value = null
    subscription.value = null
    isGameAdmin.value = false
    gameAdminChecked.value = false
    localStorage.removeItem(EXPIRES_KEY)
    localStorage.removeItem(AUTH_PROVIDER_KEY)
    clearStoredOidcState()

    if (federatedLogoutUrl) {
      window.location.assign(federatedLogoutUrl)
    }
  }

  return {
    player,
    subscription,
    token,
    loading,
    error,
    isGameAdmin,
    gameAdminChecked,
    isAuthenticated,
    initFromStorage,
    register,
    login,
    fetchProfile,
    fetchSubscription,
    prolong,
    claimStartupPackOffer,
    startBiatecOidcSignIn,
    completeBiatecOidcSignIn,
    resetBiatecSessionForRetry,
    refreshGameAdminAccess,
    logout,
  }
})
