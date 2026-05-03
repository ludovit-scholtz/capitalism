import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { gqlRequest as gqlMasterRequest } from '@/lib/graphqlMasterServer'
import { deepEqual } from '@/lib/utils'
import type { AccountContextResult, AccountContextType, Player, AuthPayload } from '@/types'

const BIATEC_OIDC_AUTHORIZE_URL = import.meta.env.VITE_BIATEC_OIDC_AUTHORIZE_URL || 'https://localhost:44305/authorize'
const BIATEC_OIDC_CLIENT_ID = import.meta.env.VITE_BIATEC_OIDC_CLIENT_ID || 'capitalism'
const BIATEC_OIDC_REDIRECT_URI = import.meta.env.VITE_BIATEC_OIDC_REDIRECT_URI || 'http://localhost:5173/auth/callback'
const BIATEC_OIDC_SCOPE = import.meta.env.VITE_BIATEC_OIDC_SCOPE || 'openid'
const OIDC_STATE_KEY = 'biatec_oidc_state'

interface OidcPendingState {
  state: string
  nonce: string
  redirectPath: string
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

  function createRandomBase64Url(bytes = 24) {
    const random = new Uint8Array(bytes)
    crypto.getRandomValues(random)
    return btoa(String.fromCharCode(...random)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
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

    const exp = typeof tokenPayload.exp === 'number' ? tokenPayload.exp : null
    const expiresAtUtc = exp
      ? new Date(exp * 1000).toISOString()
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
      localStorage.removeItem('selected_city_id')
    }

    if (typeof document !== 'undefined') {
      document.cookie = 'auth_token=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/'
      document.cookie = 'auth_expires=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/'
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

  function deriveMostUsedCityId(playerValue: Player | null): string | null {
    if (!playerValue) return null
    const cityUsage = new Map<string, number>()

    for (const company of playerValue.companies ?? []) {
      for (const building of company.buildings ?? []) {
        cityUsage.set(building.cityId, (cityUsage.get(building.cityId) ?? 0) + 1)
      }
    }

    if (cityUsage.size === 0) {
      return playerValue.onboardingCityId ?? null
    }

    let bestCityId: string | null = null
    let bestCount = -1
    for (const [cityId, count] of cityUsage.entries()) {
      if (count > bestCount) {
        bestCount = count
        bestCityId = cityId
      }
    }
    return bestCityId
  }

  const isAuthenticated = computed(() => !!token.value || !!getStoredToken())
  const isAdmin = computed(() => player.value?.role === 'ADMIN')
  const isProSubscriber = computed(() => !!player.value?.proSubscriptionEndsAtUtc && new Date(player.value.proSubscriptionEndsAtUtc).getTime() > Date.now())
  const effectiveProSubscriptionEndsAtUtc = computed(() => player.value?.proSubscriptionEndsAtUtc ?? null)

  function initFromStorage() {
    token.value = getStoredToken()
    selectedCityId.value = getStoredCityId()
  }

  function setSession(auth: AuthPayload) {
    applyStoredSession(auth.token, auth.expiresAtUtc)
    player.value = auth.player
  }

  function applyStoredSession(tokenValue: string, expiresAtUtc: string) {
    token.value = tokenValue
    localStorage.setItem('auth_token', tokenValue)
    localStorage.setItem('auth_expires', expiresAtUtc)
    if (typeof document !== 'undefined') {
      document.cookie = `auth_token=${encodeURIComponent(tokenValue)}; path=/`
      document.cookie = `auth_expires=${encodeURIComponent(expiresAtUtc)}; path=/`
    }
  }

  async function fetchCurrentPlayer() {
    const data = await gqlRequest<{ me: Player }>(`{ me {${PLAYER_SELECTION}} }`)
    if (!selectedCityId.value) {
      const preferredCityId = deriveMostUsedCityId(data.me)
      if (preferredCityId) {
        switchCity(preferredCityId)
      }
    }
    if (!deepEqual(player.value, data.me)) {
      player.value = data.me
    }
  }

  async function register(email: string, displayName: string, password: string) {
    loading.value = true
    error.value = null
    try {
      const data = await gqlMasterRequest<{ register: MasterSessionPayload }>(MASTER_REGISTER_MUTATION, { input: { email, displayName, password } })
      player.value = null
      applyStoredSession(data.register.token, data.register.expiresAtUtc)
      await fetchCurrentPlayer()
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
      await fetchCurrentPlayer()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Login failed'
      throw e
    } finally {
      loading.value = false
    }
  }

  function startBiatecOidcSignIn(redirectPath = '/') {
    if (typeof sessionStorage === 'undefined') {
      throw new Error('OIDC sign-in requires browser session storage.')
    }

    const state = createRandomBase64Url()
    const nonce = createRandomBase64Url()

    const pendingState: OidcPendingState = {
      state,
      nonce,
      redirectPath,
    }
    sessionStorage.setItem(OIDC_STATE_KEY, JSON.stringify(pendingState))

    const authorizeUrl = new URL(BIATEC_OIDC_AUTHORIZE_URL)
    authorizeUrl.searchParams.set('client_id', BIATEC_OIDC_CLIENT_ID)
    authorizeUrl.searchParams.set('redirect_uri', BIATEC_OIDC_REDIRECT_URI)
    authorizeUrl.searchParams.set('scope', BIATEC_OIDC_SCOPE)
    authorizeUrl.searchParams.set('response_type', 'id_token')
    authorizeUrl.searchParams.set('response_mode', 'query')
    authorizeUrl.searchParams.set('state', state)
    authorizeUrl.searchParams.set('nonce', nonce)

    window.location.assign(authorizeUrl.toString())
  }

  async function completeBiatecOidcSignIn() {
    const callbackSession = getTokenFromCallback()
    applyStoredSession(callbackSession.token, callbackSession.expiresAtUtc)

    try {
      await fetchCurrentPlayer()
      clearPendingOidcState()
      return callbackSession.redirectPath
    } catch (e: unknown) {
      logout()
      throw e
    }
  }

  async function fetchMe() {
    if (!token.value) {
      initFromStorage()
    }
    if (!token.value) return
    loading.value = true
    try {
      await fetchCurrentPlayer()
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

  function logout() {
    token.value = null
    player.value = null
    clearStoredSession()
  }

  return {
    player,
    token,
    loading,
    error,
    selectedCityId,
    isAuthenticated,
    isAdmin,
    isProSubscriber,
    effectiveProSubscriptionEndsAtUtc,
    initFromStorage,
    register,
    login,
    startBiatecOidcSignIn,
    completeBiatecOidcSignIn,
    applyAuthPayload: setSession,
    fetchMe,
    switchAccountContext,
    switchCity,
    logout,
  }
})
