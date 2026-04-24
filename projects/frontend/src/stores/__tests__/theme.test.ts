import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useThemeStore } from '@/stores/theme'

// Mock localStorage and document
const localStorageMock = (() => {
  let store: Record<string, string> = {}
  return {
    getItem: (key: string) => store[key] ?? null,
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

const documentMock = {
  documentElement: {
    setAttribute: vi.fn(),
  },
}

describe('useThemeStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorageMock.clear()
    vi.stubGlobal('localStorage', localStorageMock)
    vi.stubGlobal('document', documentMock)
    documentMock.documentElement.setAttribute.mockClear()
  })

  it('defaults to dark theme when no preference is stored', () => {
    const store = useThemeStore()
    store.init()
    expect(store.theme).toBe('dark')
  })

  it('init reads stored theme from localStorage', () => {
    localStorageMock.setItem('app_theme', 'light')
    const store = useThemeStore()
    store.init()
    expect(store.theme).toBe('light')
    expect(documentMock.documentElement.setAttribute).toHaveBeenCalledWith('data-theme', 'light')
  })

  it('init reads dark from localStorage', () => {
    localStorageMock.setItem('app_theme', 'dark')
    const store = useThemeStore()
    store.init()
    expect(store.theme).toBe('dark')
    expect(documentMock.documentElement.setAttribute).toHaveBeenCalledWith('data-theme', 'dark')
  })

  it('init ignores invalid stored values and falls back to dark', () => {
    localStorageMock.setItem('app_theme', 'blue')
    const store = useThemeStore()
    store.init()
    expect(store.theme).toBe('dark')
  })

  it('toggleTheme switches from dark to light', () => {
    localStorageMock.setItem('app_theme', 'dark')
    const store = useThemeStore()
    store.init()
    store.toggleTheme()
    expect(store.theme).toBe('light')
    expect(localStorageMock.getItem('app_theme')).toBe('light')
    expect(documentMock.documentElement.setAttribute).toHaveBeenLastCalledWith('data-theme', 'light')
  })

  it('toggleTheme switches from light to dark', () => {
    localStorageMock.setItem('app_theme', 'light')
    const store = useThemeStore()
    store.init()
    store.toggleTheme()
    expect(store.theme).toBe('dark')
    expect(localStorageMock.getItem('app_theme')).toBe('dark')
    expect(documentMock.documentElement.setAttribute).toHaveBeenLastCalledWith('data-theme', 'dark')
  })

  it('setTheme sets explicit theme', () => {
    const store = useThemeStore()
    store.setTheme('light')
    expect(store.theme).toBe('light')
    expect(localStorageMock.getItem('app_theme')).toBe('light')
    expect(documentMock.documentElement.setAttribute).toHaveBeenCalledWith('data-theme', 'light')
  })

  it('isDark returns true when theme is dark', () => {
    const store = useThemeStore()
    store.setTheme('dark')
    expect(store.isDark()).toBe(true)
  })

  it('isDark returns false when theme is light', () => {
    const store = useThemeStore()
    store.setTheme('light')
    expect(store.isDark()).toBe(false)
  })

  it('multiple toggles cycle correctly', () => {
    const store = useThemeStore()
    store.init() // dark
    store.toggleTheme() // light
    store.toggleTheme() // dark
    store.toggleTheme() // light
    expect(store.theme).toBe('light')
  })
})
