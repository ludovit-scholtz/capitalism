import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import type { AuthPayload } from '@/types'

class MemoryStorage implements Storage {
  private values = new Map<string, string>()

  get length() {
    return this.values.size
  }

  clear() {
    this.values.clear()
  }

  getItem(key: string) {
    return this.values.get(key) ?? null
  }

  key(index: number) {
    return Array.from(this.values.keys())[index] ?? null
  }

  removeItem(key: string) {
    this.values.delete(key)
  }

  setItem(key: string, value: string) {
    this.values.set(key, value)
  }
}

function makeAuthPayload(): AuthPayload {
  return {
    token: 'oidc-token',
    expiresAtUtc: '2030-01-01T00:00:00.000Z',
    player: {
      id: 'player-1',
      email: 'player@example.com',
      displayName: 'Player One',
      personalAccountName: 'Player One',
      gender: 'UNSPECIFIED',
      role: 'PLAYER',
      createdAtUtc: '2026-01-01T00:00:00.000Z',
      lastLoginAtUtc: '2026-01-02T00:00:00.000Z',
      personalCash: 0,
      activeAccountType: 'PERSON',
      activeCompanyId: null,
      onboardingCompletedAtUtc: null,
      onboardingCurrentStep: null,
      onboardingIndustry: null,
      onboardingCityId: null,
      onboardingCompanyId: null,
      onboardingFactoryLotId: null,
      onboardingShopBuildingId: null,
      onboardingFirstSaleCompletedAtUtc: null,
      appliedReferralCode: null,
      proSubscriptionEndsAtUtc: null,
      companies: [],
    },
  }
}

describe('useAuthStore', () => {
  const storage = new MemoryStorage()

  beforeEach(() => {
    storage.clear()
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: storage,
    })
    setActivePinia(createPinia())
  })

  it('persists the session so initFromStorage can restore it after reload', () => {
    const payload = makeAuthPayload()
    const store = useAuthStore()

    store.applyAuthPayload(payload)

    expect(localStorage.getItem('auth_token')).toBe(payload.token)
    expect(localStorage.getItem('auth_expires')).toBe(payload.expiresAtUtc)

    setActivePinia(createPinia())
    const rehydratedStore = useAuthStore()
    rehydratedStore.initFromStorage()

    expect(rehydratedStore.token).toBe(payload.token)
  })
})