import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { AuthPayload } from '@/types'

const gameGqlMock = vi.fn()
const masterGqlMock = vi.fn()

class MasterGraphQLError extends Error {}

vi.mock('@/lib/graphql', () => ({
  gqlRequest: (...args: unknown[]) => gameGqlMock(...args),
  GraphQLError: class GameGraphQLError extends Error {},
}))

vi.mock('@/lib/graphqlMasterServer', () => ({
  gqlRequest: (...args: unknown[]) => masterGqlMock(...args),
  GraphQLError: MasterGraphQLError,
}))

// Import after mocks are registered so the store binds to the mocked modules.
const { useAuthStore } = await import('@/stores/auth')

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

function base64Url(value: string) {
  return Buffer.from(value, 'utf-8')
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '')
}

function makeFakeJwt(claims: Record<string, unknown>) {
  const header = base64Url(JSON.stringify({ alg: 'none', typ: 'JWT' }))
  const payload = base64Url(JSON.stringify(claims))
  return `${header}.${payload}.signature`
}

function makePlayer() {
  return {
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
  }
}

function makeAuthPayload(): AuthPayload {
  return {
    token: 'oidc-token',
    expiresAtUtc: '2030-01-01T00:00:00.000Z',
    player: makePlayer() as AuthPayload['player'],
  }
}

describe('useAuthStore', () => {
  const storage = new MemoryStorage()
  const sessionStorageMock = new MemoryStorage()
  const fetchMock = vi.fn()

  beforeEach(() => {
    storage.clear()
    sessionStorageMock.clear()
    gameGqlMock.mockReset()
    masterGqlMock.mockReset()
    fetchMock.mockReset()
    fetchMock.mockResolvedValue({ ok: true })

    Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: storage })
    Object.defineProperty(globalThis, 'sessionStorage', { configurable: true, value: sessionStorageMock })
    Object.defineProperty(globalThis, 'fetch', { configurable: true, value: fetchMock })

    // The game API `me`/`cities` requests both route through the mocked game gqlRequest.
    gameGqlMock.mockImplementation((query: string) => {
      if (query.includes('cities')) {
        return Promise.resolve({ cities: [] })
      }
      return Promise.resolve({ me: makePlayer() })
    })

    setActivePinia(createPinia())
  })

  afterEach(() => {
    Reflect.deleteProperty(globalThis, 'window')
  })

  it('keeps the raw JWT out of localStorage and rehydrates from the cookie session after reload', () => {
    const payload = makeAuthPayload()
    const store = useAuthStore()

    store.applyAuthPayload(payload)

    // The bearer token lives in memory only — it must never be persisted.
    expect(store.token).toBe(payload.token)
    expect(localStorage.getItem('auth_token')).toBeNull()
    expect(localStorage.getItem('auth_expires')).toBeNull()
    expect(localStorage.getItem('auth_provider')).toBe('local')

    setActivePinia(createPinia())
    const rehydratedStore = useAuthStore()
    rehydratedStore.initFromStorage()

    // After reload the session is rehydrated from the cookie, not a stored JWT.
    expect(rehydratedStore.token).toBe('cookie-session')
    expect(rehydratedStore.isAuthenticated).toBe(true)
    expect(localStorage.getItem('auth_token')).toBeNull()
  })

  it('login establishes a cookie session without persisting the token', async () => {
    masterGqlMock.mockResolvedValue({ login: { token: 'login-jwt', expiresAtUtc: '2030-01-01T00:00:00.000Z' } })

    const store = useAuthStore()
    await store.login('player@example.com', 'TestPass123!')

    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/auth/session'), expect.objectContaining({ credentials: 'include' }))
    expect(store.token).toBe('login-jwt')
    expect(store.isAuthenticated).toBe(true)
    expect(localStorage.getItem('auth_token')).toBeNull()
    expect(localStorage.getItem('auth_expires')).toBeNull()
    expect(localStorage.getItem('auth_provider')).toBe('local')
  })

  it('OIDC callback exchanges the authorization code via PKCE and bootstraps a cookie session', async () => {
    const state = 'oidc-state'
    const nonce = 'oidc-nonce'
    const codeVerifier = 'oidc-code-verifier'
    sessionStorage.setItem('biatec_oidc_state', JSON.stringify({ state, nonce, redirectPath: '/dashboard', codeVerifier }))

    const idToken = makeFakeJwt({
      iss: 'https://google.biatec.io',
      aud: 'capitalism-pkce',
      nonce,
      exp: Math.floor(Date.now() / 1000) + 3600,
    })

    Object.defineProperty(globalThis, 'window', {
      configurable: true,
      value: { location: new URL(`https://app.test/auth/callback?state=${state}&code=auth-code-123`) },
    })

    fetchMock.mockImplementation((url: string) => {
      if (typeof url === 'string' && url.includes('/token')) {
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve({ idToken: idToken, accessToken: 'access-token', expiresIn: 3600 }),
        })
      }
      return Promise.resolve({ ok: true })
    })

    const store = useAuthStore()
    const redirectPath = await store.completeBiatecOidcSignIn()

    expect(redirectPath).toBe('/dashboard')
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/token'),
      expect.objectContaining({
        method: 'POST',
        body: expect.stringContaining('code_verifier=oidc-code-verifier'),
      }),
    )
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/auth/session'), expect.objectContaining({ credentials: 'include' }))
    expect(store.token).toBe(idToken)
    expect(store.isAuthenticated).toBe(true)
    expect(localStorage.getItem('auth_token')).toBeNull()
    expect(localStorage.getItem('auth_expires')).toBeNull()
    expect(localStorage.getItem('auth_provider')).toBe('biatec_oidc')
  })

  it('logout clears the in-memory token and the persisted session marker', async () => {
    masterGqlMock.mockResolvedValue({ login: { token: 'login-jwt', expiresAtUtc: '2030-01-01T00:00:00.000Z' } })

    const store = useAuthStore()
    await store.login('player@example.com', 'TestPass123!')
    expect(store.isAuthenticated).toBe(true)

    store.logout()

    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/auth/logout'), expect.objectContaining({ credentials: 'include' }))
    expect(store.token).toBeNull()
    expect(store.player).toBeNull()
    expect(store.isAuthenticated).toBe(false)
    expect(localStorage.getItem('auth_provider')).toBeNull()
    expect(localStorage.getItem('auth_token')).toBeNull()
  })
})
