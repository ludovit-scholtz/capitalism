import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { gqlRequest as gqlMasterRequest } from '@/lib/graphqlMasterServer'
import { resolvePostLogoutRedirectUri } from '@/lib/authLogout'
import { selectMainCity, shouldAutoSwitchCity } from '@/lib/cityContext'
import { deepEqual } from '@/lib/utils'
import type { AccountContextResult, AccountContextType, Player, AuthPayload, Building, City } from '@/types'

const BIATEC_OIDC_AUTHORIZE_URL = import.meta.env.VITE_BIATEC_OIDC_AUTHORIZE_URL || 'https://google.biatec.io/authorize'
const BIATEC_OIDC_END_SESSION_URL = import.meta.env.VITE_BIATEC_OIDC_END_SESSION_URL || ''
const BIATEC_OIDC_CLIENT_ID = import.meta.env.VITE_BIATEC_OIDC_CLIENT_ID || 'capitalism'
const BIATEC_OIDC_REDIRECT_URI = import.meta.env.VITE_BIATEC_OIDC_REDIRECT_URI
const BIATEC_OIDC_SCOPE = import.meta.env.VITE_BIATEC_OIDC_SCOPE || 'openid'
const BIATEC_OIDC_AUDIENCE = import.meta.env.VITE_BIATEC_OIDC_AUDIENCE || BIATEC_OIDC_CLIENT_ID
const BIATEC_OIDC_ALLOWED_ISSUERS = (import.meta.env.VITE_BIATEC_OIDC_ALLOWED_ISSUERS || 'https://google.biatec.io,https://google.biatec.io')
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

interface OidcPendingState {
  state: string
  nonce: string
  redirectPath: string
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
      startBiatecOidcSignIn(currentPath, true)
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

  function getTokenFromCallback() {
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

    const tokenValue = readParam('id_token', 'access_token', 'token', 'jwt')
    if (!tokenValue) {
      throw new Error('No token was returned from Biatec authentication.')
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
    const audiences = Array.isArray(audienceClaim) ? audienceClaim.filter((entry): entry is string => typeof entry === 'string') : typeof audienceClaim === 'string' ? [audienceClaim] : []
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
      redirectPath: pendingState.redirectPath,
    }
  }

  function getCookieValue(name: string) {
    if (typeof document === 'undefined') {
      return null
    }

    const prefix = `${name}=`
    const match = document.cookie.split('; ').find((entry) => entry.startsWith(prefix))

    return match ? decodeURIComponent(match.slice(prefix.length)) : null
  }

  function clearStoredSession() {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem('auth_token')
      localStorage.removeItem('auth_expires')
      localStorage.removeItem(AUTH_PROVIDER_KEY)
    }

    if (typeof document !== 'undefined') {
      document.cookie = 'auth_token=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/'
      document.cookie = 'auth_expires=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/'
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

  function getStoredToken() {
    if (typeof localStorage === 'undefined') {
      return null
    }

    const stored = localStorage.getItem('auth_token')
    const expires = localStorage.getItem('auth_expires')
    if (stored && expires && new Date(expires) > new Date()) {
      return stored
    }

    const cookieToken = getCookieValue('auth_token')
    const cookieExpires = getCookieValue('auth_expires')
    if (cookieToken && cookieExpires && new Date(cookieExpires) > new Date()) {
      localStorage.setItem('auth_token', cookieToken)
      localStorage.setItem('auth_expires', cookieExpires)
      return cookieToken
    }

    clearStoredSession()
    return null
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
    startBiatecOidcSignIn(redirectPath, { prompt: 'consent' })
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

        const hasValidCurrentCity =
          !!selectedCityId.value && cities.some((city) => city.id === selectedCityId.value)
        if (!hasValidCurrentCity) {
          switchCity(mainCity.id)
        }
        return
      }

      const fallbackCity =
        (playerValue.onboardingCityId
          ? cities.find((city) => city.id === playerValue.onboardingCityId)
          : null) ?? cities[0] ?? null

      if (fallbackCity && selectedCityId.value !== fallbackCity.id) {
        switchCity(fallbackCity.id)
      }
    } catch {
      /* ignore city reconciliation failures to avoid blocking auth */
    }
  }

  const isAuthenticated = computed(() => !!token.value || !!getStoredToken())
  const isAdmin = computed(() => player.value?.role === 'ADMIN')
  const isProSubscriber = computed(() => !!player.value?.proSubscriptionEndsAtUtc && new Date(player.value.proSubscriptionEndsAtUtc).getTime() > Date.now())
  const effectiveProSubscriptionEndsAtUtc = computed(() => player.value?.proSubscriptionEndsAtUtc ?? null)

  function initFromStorage() {
    token.value = getStoredToken()
    selectedCityId.value = getStoredCityId()
    if (token.value) {
      const expiresAtUtc = typeof localStorage !== 'undefined' ? localStorage.getItem('auth_expires') : null
      if (expiresAtUtc) {
        scheduleTokenRenewal(expiresAtUtc)
      }
    }
  }

  function setSession(auth: AuthPayload) {
    applyStoredSession(auth.token, auth.expiresAtUtc)
    player.value = auth.player
  }

  function applyStoredSession(tokenValue: string, expiresAtUtc: string, provider = AUTH_PROVIDER_LOCAL) {
    token.value = tokenValue
    localStorage.setItem('auth_token', tokenValue)
    localStorage.setItem('auth_expires', expiresAtUtc)
    setStoredAuthProvider(provider)
    scheduleTokenRenewal(expiresAtUtc)
    if (typeof document !== 'undefined') {
      document.cookie = `auth_token=${encodeURIComponent(tokenValue)}; path=/`
      document.cookie = `auth_expires=${encodeURIComponent(expiresAtUtc)}; path=/`
    }
  }

  async function fetchCurrentPlayer(options: FetchMeOptions = {}) {
    const data = await gqlRequest<{ me: Player }>(`{ me {${PLAYER_SELECTION}} }`)
    if (!data.me.personalAccountName) {
      data.me.personalAccountName = data.me.displayName
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

  function startBiatecOidcSignIn(redirectPath = '/', options?: boolean | BiatecSignInOptions) {
    if (typeof sessionStorage === 'undefined') {
      throw new Error('OIDC sign-in requires browser session storage.')
    }

    const normalizedOptions = normalizeBiatecSignInOptions(options)
    const normalizedRedirectPath = normalizeRedirectPath(redirectPath)

    const state = createRandomBase64Url()
    const nonce = createRandomBase64Url()

    const pendingState: OidcPendingState = {
      state,
      nonce,
      redirectPath: normalizedRedirectPath,
    }
    sessionStorage.setItem(OIDC_STATE_KEY, JSON.stringify(pendingState))

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

    window.location.assign(authorizeUrl.toString())
  }

  async function completeBiatecOidcSignIn() {
    const callbackSession = getTokenFromCallback()
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
    if (!token.value) {
      initFromStorage()
    }
    if (!token.value) return
    loading.value = true
    try {
      await fetchCurrentPlayer(options)
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
    const idTokenHint = token.value
    const federatedLogoutUrl = shouldFederatedLogout ? buildBiatecEndSessionUrl(idTokenHint) : null

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
