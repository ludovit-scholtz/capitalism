/**
 * Returns true when password-based login/register UI is enabled by environment flag.
 */
export function isPasswordAuthEnabled(flag: string | undefined) {
  return flag === 'true'
}

/**
 * Returns true when the login page should auto-start OIDC sign-in.
 */
export function shouldAutoStartOidc(passwordAuthEnabled: boolean, requiresConsentRetry: boolean) {
  return !passwordAuthEnabled && !requiresConsentRetry
}
