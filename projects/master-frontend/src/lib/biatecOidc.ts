import { resolveApiBaseUrl, resolveMasterGraphqlUrl } from '@/lib/runtimeGraphqlUrl'

// Biatec OIDC (PKCE) configuration, helpers and browser storage for the master site sign-in. Pure of store state.

export const EXPIRES_KEY = 'master_auth_expires'
export const AUTH_PROVIDER_KEY = 'master_auth_provider'
export const OIDC_STATE_KEY = 'master_biatec_oidc_state'
export const OIDC_LOGOUT_STATE_KEY = 'master_biatec_oidc_logout_state'
export const AUTH_PROVIDER_LOCAL = 'local'
export const LOCALE_KEY = 'master_locale'
export const AUTH_PROVIDER_BIATEC = 'biatec_oidc'
export const COOKIE_SESSION_SENTINEL = 'cookie-session'
export const BIATEC_OIDC_AUTHORIZE_URL =
  import.meta.env.VITE_BIATEC_OIDC_AUTHORIZE_URL || 'https://google.biatec.io/authorize'
export const BIATEC_OIDC_TOKEN_URL = import.meta.env.VITE_BIATEC_OIDC_TOKEN_URL || 'https://google.biatec.io/token'
export const BIATEC_OIDC_END_SESSION_URL = import.meta.env.VITE_BIATEC_OIDC_END_SESSION_URL || ''
export const BIATEC_OIDC_CLIENT_ID = import.meta.env.VITE_BIATEC_OIDC_CLIENT_ID || 'capitalism-pkce'
export const BIATEC_OIDC_REDIRECT_URI = import.meta.env.VITE_BIATEC_OIDC_REDIRECT_URI
export const BIATEC_OIDC_SCOPE = import.meta.env.VITE_BIATEC_OIDC_SCOPE || 'openid profile email'
export const BIATEC_OIDC_AUDIENCE = import.meta.env.VITE_BIATEC_OIDC_AUDIENCE || BIATEC_OIDC_CLIENT_ID
export const BIATEC_OIDC_ALLOWED_ISSUERS = (
  import.meta.env.VITE_BIATEC_OIDC_ALLOWED_ISSUERS ||
  'https://google.biatec.io,https://google.biatec.io'
)
  .split(',')
  .map((entry) => entry.trim())
  .filter((entry) => entry.length > 0)
export const TOKEN_RENEW_BEFORE_MS = 60 * 1000
export const API_BASE_URL = resolveApiBaseUrl(resolveMasterGraphqlUrl(import.meta.env.VITE_GRAPHQL_URL))

export interface OidcStateRecord {
  state: string
  nonce: string
  redirectPath: string
  codeVerifier: string
}

export interface BiatecCallbackSession {
  token: string
  expiresAtUtc: string
  redirectPath: string
}

export interface BiatecSignInOptions {
  silentPrompt?: boolean
  prompt?: string
}

export function normalizeRedirectPath(redirectPath: string | null | undefined) {
  if (!redirectPath || !redirectPath.startsWith('/')) {
    return '/'
  }

  return redirectPath
}

export function generateOidcRandom(length = 32) {
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'
  const bytes = new Uint8Array(length)
  crypto.getRandomValues(bytes)
  let result = ''
  for (const value of bytes) {
    result += chars[value % chars.length]
  }
  return result
}

export function arrayBufferToBase64Url(buffer: ArrayBuffer) {
  const bytes = new Uint8Array(buffer)
  let binary = ''
  for (const byte of bytes) {
    binary += String.fromCharCode(byte)
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

// PKCE (RFC 7636): code_verifier alphabet [A-Za-z0-9-._~], 43-128 chars;
// generateOidcRandom's alphanumeric alphabet is a valid subset.
export function createPkceCodeVerifier() {
  return generateOidcRandom(64)
}

export async function createPkceCodeChallenge(codeVerifier: string) {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(codeVerifier))
  return arrayBufferToBase64Url(digest)
}

export function createLogoutState() {
  const state = generateOidcRandom(32)
  sessionStorage.setItem(OIDC_LOGOUT_STATE_KEY, state)
  return state
}

export function parseJwtPayload(token: string): Record<string, unknown> {
  const [, payload] = token.split('.')
  if (!payload) {
    throw new Error('Invalid token payload.')
  }

  const normalized = payload.replace(/-/g, '+').replace(/_/g, '/')
  const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=')
  const decoded = atob(padded)
  return JSON.parse(decoded) as Record<string, unknown>
}

export function getConfiguredRedirectUri() {
  if (BIATEC_OIDC_REDIRECT_URI) {
    return BIATEC_OIDC_REDIRECT_URI
  }

  if (typeof window !== 'undefined') {
    return `${window.location.origin}/auth/callback`
  }

  return 'http://localhost:5174/auth/callback'
}

export function getStoredAuthProvider() {
  return localStorage.getItem(AUTH_PROVIDER_KEY) || AUTH_PROVIDER_LOCAL
}

export function setStoredAuthProvider(provider: string) {
  localStorage.setItem(AUTH_PROVIDER_KEY, provider)
}

export function clearStoredOidcState() {
  sessionStorage.removeItem(OIDC_STATE_KEY)
  localStorage.removeItem(OIDC_STATE_KEY)
}

export function resolveBiatecEndSessionEndpoint() {
  if (BIATEC_OIDC_END_SESSION_URL) {
    return BIATEC_OIDC_END_SESSION_URL
  }

  try {
    const authorizeUrl = new URL(BIATEC_OIDC_AUTHORIZE_URL)
    return `${authorizeUrl.origin}/connect/endsession`
  } catch {
    return ''
  }
}

export function getPostLogoutRedirectUri() {
  if (typeof window === 'undefined') {
    return 'http://localhost:5174/login'
  }

  return `${window.location.origin}/login`
}

export function buildBiatecEndSessionUrl(
  idTokenHint: string | null,
  postLogoutRedirectUri = getPostLogoutRedirectUri(),
) {
  if (typeof window === 'undefined') {
    return null
  }

  try {
    const endpoint = resolveBiatecEndSessionEndpoint()
    if (!endpoint) {
      return null
    }

    const logoutUrl = new URL(endpoint)
    const state = createLogoutState()
    logoutUrl.searchParams.set('post_logout_redirect_uri', postLogoutRedirectUri)
    logoutUrl.searchParams.set('client_id', BIATEC_OIDC_CLIENT_ID)
    logoutUrl.searchParams.set('state', state)

    if (idTokenHint) {
      logoutUrl.searchParams.set('id_token_hint', idTokenHint)
    }

    return logoutUrl.toString()
  } catch {
    return null
  }
}

export function getStoredOidcState(): OidcStateRecord | null {
  const raw = sessionStorage.getItem(OIDC_STATE_KEY) || localStorage.getItem(OIDC_STATE_KEY)
  if (!raw) {
    return null
  }

  try {
    const parsed = JSON.parse(raw) as OidcStateRecord
    if (!parsed.state || !parsed.nonce) {
      return null
    }

    return parsed
  } catch {
    return null
  }
}

export function normalizeBiatecSignInOptions(
  options?: boolean | BiatecSignInOptions,
): BiatecSignInOptions {
  if (typeof options === 'boolean') {
    return { silentPrompt: options }
  }

  return options ?? {}
}

export interface BiatecTokenResponse {
  accessToken?: string
  idToken?: string
  expiresIn?: number
  tokenType?: string
}

// Public client (PKCE) token exchange: no client_secret, client authentication
// happens via the code_verifier proving possession of the original request.
export async function exchangeAuthorizationCode(code: string, codeVerifier: string, redirectUri: string) {
  const body = new URLSearchParams()
  body.set('grant_type', 'authorization_code')
  body.set('code', code)
  body.set('redirect_uri', redirectUri)
  body.set('client_id', BIATEC_OIDC_CLIENT_ID)
  body.set('code_verifier', codeVerifier)

  const response = await fetch(BIATEC_OIDC_TOKEN_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: body.toString(),
  })

  if (!response.ok) {
    throw new Error('Failed to exchange the Biatec authorization code for a token.')
  }

  return (await response.json()) as BiatecTokenResponse
}

export async function getBiatecTokenFromCallback(): Promise<BiatecCallbackSession> {
  const url = new URL(window.location.href)
  const query = url.searchParams
  const hash = window.location.hash.startsWith('#')
    ? window.location.hash.slice(1)
    : window.location.hash
  const fragment = new URLSearchParams(hash)

  const readParam = (...keys: string[]) => {
    for (const key of keys) {
      const value = query.get(key) ?? fragment.get(key)
      if (value) {
        return value
      }
    }
    return null
  }

  const oidcError = readParam('error')
  if (oidcError) {
    const description = readParam('error_description')
    throw new Error(description || `OIDC login failed: ${oidcError}`)
  }

  const code = readParam('code')
  const returnedState = readParam('state')
  if (!code || !returnedState) {
    throw new Error('OIDC callback is missing required parameters.')
  }

  const pendingState = getStoredOidcState()
  if (!pendingState || pendingState.state !== returnedState) {
    throw new Error('OIDC state validation failed. Please try signing in again.')
  }

  const tokenResponse = await exchangeAuthorizationCode(code, pendingState.codeVerifier, getConfiguredRedirectUri())
  const tokenValue = tokenResponse.idToken
  if (!tokenValue) {
    throw new Error('No ID token was returned from Biatec authentication.')
  }

  const tokenPayload = parseJwtPayload(tokenValue)
  const nonce = typeof tokenPayload.nonce === 'string' ? tokenPayload.nonce : null
  if (nonce && nonce !== pendingState.nonce) {
    throw new Error('OIDC nonce validation failed. Please try signing in again.')
  }

  const issuer = typeof tokenPayload.iss === 'string' ? tokenPayload.iss : null
  if (!issuer || !BIATEC_OIDC_ALLOWED_ISSUERS.includes(issuer)) {
    throw new Error('OIDC issuer validation failed. Please try signing in again.')
  }

  const audienceClaim = tokenPayload.aud
  const audiences = Array.isArray(audienceClaim)
    ? audienceClaim.filter((entry): entry is string => typeof entry === 'string')
    : typeof audienceClaim === 'string'
      ? [audienceClaim]
      : []
  if (!audiences.includes(BIATEC_OIDC_AUDIENCE)) {
    throw new Error('OIDC audience validation failed. Please try signing in again.')
  }

  const exp = typeof tokenPayload.exp === 'number' ? tokenPayload.exp : null
  const expiresIn = tokenResponse.expiresIn
  const expiresAtUtc = exp
    ? new Date(exp * 1000).toISOString()
    : typeof expiresIn === 'number' && expiresIn > 0
      ? new Date(Date.now() + expiresIn * 1000).toISOString()
      : new Date(Date.now() + 120 * 60 * 1000).toISOString()

  return {
    token: tokenValue,
    expiresAtUtc,
    redirectPath: normalizeRedirectPath(pendingState.redirectPath),
  }
}
