export function isPasswordAuthEnabled(flag: string | undefined) {
  return flag === 'true'
}

export function shouldAutoStartOidc(passwordAuthEnabled: boolean, requiresConsentRetry: boolean) {
  return !passwordAuthEnabled && !requiresConsentRetry
}
