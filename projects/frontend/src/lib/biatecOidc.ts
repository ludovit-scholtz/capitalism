import { readRuntimeAppConfigValue } from '@/lib/runtimeConfig'
import { resolveApiBaseUrl, resolveGameGraphqlUrl, resolveOptionalMasterApiBaseUrl } from '@/lib/runtimeGraphqlUrl'
import { resolvePostLogoutRedirectUri } from '@/lib/authLogout'

// OIDC (PKCE) helpers, runtime configuration and browser session storage for the game frontend sign-in. Pure of store state.

export const BIATEC_OIDC_AUTHORIZE_URL =
  readRuntimeAppConfigValue('biatecOidcAuthorizeUrl') || import.meta.env.VITE_BIATEC_OIDC_AUTHORIZE_URL || 'https://google.biatec.io/authorize'
export const BIATEC_OIDC_TOKEN_URL =
  readRuntimeAppConfigValue('biatecOidcTokenUrl') || import.meta.env.VITE_BIATEC_OIDC_TOKEN_URL || 'https://google.biatec.io/token'
export const BIATEC_OIDC_END_SESSION_URL =
  readRuntimeAppConfigValue('biatecOidcEndSessionUrl') || import.meta.env.VITE_BIATEC_OIDC_END_SESSION_URL || ''
export const BIATEC_OIDC_CLIENT_ID =
  readRuntimeAppConfigValue('biatecOidcClientId') || import.meta.env.VITE_BIATEC_OIDC_CLIENT_ID || 'capitalism-pkce'
export const BIATEC_OIDC_REDIRECT_URI =
  readRuntimeAppConfigValue('biatecOidcRedirectUri') || import.meta.env.VITE_BIATEC_OIDC_REDIRECT_URI
export const BIATEC_OIDC_SCOPE =
  readRuntimeAppConfigValue('biatecOidcScope') || import.meta.env.VITE_BIATEC_OIDC_SCOPE || 'openid profile email'
export const BIATEC_OIDC_AUDIENCE =
  readRuntimeAppConfigValue('biatecOidcAudience') || import.meta.env.VITE_BIATEC_OIDC_AUDIENCE || BIATEC_OIDC_CLIENT_ID
export const BIATEC_OIDC_ALLOWED_ISSUERS = (readRuntimeAppConfigValue('biatecOidcAllowedIssuers') || import.meta.env.VITE_BIATEC_OIDC_ALLOWED_ISSUERS || 'https://google.biatec.io,https://google.biatec.io')
  .split(',')
  .map((entry: string) => entry.trim())
  .filter((entry: string) => entry.length > 0)
export const TOKEN_RENEW_BEFORE_MS = 60 * 1000
export const OIDC_STATE_KEY = 'biatec_oidc_state'
export const OIDC_LOGOUT_STATE_KEY = 'biatec_oidc_logout_state'
export const SIGNED_OUT_NOTICE_KEY = 'auth_signed_out_notice'
export const AUTH_PROVIDER_KEY = 'auth_provider'
export const AUTH_PROVIDER_LOCAL = 'local'
export const AUTH_PROVIDER_BIATEC = 'biatec_oidc'
export const COOKIE_SESSION_SENTINEL = 'cookie-session'
export const API_BASE_URL = resolveApiBaseUrl(resolveGameGraphqlUrl(import.meta.env.VITE_GRAPHQL_URL))
export const MASTER_SESSION_API_BASE_URL = resolveOptionalMasterApiBaseUrl(import.meta.env.VITE_MASTER_GRAPHQL_URL)

export interface OidcPendingState {
  state: string
  nonce: string
  redirectPath: string
  codeVerifier: string
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

export function getConfiguredRedirectUri() {
  if (BIATEC_OIDC_REDIRECT_URI) {
    return BIATEC_OIDC_REDIRECT_URI
  }

  if (typeof window !== 'undefined') {
    return `${window.location.origin}/auth/callback`
  }

  return 'http://localhost:5173/auth/callback'
}

export function getStoredAuthProvider() {
  if (typeof localStorage === 'undefined') {
    return AUTH_PROVIDER_LOCAL
  }

  return localStorage.getItem(AUTH_PROVIDER_KEY) || AUTH_PROVIDER_LOCAL
}

export function setStoredAuthProvider(provider: string) {
  if (typeof localStorage === 'undefined') {
    return
  }

  localStorage.setItem(AUTH_PROVIDER_KEY, provider)
}

export function normalizeBiatecSignInOptions(options?: boolean | BiatecSignInOptions): BiatecSignInOptions {
  if (typeof options === 'boolean') {
    return { silentPrompt: options }
  }

  return options ?? {}
}

export function createRandomBase64Url(bytes = 24) {
  const random = new Uint8Array(bytes)
  crypto.getRandomValues(random)
  return btoa(String.fromCharCode(...random))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '')
}

export function arrayBufferToBase64Url(buffer: ArrayBuffer) {
  const bytes = new Uint8Array(buffer)
  let binary = ''
  for (const byte of bytes) {
    binary += String.fromCharCode(byte)
  }
  return btoa(binary)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '')
}

// PKCE (RFC 7636): the code_verifier must be 43-128 chars from the unreserved
// set [A-Za-z0-9-._~]; createRandomBase64Url's alphabet is a subset of that.
export function createPkceCodeVerifier() {
  return createRandomBase64Url(64)
}

export async function createPkceCodeChallenge(codeVerifier: string) {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(codeVerifier))
  return arrayBufferToBase64Url(digest)
}

export function getPendingOidcState() {
  if (typeof sessionStorage === 'undefined') {
    return null
  }

  const raw = sessionStorage.getItem(OIDC_STATE_KEY)
  if (!raw) {
    return null
  }

  try {
    const parsed = JSON.parse(raw) as OidcPendingState
    if (!parsed.state || !parsed.nonce || !parsed.redirectPath) {
      return null
    }
    return parsed
  } catch {
    return null
  }
}

export function clearPendingOidcState() {
  if (typeof sessionStorage !== 'undefined') {
    sessionStorage.removeItem(OIDC_STATE_KEY)
  }
}

export function createLogoutState() {
  const state = createRandomBase64Url()
  if (typeof sessionStorage !== 'undefined') {
    sessionStorage.setItem(OIDC_LOGOUT_STATE_KEY, state)
  }
  return state
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

export function parseJwtPayload(jwt: string): Record<string, unknown> {
  const parts = jwt.split('.')
  if (parts.length !== 3 || !parts[1]) {
    throw new Error('Invalid OIDC token format.')
  }

  const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')
  const decoded = atob(padded)
  return JSON.parse(decoded) as Record<string, unknown>
}

export function getAuthorizationCodeFromCallback() {
  const query = new URLSearchParams(window.location.search)
  const hash = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash
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

  const errorCode = readParam('error')
  if (errorCode) {
    const description = readParam('error_description')
    throw new Error(description || `OIDC login failed: ${errorCode}`)
  }

  const callbackState = readParam('state')
  const pendingState = getPendingOidcState()
  if (!pendingState || !callbackState || callbackState !== pendingState.state) {
    throw new Error('OIDC state validation failed. Please try signing in again.')
  }

  const code = readParam('code')
  if (!code) {
    throw new Error('No authorization code was returned from Biatec authentication.')
  }

  return { code, pendingState }
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

export function validateBiatecIdToken(idToken: string, pendingState: OidcPendingState) {
  const tokenPayload = parseJwtPayload(idToken)
  const nonce = typeof tokenPayload.nonce === 'string' ? tokenPayload.nonce : null
  if (nonce && nonce !== pendingState.nonce) {
    throw new Error('OIDC nonce validation failed. Please try signing in again.')
  }

  const issuer = typeof tokenPayload.iss === 'string' ? tokenPayload.iss : null
  if (!issuer || !BIATEC_OIDC_ALLOWED_ISSUERS.includes(issuer)) {
    throw new Error('OIDC issuer validation failed. Please try signing in again.')
  }

  const audienceClaim = tokenPayload.aud
  const audiences = Array.isArray(audienceClaim) ? audienceClaim.filter((entry): entry is string => typeof entry === 'string') : typeof audienceClaim === 'string' ? [audienceClaim] : []
  if (!audiences.includes(BIATEC_OIDC_AUDIENCE)) {
    throw new Error('OIDC audience validation failed. Please try signing in again.')
  }

  return tokenPayload
}

export function resolveTokenExpiryUtc(tokenPayload: Record<string, unknown>, expiresIn?: number) {
  const exp = typeof tokenPayload.exp === 'number' ? tokenPayload.exp : null
  return exp
    ? new Date(exp * 1000).toISOString()
    : typeof expiresIn === 'number' && expiresIn > 0
      ? new Date(Date.now() + expiresIn * 1000).toISOString()
      : new Date(Date.now() + 120 * 60 * 1000).toISOString()
}

export async function getTokenFromCallback() {
  const { code, pendingState } = getAuthorizationCodeFromCallback()
  const tokenResponse = await exchangeAuthorizationCode(code, pendingState.codeVerifier, getConfiguredRedirectUri())

  const idToken = tokenResponse.idToken
  if (!idToken) {
    throw new Error('No ID token was returned from Biatec authentication.')
  }

  const tokenPayload = validateBiatecIdToken(idToken, pendingState)
  const expiresAtUtc = resolveTokenExpiryUtc(tokenPayload, tokenResponse.expiresIn)

  return {
    token: idToken,
    expiresAtUtc,
    redirectPath: pendingState.redirectPath,
  }
}

export function clearStoredSession() {
  if (typeof localStorage !== 'undefined') {
    localStorage.removeItem('auth_token')
    localStorage.removeItem('auth_expires')
    localStorage.removeItem(AUTH_PROVIDER_KEY)
  }
}

export function getPostLogoutRedirectUri() {
  if (typeof window === 'undefined') {
    return resolvePostLogoutRedirectUri(undefined)
  }

  return resolvePostLogoutRedirectUri(window.location.origin)
}

export function buildBiatecEndSessionUrl(idTokenHint: string | null, postLogoutRedirectUri = getPostLogoutRedirectUri()) {
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

// Raw JWTs are never persisted in localStorage. Older builds stored
// `auth_token`/`auth_expires`; proactively purge any such legacy values so a
// previously-leaked token cannot be picked up after upgrading. Returns true
// when a legacy session token was present so the session can be rehydrated
// from the secure cookie instead.
export function purgeLegacyTokenStorage() {
  if (typeof localStorage === 'undefined') {
    return false
  }
  const hadLegacyToken = localStorage.getItem('auth_token') !== null
  localStorage.removeItem('auth_token')
  localStorage.removeItem('auth_expires')
  return hadLegacyToken
}

// A non-sensitive provider marker records that an authenticated session was
// established. It lets us optimistically rehydrate from the secure cookie
// session on reload without exposing the bearer token.
export function hasPersistedSessionMarker() {
  if (typeof localStorage === 'undefined') {
    return false
  }
  return localStorage.getItem(AUTH_PROVIDER_KEY) !== null
}

export function getStoredCityId() {
  if (typeof localStorage === 'undefined') {
    return null
  }
  return localStorage.getItem('selected_city_id')
}

export function setStoredCityId(cityId: string | null) {
  if (typeof localStorage === 'undefined') {
    return
  }
  if (cityId) {
    localStorage.setItem('selected_city_id', cityId)
  } else {
    localStorage.removeItem('selected_city_id')
  }
}
