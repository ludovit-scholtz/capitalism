const MASTER_GRAPHQL_URL =
  import.meta.env.VITE_MASTER_GRAPHQL_URL || 'https://localhost:44364/graphql'

const PASSWORD_RESET_BASE_URL = MASTER_GRAPHQL_URL.replace(/\/graphql\/?$/i, '')

export class PasswordResetError extends Error {
  constructor(
    message: string,
    public readonly code?: string,
  ) {
    super(message)
    this.name = 'PasswordResetError'
  }
}

interface ForgotPasswordResponse {
  message?: string
  code?: string
}

interface ResetPasswordResponse {
  message?: string
  code?: string
}

async function postJson<T>(path: string, payload: Record<string, unknown>): Promise<T> {
  const response = await fetch(`${PASSWORD_RESET_BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })

  const json = (await response.json()) as T

  if (!response.ok) {
    const error = json as { message?: string; code?: string }
    throw new PasswordResetError(error.message || 'Password reset request failed.', error.code)
  }

  return json
}

export async function requestPasswordReset(email: string): Promise<string> {
  const response = await postJson<ForgotPasswordResponse>('/auth/forgot-password', { email })
  return response.message || 'If an account exists, a reset link has been sent.'
}

export async function resetPassword(token: string, newPassword: string): Promise<string> {
  const response = await postJson<ResetPasswordResponse>('/auth/reset-password', {
    token,
    newPassword,
  })
  return response.message || 'Password has been reset successfully.'
}
