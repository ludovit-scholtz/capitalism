import { readRuntimeAppConfigValue } from './runtimeConfig'

export interface RuntimeLocationLike {
  hostname: string
  protocol: string
}

const LOCAL_GAME_GRAPHQL_FALLBACK = 'http://localhost:44356/graphql'
const LOCAL_MASTER_GRAPHQL_FALLBACK = 'https://localhost:44364/graphql'
const LOCAL_MASTER_WEB_FALLBACK = 'http://localhost:5174'

function normalizeExplicitUrl(explicitUrl?: string | null) {
  const trimmed = explicitUrl?.trim()
  return trimmed && trimmed.length > 0 ? trimmed : null
}

function getBrowserLocation(): RuntimeLocationLike | undefined {
  if (typeof window === 'undefined' || !window.location) {
    return undefined
  }

  return window.location
}

function isLocalHostname(hostname: string) {
  return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '[::1]'
}

function normalizeProtocol(protocol?: string) {
  return protocol === 'https:' ? 'https:' : 'http:'
}

export function deriveGameGraphqlUrl(location?: RuntimeLocationLike): string | null {
  const currentLocation = location ?? getBrowserLocation()
  if (!currentLocation?.hostname || isLocalHostname(currentLocation.hostname)) {
    return null
  }

  const labels = currentLocation.hostname.split('.').filter((label) => label.length > 0)
  if (labels.length < 3) {
    return null
  }

  const [shardLabel, ...domainParts] = labels
  const protocol = normalizeProtocol(currentLocation.protocol)
  return `${protocol}//${shardLabel}-api.${domainParts.join('.')}/graphql`
}

export function deriveMasterGraphqlUrl(location?: RuntimeLocationLike): string | null {
  const currentLocation = location ?? getBrowserLocation()
  if (!currentLocation?.hostname || isLocalHostname(currentLocation.hostname)) {
    return null
  }

  const labels = currentLocation.hostname.split('.').filter((label) => label.length > 0)
  if (labels.length < 2) {
    return null
  }

  const masterDomain = labels.length >= 3 ? labels.slice(1).join('.') : currentLocation.hostname
  const protocol = normalizeProtocol(currentLocation.protocol)
  return `${protocol}//api.${masterDomain}/graphql`
}

export function deriveMasterWebUrl(location?: RuntimeLocationLike): string | null {
  const currentLocation = location ?? getBrowserLocation()
  if (!currentLocation?.hostname || isLocalHostname(currentLocation.hostname)) {
    return null
  }

  const labels = currentLocation.hostname.split('.').filter((label) => label.length > 0)
  if (labels.length < 2) {
    return null
  }

  const masterDomain = labels.length >= 3 ? labels.slice(1).join('.') : currentLocation.hostname
  const protocol = normalizeProtocol(currentLocation.protocol)
  return `${protocol}//www.${masterDomain}`
}

export function resolveGameGraphqlUrl(
  explicitUrl?: string | null,
  location?: RuntimeLocationLike,
  localFallback = LOCAL_GAME_GRAPHQL_FALLBACK,
) {
  return (
    normalizeExplicitUrl(readRuntimeAppConfigValue('graphqlUrl')) ??
    normalizeExplicitUrl(explicitUrl) ??
    deriveGameGraphqlUrl(location) ??
    localFallback
  )
}

export function resolveMasterGraphqlUrl(
  explicitUrl?: string | null,
  location?: RuntimeLocationLike,
  localFallback = LOCAL_MASTER_GRAPHQL_FALLBACK,
) {
  return (
    normalizeExplicitUrl(readRuntimeAppConfigValue('masterGraphqlUrl')) ??
    normalizeExplicitUrl(explicitUrl) ??
    deriveMasterGraphqlUrl(location) ??
    localFallback
  )
}

export function resolveMasterWebUrl(
  explicitUrl?: string | null,
  location?: RuntimeLocationLike,
  localFallback = LOCAL_MASTER_WEB_FALLBACK,
) {
  return (
    normalizeExplicitUrl(readRuntimeAppConfigValue('masterWebUrl')) ??
    normalizeExplicitUrl(explicitUrl) ??
    deriveMasterWebUrl(location) ??
    localFallback
  )
}

export function resolveApiBaseUrl(graphqlUrl: string) {
  return graphqlUrl.replace(/\/graphql\/?$/, '')
}

export function resolveOptionalMasterApiBaseUrl(
  explicitUrl?: string | null,
  location?: RuntimeLocationLike,
) {
  const explicit = normalizeExplicitUrl(explicitUrl)
  if (explicit) {
    return resolveApiBaseUrl(explicit)
  }

  const derived = deriveMasterGraphqlUrl(location)
  return derived ? resolveApiBaseUrl(derived) : ''
}