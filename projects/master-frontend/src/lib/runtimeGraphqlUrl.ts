export interface RuntimeLocationLike {
  hostname: string
  protocol: string
}

const LOCAL_MASTER_GRAPHQL_FALLBACK = 'http://localhost:44364/graphql'

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
  const protocol = currentLocation.protocol === 'https:' ? 'https:' : 'http:'
  return `${protocol}//api.${masterDomain}/graphql`
}

export function resolveMasterGraphqlUrl(
  explicitUrl?: string | null,
  location?: RuntimeLocationLike,
  localFallback = LOCAL_MASTER_GRAPHQL_FALLBACK,
) {
  return normalizeExplicitUrl(explicitUrl) ?? deriveMasterGraphqlUrl(location) ?? localFallback
}

export function resolveApiBaseUrl(graphqlUrl: string) {
  return graphqlUrl.replace(/\/graphql\/?$/, '')
}