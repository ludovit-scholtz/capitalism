import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { gqlRequest as gqlMasterRequest } from '@/lib/graphqlMasterServer'
import { selectMainCity, shouldAutoSwitchCity } from '@/lib/cityContext'
import { deepEqual } from '@/lib/utils'
import { API_BASE_URL, AUTH_PROVIDER_BIATEC, AUTH_PROVIDER_LOCAL, BIATEC_OIDC_AUTHORIZE_URL, BIATEC_OIDC_CLIENT_ID, BIATEC_OIDC_SCOPE, COOKIE_SESSION_SENTINEL, MASTER_SESSION_API_BASE_URL, OIDC_STATE_KEY, SIGNED_OUT_NOTICE_KEY, TOKEN_RENEW_BEFORE_MS, buildBiatecEndSessionUrl, clearPendingOidcState, clearStoredSession, createPkceCodeChallenge, createPkceCodeVerifier, createRandomBase64Url, getConfiguredRedirectUri, getPendingOidcState, getStoredAuthProvider, getStoredCityId, getTokenFromCallback, hasPersistedSessionMarker, normalizeBiatecSignInOptions, normalizeRedirectPath, purgeLegacyTokenStorage, setStoredAuthProvider, setStoredCityId } from '@/lib/biatecOidc'
import type { BiatecSignInOptions, OidcPendingState } from '@/lib/biatecOidc'
import type { AccountContextResult, AccountContextType, Player, AuthPayload, Building, City } from '@/types'

interface LogoutOptions {
  federated?: boolean
}

interface FetchMeOptions {
  reconcileCityContext?: boolean
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
