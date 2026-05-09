import { describe, expect, it } from 'vitest'
import { isPasswordAuthEnabled, shouldAutoStartOidc } from '../authMode'

describe('authMode', () => {
  it('treats only explicit true as enabled password auth', () => {
    expect(isPasswordAuthEnabled('true')).toBe(true)
    expect(isPasswordAuthEnabled('false')).toBe(false)
    expect(isPasswordAuthEnabled(undefined)).toBe(false)
  })

  it('auto-starts OIDC only in OIDC-only mode without consent retry', () => {
    expect(shouldAutoStartOidc(false, false)).toBe(true)
    expect(shouldAutoStartOidc(true, false)).toBe(false)
    expect(shouldAutoStartOidc(false, true)).toBe(false)
  })
})
