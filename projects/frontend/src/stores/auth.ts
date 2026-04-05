import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { deepEqual } from '@/lib/utils'
import type { AccountContextResult, AccountContextType, Player, AuthPayload, StartupPackOffer } from '@/types'

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
  }
`

export const useAuthStore = defineStore('auth', () => {
  const player = ref<Player | null>(null)
  const startupPackOffer = ref<StartupPackOffer | null>(null)
  const token = ref<string | null>(null)
  const proSubscriptionEndsAtUtcOverride = ref<string | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  function getCookieValue(name: string) {
    if (typeof document === 'undefined') {
      return null
    }

    const prefix = `${name}=`
    const match = document.cookie
      .split('; ')
      .find((entry) => entry.startsWith(prefix))

    return match ? decodeURIComponent(match.slice(prefix.length)) : null
  }

  function clearStoredSession() {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem('auth_token')
      localStorage.removeItem('auth_expires')
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

  const isAuthenticated = computed(() => !!token.value || !!getStoredToken())
  const isAdmin = computed(() => player.value?.role === 'ADMIN')
  const isProSubscriber = computed(() => !!player.value?.proSubscriptionEndsAtUtc && new Date(player.value.proSubscriptionEndsAtUtc).getTime() > Date.now())
  const effectiveProSubscriptionEndsAtUtc = computed(
    () => player.value?.proSubscriptionEndsAtUtc ?? proSubscriptionEndsAtUtcOverride.value,
  )

  function initFromStorage() {
    token.value = getStoredToken()
  }

  function setSession(auth: AuthPayload) {
    token.value = auth.token
    player.value = auth.player
    startupPackOffer.value = null
    proSubscriptionEndsAtUtcOverride.value = auth.player.proSubscriptionEndsAtUtc
    localStorage.setItem('auth_token', auth.token)
    localStorage.setItem('auth_expires', auth.expiresAtUtc)
    if (typeof document !== 'undefined') {
      document.cookie = `auth_token=${encodeURIComponent(auth.token)}; path=/`
      document.cookie = `auth_expires=${encodeURIComponent(auth.expiresAtUtc)}; path=/`
    }
  }

  function setStartupPackOffer(offer: StartupPackOffer | null) {
    startupPackOffer.value = offer
  }

  function setProSubscriptionEndsAtUtc(value: string | null) {
    proSubscriptionEndsAtUtcOverride.value = value
    if (player.value) {
      player.value = {
        ...player.value,
        proSubscriptionEndsAtUtc: value,
      }
    }
  }

  async function register(email: string, displayName: string, password: string) {
    loading.value = true
    error.value = null
    try {
      const data = await gqlRequest<{ register: AuthPayload }>(
        `mutation Register($input: RegisterInput!) {
          register(input: $input) {
            token
            expiresAtUtc
            player {${PLAYER_SELECTION}}
          }
        }`,
        { input: { email, displayName, password } },
      )
      setSession(data.register)
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
      const data = await gqlRequest<{ login: AuthPayload }>(
        `mutation Login($input: LoginInput!) {
          login(input: $input) {
            token
            expiresAtUtc
            player {${PLAYER_SELECTION}}
          }
        }`,
        { input: { email, password } },
      )
      setSession(data.login)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Login failed'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function fetchMe() {
    if (!token.value) {
      initFromStorage()
    }
    if (!token.value) return
    loading.value = true
    try {
      const data = await gqlRequest<{ me: Player; startupPackOffer: StartupPackOffer | null }>(
        `{
          me {${PLAYER_SELECTION}}
          startupPackOffer {
            id
            offerKey
            status
            createdAtUtc
            expiresAtUtc
            shownAtUtc
            dismissedAtUtc
            claimedAtUtc
            companyCashGrant
            proDurationDays
            grantedCompanyId
          }
        }`,
      )
      if (!deepEqual(player.value, data.me)) {
        player.value = data.me
      }
      proSubscriptionEndsAtUtcOverride.value = data.me.proSubscriptionEndsAtUtc ?? proSubscriptionEndsAtUtcOverride.value
      if (!deepEqual(startupPackOffer.value, data.startupPackOffer)) {
        startupPackOffer.value = data.startupPackOffer
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

  function logout() {
    token.value = null
    player.value = null
    startupPackOffer.value = null
    proSubscriptionEndsAtUtcOverride.value = null
    clearStoredSession()
  }

  return {
    player,
    startupPackOffer,
    token,
    loading,
    error,
    isAuthenticated,
    isAdmin,
    isProSubscriber,
    effectiveProSubscriptionEndsAtUtc,
    initFromStorage,
    setStartupPackOffer,
    setProSubscriptionEndsAtUtc,
    register,
    login,
    fetchMe,
    switchAccountContext,
    logout,
  }
})
