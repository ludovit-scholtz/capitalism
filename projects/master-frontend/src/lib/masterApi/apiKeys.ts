import { gqlRequest } from '../graphql'

const REQUEST_DISCORD_LINK_CODE_MUTATION = `
  mutation RequestDiscordLinkCode {
    requestDiscordLinkCode {
      code
      expiresAtUtc
      commandPrefix
      discordInviteUrl
    }
  }
`

// ─── API Key Management ──────────────────────────────────────────────────────

export interface ApiKeyInfo {
  id: string
  name: string
  createdAtUtc: string
  lastUsedAtUtc: string | null
  totalCallCount: number
  revokedAtUtc: string | null
  scopes: string[]
  companyIds: string[]
}

export interface GenerateApiKeyPayload {
  apiKey: ApiKeyInfo
  plaintextKey: string
}

export interface ApiKeyAuditLogInfo {
  id: string
  keyId: string
  keyName: string
  playerId: string
  playerEmail: string
  playerDisplayName: string
  operationName: string
  operationType: string
  scopeUsed: string
  wasAllowed: boolean
  denialCode: string | null
  denialReason: string | null
  attemptedObjectId: string | null
  ipAddress: string | null
  sessionContext: string | null
  occurredAtUtc: string
}

export interface AdminApiKeyInfo {
  playerId: string
  playerEmail: string
  playerDisplayName: string
  key: ApiKeyInfo
}

export interface GameCompanySummary {
  id: string
  name: string
}

const MY_API_KEYS_QUERY = `
  query GetMyApiKeys {
    myApiKeys {
      id
      name
      createdAtUtc
      lastUsedAtUtc
      totalCallCount
      revokedAtUtc
      scopes
      companyIds
    }
  }
`

const MY_COMPANIES_QUERY = `
  query GetMyCompaniesForApiKeys {
    myCompanies {
      id
      name
    }
  }
`

const MY_API_KEY_AUDIT_LOG_QUERY = `
  query GetMyApiKeyAuditLog($limit: Int!, $keyId: UUID) {
    myApiKeyAuditLog(limit: $limit, keyId: $keyId) {
      id
      keyId
      keyName
      playerId
      playerEmail
      playerDisplayName
      operationName
      operationType
      scopeUsed
      wasAllowed
      denialCode
      denialReason
      attemptedObjectId
      ipAddress
      sessionContext
      occurredAtUtc
    }
  }
`

const ADMIN_API_KEYS_QUERY = `
  query GetAdminApiKeys($playerEmail: String, $includeRevoked: Boolean!, $limit: Int!) {
    adminApiKeys(playerEmail: $playerEmail, includeRevoked: $includeRevoked, limit: $limit) {
      playerId
      playerEmail
      playerDisplayName
      key {
        id
        name
        createdAtUtc
        lastUsedAtUtc
        totalCallCount
        revokedAtUtc
        scopes
        companyIds
      }
    }
  }
`

const ADMIN_API_KEY_AUDIT_LOG_QUERY = `
  query GetAdminApiKeyAuditLog($playerEmail: String, $keyId: UUID, $limit: Int!) {
    adminApiKeyAuditLog(playerEmail: $playerEmail, keyId: $keyId, limit: $limit) {
      id
      keyId
      keyName
      playerId
      playerEmail
      playerDisplayName
      operationName
      operationType
      scopeUsed
      wasAllowed
      denialCode
      denialReason
      attemptedObjectId
      ipAddress
      sessionContext
      occurredAtUtc
    }
  }
`

const GENERATE_API_KEY_MUTATION = `
  mutation GenerateApiKey($input: GenerateApiKeyInput!) {
    generateApiKey(input: $input) {
      apiKey {
        id
        name
        createdAtUtc
        lastUsedAtUtc
        totalCallCount
        revokedAtUtc
        scopes
        companyIds
      }
      plaintextKey
    }
  }
`

const REVOKE_API_KEY_MUTATION = `
  mutation RevokeApiKey($input: RevokeApiKeyInput!) {
    revokeApiKey(input: $input)
  }
`

const FORCE_REVOKE_API_KEY_MUTATION = `
  mutation ForceRevokeApiKey($keyId: UUID!) {
    forceRevokeApiKey(keyId: $keyId)
  }
`

const REVOKE_ALL_PLAYER_API_KEYS_MUTATION = `
  mutation RevokeAllPlayerApiKeys($playerId: UUID!) {
    revokeAllPlayerApiKeys(playerId: $playerId)
  }
`

/** Returns the caller's active and revoked API keys from the Game API. */
export async function fetchMyApiKeys(gameGqlUrl: string, token: string): Promise<ApiKeyInfo[]> {
  const data = await gqlRequest<{ myApiKeys: ApiKeyInfo[] }>(
    MY_API_KEYS_QUERY,
    undefined,
    token,
    gameGqlUrl,
  )
  return data.myApiKeys
}

export async function fetchMyCompaniesForApiKeys(
  gameGqlUrl: string,
  token: string,
): Promise<GameCompanySummary[]> {
  const data = await gqlRequest<{ myCompanies: GameCompanySummary[] }>(
    MY_COMPANIES_QUERY,
    undefined,
    token,
    gameGqlUrl,
  )
  return data.myCompanies
}

export async function fetchMyApiKeyAuditLog(
  gameGqlUrl: string,
  token: string,
  limit = 50,
  keyId?: string,
): Promise<ApiKeyAuditLogInfo[]> {
  const data = await gqlRequest<{ myApiKeyAuditLog: ApiKeyAuditLogInfo[] }>(
    MY_API_KEY_AUDIT_LOG_QUERY,
    { limit, keyId: keyId ?? null },
    token,
    gameGqlUrl,
  )
  return data.myApiKeyAuditLog
}

/** Generates a new API key for the authenticated player on the Game API. */
export async function generateApiKey(
  gameGqlUrl: string,
  token: string,
  name: string,
  scopes: string[],
  companyIds: string[],
): Promise<GenerateApiKeyPayload> {
  const data = await gqlRequest<{ generateApiKey: GenerateApiKeyPayload }>(
    GENERATE_API_KEY_MUTATION,
    { input: { name, scopes, companyIds } },
    token,
    gameGqlUrl,
  )
  return data.generateApiKey
}

/** Revokes an API key by ID. */
export async function revokeApiKey(
  gameGqlUrl: string,
  token: string,
  keyId: string,
): Promise<void> {
  await gqlRequest<{ revokeApiKey: boolean }>(
    REVOKE_API_KEY_MUTATION,
    { input: { keyId } },
    token,
    gameGqlUrl,
  )
}

export async function fetchAdminApiKeys(
  gameGqlUrl: string,
  token: string,
  playerEmail: string,
  includeRevoked = true,
  limit = 100,
): Promise<AdminApiKeyInfo[]> {
  const data = await gqlRequest<{ adminApiKeys: AdminApiKeyInfo[] }>(
    ADMIN_API_KEYS_QUERY,
    { playerEmail: playerEmail || null, includeRevoked, limit },
    token,
    gameGqlUrl,
  )
  return data.adminApiKeys
}

export async function fetchAdminApiKeyAuditLog(
  gameGqlUrl: string,
  token: string,
  playerEmail: string,
  keyId?: string,
  limit = 100,
): Promise<ApiKeyAuditLogInfo[]> {
  const data = await gqlRequest<{ adminApiKeyAuditLog: ApiKeyAuditLogInfo[] }>(
    ADMIN_API_KEY_AUDIT_LOG_QUERY,
    { playerEmail: playerEmail || null, keyId: keyId ?? null, limit },
    token,
    gameGqlUrl,
  )
  return data.adminApiKeyAuditLog
}

export async function forceRevokeApiKey(
  gameGqlUrl: string,
  token: string,
  keyId: string,
): Promise<void> {
  await gqlRequest<{ forceRevokeApiKey: boolean }>(
    FORCE_REVOKE_API_KEY_MUTATION,
    { keyId },
    token,
    gameGqlUrl,
  )
}

export async function revokeAllPlayerApiKeys(
  gameGqlUrl: string,
  token: string,
  playerId: string,
): Promise<number> {
  const data = await gqlRequest<{ revokeAllPlayerApiKeys: number }>(
    REVOKE_ALL_PLAYER_API_KEYS_MUTATION,
    { playerId },
    token,
    gameGqlUrl,
  )
  return data.revokeAllPlayerApiKeys
}

export interface DiscordLinkCodeInfo {
  code: string
  expiresAtUtc: string
  commandPrefix: string
  discordInviteUrl: string
}

export async function requestDiscordLinkCode(token: string): Promise<DiscordLinkCodeInfo> {
  const data = await gqlRequest<{ requestDiscordLinkCode: DiscordLinkCodeInfo }>(
    REQUEST_DISCORD_LINK_CODE_MUTATION,
    undefined,
    token,
  )
  return data.requestDiscordLinkCode
}
