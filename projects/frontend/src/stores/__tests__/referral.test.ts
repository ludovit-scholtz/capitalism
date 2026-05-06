import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useReferralStore } from '@/stores/referral'

// ---------- mocks ----------
const localStorageMock = (() => {
  let store: Record<string, string> = {}
  return {
    getItem: (key: string): string | null => store[key] ?? null,
    setItem: (key: string, value: string) => {
      store[key] = value
    },
    removeItem: (key: string) => {
      delete store[key]
    },
    clear: () => {
      store = {}
    },
  }
})()

describe('useReferralStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorageMock.clear()
    vi.stubGlobal('localStorage', localStorageMock)
    vi.stubGlobal('window', {
      location: { search: '' },
      localStorage: localStorageMock,
    })
  })

  // ---------- initFromUrl ----------

  it('captures a valid referral code from ?ref= query param', () => {
    vi.stubGlobal('window', {
      location: { search: '?ref=ABC123' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromUrl()
    expect(store.pendingCode).toBe('ABC123')
  })

  it('normalizes lowercase code to uppercase', () => {
    vi.stubGlobal('window', {
      location: { search: '?ref=abc123xyz' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromUrl()
    expect(store.pendingCode).toBe('ABC123XYZ')
  })

  it('persists captured code in localStorage', () => {
    vi.stubGlobal('window', {
      location: { search: '?ref=PERSIST1' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromUrl()
    expect(localStorageMock.getItem('pending_referral_code')).toBe('PERSIST1')
  })

  it('ignores a code shorter than 4 characters', () => {
    vi.stubGlobal('window', {
      location: { search: '?ref=AB' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromUrl()
    expect(store.pendingCode).toBeNull()
  })

  it('ignores a code longer than 20 characters', () => {
    vi.stubGlobal('window', {
      location: { search: '?ref=ABCDEFGHIJ12345678901' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromUrl()
    expect(store.pendingCode).toBeNull()
  })

  it('accepts a code at exactly the maximum length (20 chars)', () => {
    vi.stubGlobal('window', {
      location: { search: '?ref=ABCDEFGHIJ1234567890' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromUrl()
    expect(store.pendingCode).toBe('ABCDEFGHIJ1234567890')
  })

  it('ignores a code containing special characters', () => {
    vi.stubGlobal('window', {
      location: { search: '?ref=BAD!CODE' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromUrl()
    expect(store.pendingCode).toBeNull()
  })

  it('does nothing when no ?ref= param is present', () => {
    vi.stubGlobal('window', {
      location: { search: '' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromUrl()
    expect(store.pendingCode).toBeNull()
  })

  // ---------- initFromStorage ----------

  it('loads a stored code from localStorage', () => {
    localStorageMock.setItem('pending_referral_code', 'STORED99')
    vi.stubGlobal('window', {
      location: { search: '' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromStorage()
    expect(store.pendingCode).toBe('STORED99')
  })

  it('does not override an in-memory code already captured from URL', () => {
    vi.stubGlobal('window', {
      location: { search: '?ref=URLCODE1' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromUrl()
    localStorageMock.setItem('pending_referral_code', 'OLDCODE2')
    store.initFromStorage()
    expect(store.pendingCode).toBe('URLCODE1')
  })

  it('ignores an invalid stored code', () => {
    localStorageMock.setItem('pending_referral_code', 'BAD!')
    vi.stubGlobal('window', {
      location: { search: '' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromStorage()
    expect(store.pendingCode).toBeNull()
  })

  it('does nothing when localStorage has no code', () => {
    vi.stubGlobal('window', {
      location: { search: '' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromStorage()
    expect(store.pendingCode).toBeNull()
  })

  // ---------- clearPendingCode ----------

  it('clears the pending code from state and localStorage', () => {
    vi.stubGlobal('window', {
      location: { search: '?ref=CLEAR123' },
      localStorage: localStorageMock,
    })
    const store = useReferralStore()
    store.initFromUrl()
    expect(store.pendingCode).toBe('CLEAR123')

    store.clearPendingCode()
    expect(store.pendingCode).toBeNull()
    expect(localStorageMock.getItem('pending_referral_code')).toBeNull()
  })

  it('is safe to call clearPendingCode when no code is set', () => {
    const store = useReferralStore()
    expect(() => store.clearPendingCode()).not.toThrow()
    expect(store.pendingCode).toBeNull()
  })
})
