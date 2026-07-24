import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { gqlRequest as gqlMasterRequest } from '@/lib/graphqlMasterServer'
import { resolvePostLogoutRedirectUri } from '@/lib/authLogout'
import { selectMainCity, shouldAutoSwitchCity } from '@/lib/cityContext'
import { resolveApiBaseUrl, resolveGameGraphqlUrl, resolveOptionalMasterApiBaseUrl } from '@/lib/runtimeGraphqlUrl'
import { readRuntimeAppConfigValue } from '@/lib/runtimeConfig'
import { deepEqual } from '@/lib/utils'
import type { AccountContextResult, AccountContextType, Player, AuthPayload, Building, City } from '@/types'

const BIATEC_OIDC_AUTHORIZE_URL =
  readRuntimeAppConfigValue('biatecOidcAuthorizeUrl') || import.meta.env.VITE_BIATEC_OIDC_AUTHORIZE_URL || 'https://google.biatec.io/authorize'
const BIATEC_OIDC_TOKEN_URL =
  readRuntimeAppConfigValue('biatecOidcTokenUrl') || import.meta.env.VITE_BIATEC_OIDC_TOKEN_URL || 'https://google.biatec.io/token'
const BIATEC_OIDC_END_SESSION_URL =
  readRuntimeAppConfigValue('biatecOidcEndSessionUrl') || import.meta.env.VITE_BIATEC_OIDC_END_SESSION_URL || ''
const BIATEC_OIDC_CLIENT_ID =
  readRuntimeAppConfigValue('biatecOidcClientId') || import.meta.env.VITE_BIATEC_OIDC_CLIENT_ID || 'capitalism-pkce'
const BIATEC_OIDC_REDIRECT_URI =
  readRuntimeAppConfigValue('biatecOidcRedirectUri') || import.meta.env.VITE_BIATEC_OIDC_REDIRECT_URI
const BIATEC_OIDC_SCOPE =
  readRuntimeAppConfigValue('biatecOidcScope') || import.meta.env.VITE_BIATEC_OIDC_SCOPE || 'openid profile email'
const BIATEC_OIDC_AUDIENCE =
  readRuntimeAppConfigValue('biatecOidcAudience') || import.meta.env.VITE_BIATEC_OIDC_AUDIENCE || BIATEC_OIDC_CLIENT_ID
const BIATEC_OIDC_ALLOWED_ISSUERS = (readRuntimeAppConfigValue('biatecOidcAllowedIssuers') || import.meta.env.VITE_BIATEC_OIDC_ALLOWED_ISSUERS || 'https://google.biatec.io,https://google.biatec.io')
  .split(',')
  .map((entry: string) => entry.trim())
  .filter((entry: string) => entry.length > 0)
const TOKEN_RENEW_BEFORE_MS = 60 * 1000
const OIDC_STATE_KEY = 'biatec_oidc_state'
const OIDC_LOGOUT_STATE_KEY = 'biatec_oidc_logout_state'
const SIGNED_OUT_NOTICE_KEY = 'auth_signed_out_notice'
const AUTH_PROVIDER_KEY = 'auth_provider'
const AUTH_PROVIDER_LOCAL = 'local'
const AUTH_PROVIDER_BIATEC = 'biatec_oidc'
const COOKIE_SESSION_SENTINEL = 'cookie-session'
const API_BASE_URL = resolveApiBaseUrl(resolveGameGraphqlUrl(import.meta.env.VITE_GRAPHQL_URL))
const MASTER_SESSION_API_BASE_URL = resolveOptionalMasterApiBaseUrl(import.meta.env.VITE_MASTER_GRAPHQL_URL)

interface OidcPendingState {
  state: string
  nonce: string
  redirectPath: string
  codeVerifier: string
}

interface BiatecSignInOptions {
  silentPrompt?: boolean
  prompt?: string
}

interface LogoutOptions {
  federated?: boolean
}

interface FetchMeOptions {
  reconcileCityContext?: boolean
}

function normalizeRedirectPath(redirectPath: string | null | undefined) {
  if (!redirectPath || !redirectPath.startsWith('/')) {
    return '/'
  }

  return redirectPath
}

const PLAYER_SELECTION = `
  id
  displayName
  personalAccountName
  gender
  email
  role
  createdAtUtc
  lastLoginAtUtc
  personalCash
  activeAccountType
  activeCompanyId
  onboardingCompletedAtUtc
  onboardingCurrentStep
  onboardingIndustry
  onboardingCityId
  onboardingCompanyId
  onboardingFactoryLotId
  onboardingShopBuildingId
  onboardingFirstSaleCompletedAtUtc
  appliedReferralCode
  proSubscriptionEndsAtUtc
  companies {
    id
    name
    cash
    foundedAtUtc
    foundedAtTick
    buildings {
      id
      cityId
    }
  }
`

interface MasterSessionPayload {
  token: string
  expiresAtUtc: string
}

const MASTER_REGISTER_MUTATION = `
  mutation Register($input: RegisterInput!) {
    register(input: $input) {
      token
      expiresAtUtc
    }
  }
`

const MASTER_LOGIN_MUTATION = `
  mutation Login($input: LoginInput!) {
    login(input: $input) {
      token
      expiresAtUtc
    }
  }
`

export const useAuthStore = defineStore('auth', () => {
  const player = ref<Player | null>(null)
  const token = ref<string | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const selectedCityId = ref<string | null>(null)
  const autoSwitchedMainCityName = ref<string | null>(null)
  let renewalTimer: ReturnType<typeof setTimeout> | null = null

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

    return 'http://localhost:5173/auth/callback'
  }

  function getStoredAuthProvider() {
    if (typeof localStorage === 'undefined') {
      return AUTH_PROVIDER_LOCAL
    }

    return localStorage.getItem(AUTH_PROVIDER_KEY) || AUTH_PROVIDER_LOCAL
  }

  function setStoredAuthProvider(provider: string) {
    if (typeof localStorage === 'undefined') {
      return
    }

    localStorage.setItem(AUTH_PROVIDER_KEY, provider)
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
      const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}` || '/'
      void startBiatecOidcSignIn(currentPath, true)
    }, delay)
  }

  function normalizeBiatecSignInOptions(options?: boolean | BiatecSignInOptions): BiatecSignInOptions {
    if (typeof options === 'boolean') {
      return { silentPrompt: options }
    }

    return options ?? {}
  }

  function createRandomBase64Url(bytes = 24) {
    const random = new Uint8Array(bytes)
    crypto.getRandomValues(random)
    return btoa(String.fromCharCode(...random))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/g, '')
  }

  function arrayBufferToBase64Url(buffer: ArrayBuffer) {
    const bytes = new Uint8Array(buffer)
    let binary = ''
    for (const byte of bytes) {
      binary += String.fromCharCode(byte)
    }
    return btoa(binary)
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/g, '')
  }

  // PKCE (RFC 7636): the code_verifier must be 43-128 chars from the unreserved
  // set [A-Za-z0-9-._~]; createRandomBase64Url's alphabet is a subset of that.
  function createPkceCodeVerifier() {
    return createRandomBase64Url(64)
  }

  async function createPkceCodeChallenge(codeVerifier: string) {
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(codeVerifier))
    return arrayBufferToBase64Url(digest)
  }

  function getPendingOidcState() {
    if (typeof sessionStorage === 'undefined') {
      return null
    }

    const raw = sessionStorage.getItem(OIDC_STATE_KEY)
    if (!raw) {
      return null
    }

    try {
      const parsed = JSON.parse(raw) as OidcPendingState
      if (!parsed.state || !parsed.nonce || !parsed.redirectPath) {
        return null
      }
      return parsed
    } catch {
      return null
    }
  }

  function clearPendingOidcState() {
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.removeItem(OIDC_STATE_KEY)
    }
  }

  function createLogoutState() {
    const state = createRandomBase64Url()
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.setItem(OIDC_LOGOUT_STATE_KEY, state)
    }
    return state
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

  function parseJwtPayload(jwt: string): Record<string, unknown> {
    const parts = jwt.split('.')
    if (parts.length !== 3 || !parts[1]) {
      throw new Error('Invalid OIDC token format.')
    }

    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')
    const decoded = atob(padded)
    return JSON.parse(decoded) as Record<string, unknown>
  }

  function getAuthorizationCodeFromCallback() {
    const query = new URLSearchParams(window.location.search)
    const hash = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash
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

    const errorCode = readParam('error')
    if (errorCode) {
      const description = readParam('error_description')
      throw new Error(description || `OIDC login failed: ${errorCode}`)
    }

    const callbackState = readParam('state')
    const pendingState = getPendingOidcState()
    if (!pendingState || !callbackState || callbackState !== pendingState.state) {
      throw new Error('OIDC state validation failed. Please try signing in again.')
    }

    const code = readParam('code')
    if (!code) {
      throw new Error('No authorization code was returned from Biatec authentication.')
    }

    return { code, pendingState }
  }

  interface BiatecTokenResponse {
    access_token?: string
    id_token?: string
    expires_in?: number
    token_type?: string
  }

  // Public client (PKCE) token exchange: no client_secret, client authentication
  // happens via the code_verifier proving possession of the original request.
  async function exchangeAuthorizationCode(code: string, codeVerifier: string, redirectUri: string) {
    const body = new URLSearchParams()
    body.set('grant_type', 'authorization_code')
    body.set('code', code)
    body.set('redirect_uri', redirectUri)
    body.set('client_id', BIATEC_OIDC_CLIENT_ID)
    body.set('code_verifier', codeVerifier)

    const response = await fetch(BIATEC_OIDC_TOKEN_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: body.toString(),
    })

    if (!response.ok) {
      throw new Error('Failed to exchange the Biatec authorization code for a token.')
    }

    return (await response.json()) as BiatecTokenResponse
  }

  function validateBiatecIdToken(idToken: string, pendingState: OidcPendingState) {
    const tokenPayload = parseJwtPayload(idToken)
    const nonce = typeof tokenPayload.nonce === 'string' ? tokenPayload.nonce : null
    if (nonce && nonce !== pendingState.nonce) {
      throw new Error('OIDC nonce validation failed. Please try signing in again.')
    }

    const issuer = typeof tokenPayload.iss === 'string' ? tokenPayload.iss : null
    if (!issuer || !BIATEC_OIDC_ALLOWED_ISSUERS.includes(issuer)) {
      throw new Error('OIDC issuer validation failed. Please try signing in again.')
    }

    const audienceClaim = tokenPayload.aud
    const audiences = Array.isArray(audienceClaim) ? audienceClaim.filter((entry): entry is string => typeof entry === 'string') : typeof audienceClaim === 'string' ? [audienceClaim] : []
    if (!audiences.includes(BIATEC_OIDC_AUDIENCE)) {
      throw new Error('OIDC audience validation failed. Please try signing in again.')
    }

    return tokenPayload
  }

  function resolveTokenExpiryUtc(tokenPayload: Record<string, unknown>, expiresIn?: number) {
    const exp = typeof tokenPayload.exp === 'number' ? tokenPayload.exp : null
    return exp
      ? new Date(exp * 1000).toISOString()
      : typeof expiresIn === 'number' && expiresIn > 0
        ? new Date(Date.now() + expiresIn * 1000).toISOString()
        : new Date(Date.now() + 120 * 60 * 1000).toISOString()
  }

  async function getTokenFromCallback() {
    const { code, pendingState } = getAuthorizationCodeFromCallback()
    const tokenResponse = await exchangeAuthorizationCode(code, pendingState.codeVerifier, getConfiguredRedirectUri())

    const idToken = tokenResponse.id_token
    if (!idToken) {
      throw new Error('No ID token was returned from Biatec authentication.')
    }

    const tokenPayload = validateBiatecIdToken(idToken, pendingState)
    const expiresAtUtc = resolveTokenExpiryUtc(tokenPayload, tokenResponse.expires_in)

    return {
      token: idToken,
      expiresAtUtc,
      redirectPath: pendingState.redirectPath,
    }
  }

  function clearStoredSession() {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem('auth_token')
      localStorage.removeItem('auth_expires')
      localStorage.removeItem(AUTH_PROVIDER_KEY)
    }
  }

  function getPostLogoutRedirectUri() {
    if (typeof window === 'undefined') {
      return resolvePostLogoutRedirectUri(undefined)
    }

    return resolvePostLogoutRedirectUri(window.location.origin)
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

  // Raw JWTs are never persisted in localStorage. Older builds stored
  // `auth_token`/`auth_expires`; proactively purge any such legacy values so a
  // previously-leaked token cannot be picked up after upgrading. Returns true
  // when a legacy session token was present so the session can be rehydrated
  // from the secure cookie instead.
  function purgeLegacyTokenStorage() {
    if (typeof localStorage === 'undefined') {
      return false
    }
    const hadLegacyToken = localStorage.getItem('auth_token') !== null
    localStorage.removeItem('auth_token')
    localStorage.removeItem('auth_expires')
    return hadLegacyToken
  }

  // A non-sensitive provider marker records that an authenticated session was
  // established. It lets us optimistically rehydrate from the secure cookie
  // session on reload without exposing the bearer token.
  function hasPersistedSessionMarker() {
    if (typeof localStorage === 'undefined') {
      return false
    }
    return localStorage.getItem(AUTH_PROVIDER_KEY) !== null
  }

  function getStoredCityId() {
    if (typeof localStorage === 'undefined') {
      return null
    }
    return localStorage.getItem('selected_city_id')
  }

  function setStoredCityId(cityId: string | null) {
    if (typeof localStorage === 'undefined') {
      return
    }
    if (cityId) {
      localStorage.setItem('selected_city_id', cityId)
    } else {
      localStorage.removeItem('selected_city_id')
    }
  }

  function resetBiatecSessionForRetry(_reason = 'drive_access') {
    if (typeof window === 'undefined') {
      return false
    }

    const redirectPath = normalizeRedirectPath(getPendingOidcState()?.redirectPath)

    clearRenewalTimer()
    token.value = null
    player.value = null
    clearStoredSession()
    clearPendingOidcState()

    // Skip end_session — the Biatec IdP session is still valid.
    // prompt=consent re-shows the Google consent screen without requiring
    // a post_logout_redirect_uri in the server allowlist.
    void startBiatecOidcSignIn(redirectPath, { prompt: 'consent' })
    return true
  }

  function getPlayerBuildings(playerValue: Player | null): Building[] {
    return playerValue?.companies.flatMap((company) => company.buildings ?? []) ?? []
  }

  async function loadContextCities() {
    const data = await gqlRequest<{ cities: Pick<City, 'id' | 'name'>[] }>(`{ cities { id name } }`)
    return data?.cities ?? []
  }

  async function reconcileCityContext(playerValue: Player | null) {
    autoSwitchedMainCityName.value = null

    if (!playerValue) {
      return
    }

    try {
      const cities = await loadContextCities()
      if (cities.length === 0) {
        return
      }

      const buildings = getPlayerBuildings(playerValue)
      const mainCity = selectMainCity(cities, buildings)

      if (mainCity) {
        if (shouldAutoSwitchCity(selectedCityId.value, mainCity.id, buildings)) {
          switchCity(mainCity.id)
          autoSwitchedMainCityName.value = mainCity.name
          return
        }

        const hasValidCurrentCity = !!selectedCityId.value && cities.some((city) => city.id === selectedCityId.value)
        if (!hasValidCurrentCity) {
          switchCity(mainCity.id)
        }
        return
      }

      const fallbackCity = (playerValue.onboardingCityId ? cities.find((city) => city.id === playerValue.onboardingCityId) : null) ?? cities[0] ?? null

      if (fallbackCity && selectedCityId.value !== fallbackCity.id) {
        switchCity(fallbackCity.id)
      }
    } catch {
      /* ignore city reconciliation failures to avoid blocking auth */
    }
  }

  const isAuthenticated = computed(() => !!token.value || !!player.value)
  const isAdmin = computed(() => player.value?.role === 'ADMIN')
  const isProSubscriber = computed(() => !!player.value?.proSubscriptionEndsAtUtc && new Date(player.value.proSubscriptionEndsAtUtc).getTime() > Date.now())
  const effectiveProSubscriptionEndsAtUtc = computed(() => player.value?.proSubscriptionEndsAtUtc ?? null)

  function initFromStorage() {
    selectedCityId.value = getStoredCityId()
    const hadLegacyToken = purgeLegacyTokenStorage()
    // Rehydrate the gameplay session from the secure cookie rather than a
    // persisted JWT. A non-sensitive provider marker (or a legacy bootstrap
    // token from an older build) indicates a prior session; marking the
    // in-memory token with the cookie sentinel keeps the user authenticated
    // until fetchMe() validates the cookie session.
    if (hasPersistedSessionMarker() || hadLegacyToken) {
      token.value = COOKIE_SESSION_SENTINEL
    }
  }

  function setSession(auth: AuthPayload) {
    applyStoredSession(auth.token, auth.expiresAtUtc)
    player.value = auth.player
  }

  function applyStoredSession(tokenValue: string, expiresAtUtc: string, provider = AUTH_PROVIDER_LOCAL) {
    token.value = tokenValue
    // The bearer token is held in memory only; gameplay requests authenticate
    // via the secure cookie session. Persist just the non-sensitive provider
    // marker so the session can be rehydrated from the cookie on reload.
    setStoredAuthProvider(provider)
    scheduleTokenRenewal(expiresAtUtc)
  }

  async function establishCookieSession(apiBaseUrl: string, tokenValue: string) {
    const response = await fetch(`${apiBaseUrl}/auth/session`, {
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

  async function establishOptionalCookieSession(apiBaseUrl: string, tokenValue: string) {
    if (!apiBaseUrl) {
      return
    }

    try {
      await establishCookieSession(apiBaseUrl, tokenValue)
    } catch {
      return
    }
  }

  async function fetchCurrentPlayer(options: FetchMeOptions = {}) {
    const data = await gqlRequest<{ me: Player }>(`{ me {${PLAYER_SELECTION}} }`)
    if (!data.me.personalAccountName) {
      data.me.personalAccountName = data.me.displayName
    }
    if (!data.me.gender) {
      data.me.gender = 'UNSPECIFIED'
    }
    if (options.reconcileCityContext) {
      await reconcileCityContext(data.me)
    }
    if (!deepEqual(player.value, data.me)) {
      player.value = data.me
    }
  }

  async function register(email: string, displayName: string, password: string, referralCode?: string | null) {
    loading.value = true
    error.value = null
    try {
      const input: Record<string, string> = { email, displayName, password }
      if (referralCode) {
        input.referralCode = referralCode
      }
      const data = await gqlMasterRequest<{ register: MasterSessionPayload }>(MASTER_REGISTER_MUTATION, { input })
      await Promise.all([establishCookieSession(API_BASE_URL, data.register.token), establishOptionalCookieSession(MASTER_SESSION_API_BASE_URL, data.register.token)])
      player.value = null
      applyStoredSession(data.register.token, data.register.expiresAtUtc)
      await fetchCurrentPlayer({ reconcileCityContext: true })
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
      const data = await gqlMasterRequest<{ login: MasterSessionPayload }>(MASTER_LOGIN_MUTATION, { input: { email, password } })
      await Promise.all([establishCookieSession(API_BASE_URL, data.login.token), establishOptionalCookieSession(MASTER_SESSION_API_BASE_URL, data.login.token)])
      player.value = null
      applyStoredSession(data.login.token, data.login.expiresAtUtc)
      await fetchCurrentPlayer({ reconcileCityContext: true })
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Login failed'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function startBiatecOidcSignIn(redirectPath = '/', options?: boolean | BiatecSignInOptions) {
    if (typeof sessionStorage === 'undefined') {
      throw new Error('OIDC sign-in requires browser session storage.')
    }

    const normalizedOptions = normalizeBiatecSignInOptions(options)
    const normalizedRedirectPath = normalizeRedirectPath(redirectPath)

    const state = createRandomBase64Url()
    const nonce = createRandomBase64Url()
    const codeVerifier = createPkceCodeVerifier()
    const codeChallenge = await createPkceCodeChallenge(codeVerifier)

    const pendingState: OidcPendingState = {
      state,
      nonce,
      redirectPath: normalizedRedirectPath,
      codeVerifier,
    }
    sessionStorage.setItem(OIDC_STATE_KEY, JSON.stringify(pendingState))

    const authorizeUrl = new URL(BIATEC_OIDC_AUTHORIZE_URL)
    authorizeUrl.searchParams.set('client_id', BIATEC_OIDC_CLIENT_ID)
    authorizeUrl.searchParams.set('redirect_uri', getConfiguredRedirectUri())
    authorizeUrl.searchParams.set('scope', BIATEC_OIDC_SCOPE)
    authorizeUrl.searchParams.set('response_type', 'code')
    authorizeUrl.searchParams.set('state', state)
    authorizeUrl.searchParams.set('nonce', nonce)
    authorizeUrl.searchParams.set('code_challenge', codeChallenge)
    authorizeUrl.searchParams.set('code_challenge_method', 'S256')
    if (normalizedOptions.prompt) {
      authorizeUrl.searchParams.set('prompt', normalizedOptions.prompt)
    } else if (normalizedOptions.silentPrompt) {
      authorizeUrl.searchParams.set('prompt', 'none')
    }

    window.location.assign(authorizeUrl.toString())
  }

  async function completeBiatecOidcSignIn() {
    const callbackSession = await getTokenFromCallback()
    await Promise.all([establishCookieSession(API_BASE_URL, callbackSession.token), establishOptionalCookieSession(MASTER_SESSION_API_BASE_URL, callbackSession.token)])
    applyStoredSession(callbackSession.token, callbackSession.expiresAtUtc, AUTH_PROVIDER_BIATEC)

    try {
      await fetchCurrentPlayer({ reconcileCityContext: true })
      clearPendingOidcState()
      return callbackSession.redirectPath
    } catch (e: unknown) {
      clearRenewalTimer()
      token.value = null
      player.value = null
      clearStoredSession()
      throw e
    }
  }

  async function fetchMe(options: FetchMeOptions = {}) {
    loading.value = true
    try {
      await fetchCurrentPlayer(options)
      if (!token.value) {
        token.value = COOKIE_SESSION_SENTINEL
      }
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to load account'

      if (e instanceof GraphQLError && /not authenticated/i.test(e.message)) {
        logout()
      }
    } finally {
      loading.value = false
    }
  }

  async function switchAccountContext(accountType: AccountContextType, companyId?: string | null) {
    loading.value = true
    error.value = null
    try {
      const data = await gqlRequest<{ switchAccountContext: AccountContextResult }>(
        `mutation SwitchAccountContext($input: SwitchAccountContextInput!) {
          switchAccountContext(input: $input) {
            activeAccountType
            activeCompanyId
            activeAccountName
          }
        }`,
        {
          input: {
            accountType,
            companyId: accountType === 'COMPANY' ? companyId : null,
          },
        },
      )

      await fetchMe()
      return data.switchAccountContext
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to switch account context'
      throw e
    } finally {
      loading.value = false
    }
  }

  function switchCity(cityId: string) {
    selectedCityId.value = cityId
    setStoredCityId(cityId)
  }

  function clearAutoSwitchedMainCityName() {
    autoSwitchedMainCityName.value = null
  }

  /**
   * Clears local auth state and optionally starts federated logout.
   * @param options Logout options where `federated` requests OIDC end-session redirect when available.
   * Returns true when federated redirect navigation was started.
   */
  function logout(options: LogoutOptions = {}): boolean {
    const shouldFederatedLogout = options.federated === true && getStoredAuthProvider() === AUTH_PROVIDER_BIATEC
    const idTokenHint = token.value && token.value !== COOKIE_SESSION_SENTINEL ? token.value : null
    const federatedLogoutUrl = shouldFederatedLogout ? buildBiatecEndSessionUrl(idTokenHint) : null

    void fetch(`${API_BASE_URL}/auth/logout`, {
      method: 'POST',
      credentials: 'include',
    }).catch(() => undefined)

    clearRenewalTimer()
    token.value = null
    player.value = null
    autoSwitchedMainCityName.value = null
    clearStoredSession()
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.setItem(SIGNED_OUT_NOTICE_KEY, '1')
    }

    if (federatedLogoutUrl) {
      window.location.assign(federatedLogoutUrl)
      return true
    }

    return false
  }

  return {
    player,
    token,
    loading,
    error,
    selectedCityId,
    autoSwitchedMainCityName,
    isAuthenticated,
    isAdmin,
    isProSubscriber,
    effectiveProSubscriptionEndsAtUtc,
    initFromStorage,
    register,
    login,
    startBiatecOidcSignIn,
    completeBiatecOidcSignIn,
    resetBiatecSessionForRetry,
    applyAuthPayload: setSession,
    fetchMe,
    switchAccountContext,
    switchCity,
    clearAutoSwitchedMainCityName,
    logout,
  }
})
