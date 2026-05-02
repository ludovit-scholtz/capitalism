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

const TOKEN_KEY = 'master_auth_token'
const EXPIRES_KEY = 'master_auth_expires'

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

  const isAuthenticated = computed(() => !!token.value)

  function initFromStorage() {
    const stored = localStorage.getItem(TOKEN_KEY)
    const expires = localStorage.getItem(EXPIRES_KEY)
    if (stored && expires && new Date(expires) > new Date()) {
      token.value = stored
    } else {
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(EXPIRES_KEY)
    }
  }

  function setSession(auth: MasterAuthPayload) {
    token.value = auth.token
    player.value = auth.player
    subscription.value = null
    isGameAdmin.value = false
    gameAdminChecked.value = false
    localStorage.setItem(TOKEN_KEY, auth.token)
    localStorage.setItem(EXPIRES_KEY, auth.expiresAtUtc)
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

  function logout() {
    token.value = null
    player.value = null
    subscription.value = null
    isGameAdmin.value = false
    gameAdminChecked.value = false
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(EXPIRES_KEY)
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
    refreshGameAdminAccess,
    logout,
  }
})
