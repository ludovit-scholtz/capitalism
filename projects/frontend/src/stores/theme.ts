import { defineStore } from 'pinia'
import { ref } from 'vue'

const STORAGE_KEY = 'app_theme'

function detectPreferredTheme(): 'dark' | 'light' {
  // Check stored preference first (works in both browser and node test environments)
  try {
    const stored =
      typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null
    if (stored === 'light' || stored === 'dark') return stored
  } catch {
    // localStorage unavailable (e.g., SSR)
  }
  // Game is intentionally dark-first. Do NOT fall back to prefers-color-scheme
  // because CI headless browsers default to light, which would break dark-mode
  // expectations in tests and give new players the wrong initial experience.
  return 'dark'
}

export const useThemeStore = defineStore('theme', () => {
  const theme = ref<'dark' | 'light'>('dark')

  function applyTheme(t: 'dark' | 'light') {
    if (typeof document !== 'undefined') {
      document.documentElement.setAttribute('data-theme', t)
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
    } catch {}
    applyTheme(theme.value)
  }

  function setTheme(t: 'dark' | 'light') {
    theme.value = t
    try {
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(STORAGE_KEY, t)
      }
    } catch {}
    applyTheme(t)
  }

  const isDark = () => theme.value === 'dark'

  return { theme, init, toggleTheme, setTheme, isDark }
})
