import { afterEach, describe, expect, it, vi } from 'vitest'
import { PasswordResetError, requestPasswordReset, resetPassword } from '../passwordReset'

describe('password reset API helpers', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('returns neutral forgot-password message on success', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        ok: true,
        json: async () => ({ message: 'If an account exists, a reset link has been sent.' }),
      })),
    )

    await expect(requestPasswordReset('player@example.com')).resolves.toBe(
      'If an account exists, a reset link has been sent.',
    )
  })

  it('throws structured PasswordResetError on failure', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        ok: false,
        json: async () => ({ message: 'disabled', code: 'METHOD_NOT_ALLOWED' }),
      })),
    )

    await expect(resetPassword('token', 'Password123!')).rejects.toEqual(
      expect.objectContaining<Partial<PasswordResetError>>({
        code: 'METHOD_NOT_ALLOWED',
        message: 'disabled',
      }),
    )
  })
})
