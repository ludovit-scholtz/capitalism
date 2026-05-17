export interface RuntimeAppConfig {
  graphqlUrl?: string | null
  masterGraphqlUrl?: string | null
  masterWebUrl?: string | null
  biatecOidcAuthorizeUrl?: string | null
  biatecOidcEndSessionUrl?: string | null
  biatecOidcClientId?: string | null
  biatecOidcRedirectUri?: string | null
  biatecOidcScope?: string | null
  biatecOidcAudience?: string | null
  biatecOidcAllowedIssuers?: string | null
}

declare global {
  interface Window {
    __capitalismRuntimeConfig__?: RuntimeAppConfig
  }
}

export function readRuntimeAppConfig(): RuntimeAppConfig | null {
  if (typeof window === 'undefined') {
    return null
  }

  return window.__capitalismRuntimeConfig__ ?? null
}

export function readRuntimeAppConfigValue(key: keyof RuntimeAppConfig): string | null {
  const value = readRuntimeAppConfig()?.[key]
  if (typeof value !== 'string') {
    return null
  }

  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : null
}