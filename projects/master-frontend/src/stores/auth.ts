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
  setWeeklyReportEmailSubscription,
} from '@/lib/masterApi'
import { API_BASE_URL, AUTH_PROVIDER_BIATEC, AUTH_PROVIDER_KEY, AUTH_PROVIDER_LOCAL, BIATEC_OIDC_AUTHORIZE_URL, BIATEC_OIDC_CLIENT_ID, BIATEC_OIDC_SCOPE, COOKIE_SESSION_SENTINEL, EXPIRES_KEY, LOCALE_KEY, OIDC_STATE_KEY, TOKEN_RENEW_BEFORE_MS, buildBiatecEndSessionUrl, clearStoredOidcState, createPkceCodeChallenge, createPkceCodeVerifier, generateOidcRandom, getBiatecTokenFromCallback, getConfiguredRedirectUri, getStoredAuthProvider, getStoredOidcState, normalizeBiatecSignInOptions, normalizeRedirectPath, setStoredAuthProvider } from '@/lib/biatecOidc'
import type { BiatecSignInOptions, OidcStateRecord } from '@/lib/biatecOidc'

interface LogoutOptions {
  federated?: boolean
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
      void startBiatecOidcSignIn(currentPath, true)
    }, delay)
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
    void startBiatecOidcSignIn(redirectPath, { prompt: 'consent' })
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

  function getEmailContext() {
    const rawLocale = localStorage.getItem(LOCALE_KEY) ?? document.documentElement.lang ?? 'en'
    const locale = rawLocale.toLowerCase().split('-')[0]
    return { locale, currentUrl: window.location.href }
  }

  async function register(email: string, displayName: string, password: string) {
    loading.value = true
    error.value = null
    try {
      const { locale, currentUrl } = getEmailContext()
      const auth = await registerAccount(email, displayName, password, locale, currentUrl)
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
      const { locale, currentUrl } = getEmailContext()
      const auth = await loginAccount(email, password, locale, currentUrl)
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

  async function startBiatecOidcSignIn(redirectPath = '/', options?: boolean | BiatecSignInOptions) {
    const normalizedOptions = normalizeBiatecSignInOptions(options)
    const state = generateOidcRandom(32)
    const nonce = generateOidcRandom(32)
    const codeVerifier = createPkceCodeVerifier()
    const codeChallenge = await createPkceCodeChallenge(codeVerifier)

    const stateRecord: OidcStateRecord = {
      state,
      nonce,
      redirectPath: normalizeRedirectPath(redirectPath),
      codeVerifier,
    }
    sessionStorage.setItem(OIDC_STATE_KEY, JSON.stringify(stateRecord))

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

    window.location.href = authorizeUrl.toString()
  }

  async function completeBiatecOidcSignIn() {
    const callbackSession = await getBiatecTokenFromCallback()
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

  async function setWeeklyReportSubscription(subscribed: boolean) {
    if (!token.value) return
    loading.value = true
    error.value = null
    try {
      const result = await setWeeklyReportEmailSubscription(token.value, subscribed)
      if (player.value) {
        player.value = { ...player.value, weeklyReportEmailSubscribed: result }
      }
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update email preferences'
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
    setWeeklyReportSubscription,
    startBiatecOidcSignIn,
    completeBiatecOidcSignIn,
    resetBiatecSessionForRetry,
    refreshGameAdminAccess,
    logout,
  }
})
