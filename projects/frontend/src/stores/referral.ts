import { defineStore } from 'pinia'
import { ref } from 'vue'

const REFERRAL_CODE_KEY = 'pending_referral_code'
const REFERRAL_CODE_PARAM = 'ref'
const REFERRAL_CODE_PATTERN = /^[A-Z0-9]{4,20}$/

/**
 * Stores a referral code captured from the URL `?ref=CODE` query parameter.
 * The code is persisted in localStorage so it survives page reloads,
 * and is cleared once the player registers or logs in.
 */
export const useReferralStore = defineStore('referral', () => {
  const pendingCode = ref<string | null>(null)

  function isValidCode(code: string): boolean {
    return REFERRAL_CODE_PATTERN.test(code)
  }

  /**
   * Reads the referral code from the `?ref=` URL query parameter.
   * If valid, stores it in Pinia state and localStorage.
   */
  function initFromUrl() {
    if (typeof window === 'undefined') return

    const params = new URLSearchParams(window.location.search)
    const raw = params.get(REFERRAL_CODE_PARAM)
    if (!raw) return

    const normalized = raw.trim().toUpperCase()
    if (!isValidCode(normalized)) return

    pendingCode.value = normalized
    try {
      localStorage.setItem(REFERRAL_CODE_KEY, normalized)
    } catch {
      // Ignore storage errors
    }
  }

  /**
   * Reads the referral code from localStorage (for page reload persistence).
   * Does not override a code already captured from the URL in this session.
   */
  function initFromStorage() {
    if (pendingCode.value) return
    if (typeof localStorage === 'undefined') return

    try {
      const stored = localStorage.getItem(REFERRAL_CODE_KEY)
      if (stored && isValidCode(stored)) {
        pendingCode.value = stored
      }
    } catch {
      // Ignore storage errors
    }
  }

  /**
   * Clears the pending referral code after it has been applied (on registration).
   */
  function clearPendingCode() {
    pendingCode.value = null
    try {
      localStorage.removeItem(REFERRAL_CODE_KEY)
    } catch {
      // Ignore storage errors
    }
  }

  return {
    pendingCode,
    initFromUrl,
    initFromStorage,
    clearPendingCode,
  }
})
