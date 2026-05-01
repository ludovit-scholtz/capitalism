import { defineStore } from 'pinia'
import { ref } from 'vue'

const STORAGE_KEY = 'app_theme'

function detectPreferredTheme(): 'dark' | 'light' {
  try {
    const stored = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null
    if (stored === 'light' || stored === 'dark') {
      return stored
    }
  } catch {
    // localStorage unavailable
  }
  return 'dark'
}

export const useThemeStore = defineStore('theme', () => {
  const theme = ref<'dark' | 'light'>('dark')

  function applyTheme(nextTheme: 'dark' | 'light') {
    if (typeof document !== 'undefined') {
      document.documentElement.setAttribute('data-theme', nextTheme)
    }
  }

  function init() {
    theme.value = detectPreferredTheme()
    applyTheme(theme.value)
  }

  function toggleTheme() {
    theme.value = theme.value === 'dark' ? 'light' : 'dark'
    try {
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(STORAGE_KEY, theme.value)
      }
    } catch {
      // ignore storage write failures
    }
    applyTheme(theme.value)
  }

  return {
    theme,
    init,
    toggleTheme,
  }
})
