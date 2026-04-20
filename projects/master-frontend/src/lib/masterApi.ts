import { gqlRequest } from './graphql'

export interface GameServerSummary {
  id: string
  serverKey: string
  displayName: string
  description: string
  region: string
  environment: string
  backendUrl: string
  graphqlUrl: string
  frontendUrl: string
  version: string
  playerCount: number
  companyCount: number
  currentTick: number
  registeredAtUtc: string
  lastHeartbeatAtUtc: string
  isOnline: boolean
}

export interface MasterPlayerProfile {
  id: string
  email: string
  displayName: string
  createdAtUtc: string
  startupPackClaimedAtUtc: string | null
  canClaimStartupPack: boolean
}

export interface MasterAuthPayload {
  token: string
  expiresAtUtc: string
  player: MasterPlayerProfile
}

export interface SubscriptionInfo {
  tier: 'FREE' | 'PRO'
  status: 'NONE' | 'ACTIVE' | 'EXPIRED'
  isActive: boolean
  daysRemaining: number | null
  canProlong: boolean
  expiresAtUtc: string | null
  startsAtUtc: string | null
}

interface GameServersPayload {
  gameServers: GameServerSummary[]
}

const GAME_SERVERS_QUERY = `
  query GetGameServers {
    gameServers {
      id
      serverKey
      displayName
      description
      region
      environment
      backendUrl
      graphqlUrl
      frontendUrl
      version
      playerCount
      companyCount
      currentTick
      registeredAtUtc
      lastHeartbeatAtUtc
      isOnline
    }
  }
`

const REGISTER_MUTATION = `
  mutation Register($input: RegisterInput!) {
    register(input: $input) {
      token
      expiresAtUtc
      player {
        id
        email
        displayName
        createdAtUtc
        startupPackClaimedAtUtc
        canClaimStartupPack
      }
    }
  }
`

const LOGIN_MUTATION = `
  mutation Login($input: LoginInput!) {
    login(input: $input) {
      token
      expiresAtUtc
      player {
        id
        email
        displayName
        createdAtUtc
        startupPackClaimedAtUtc
        canClaimStartupPack
      }
    }
  }
`

const ME_QUERY = `
  query {
    me {
      id
      email
      displayName
      createdAtUtc
      startupPackClaimedAtUtc
      canClaimStartupPack
    }
  }
`

const MY_SUBSCRIPTION_QUERY = `
  query {
    mySubscription {
      tier
      status
      isActive
      daysRemaining
      canProlong
      expiresAtUtc
      startsAtUtc
    }
  }
`

const PROLONG_SUBSCRIPTION_MUTATION = `
  mutation ProlongSubscription($input: ProlongSubscriptionInput!) {
    prolongSubscription(input: $input) {
      tier
      status
      isActive
      daysRemaining
      canProlong
      expiresAtUtc
      startsAtUtc
    }
  }
`

const CLAIM_STARTUP_PACK_MUTATION = `
  mutation ClaimStartupPack {
    claimStartupPack {
      tier
      status
      isActive
      daysRemaining
      canProlong
      expiresAtUtc
      startsAtUtc
    }
  }
`

export async function fetchGameServers(): Promise<GameServerSummary[]> {
  const data = await gqlRequest<GameServersPayload>(GAME_SERVERS_QUERY)
  return data.gameServers
}

export async function registerAccount(
  email: string,
  displayName: string,
  password: string,
): Promise<MasterAuthPayload> {
  const data = await gqlRequest<{ register: MasterAuthPayload }>(REGISTER_MUTATION, {
    input: { email, displayName, password },
  })
  return data.register
}

export async function loginAccount(email: string, password: string): Promise<MasterAuthPayload> {
  const data = await gqlRequest<{ login: MasterAuthPayload }>(LOGIN_MUTATION, {
    input: { email, password },
  })
  return data.login
}

export async function fetchMe(token: string): Promise<MasterPlayerProfile> {
  const data = await gqlRequest<{ me: MasterPlayerProfile }>(ME_QUERY, undefined, token)
  return data.me
}

export async function fetchMySubscription(token: string): Promise<SubscriptionInfo> {
  const data = await gqlRequest<{ mySubscription: SubscriptionInfo }>(
    MY_SUBSCRIPTION_QUERY,
    undefined,
    token,
  )
  return data.mySubscription
}

export async function prolongSubscription(
  token: string,
  months: number,
): Promise<SubscriptionInfo> {
  const data = await gqlRequest<{ prolongSubscription: SubscriptionInfo }>(
    PROLONG_SUBSCRIPTION_MUTATION,
    { input: { months } },
    token,
  )
  return data.prolongSubscription
}

export async function claimStartupPack(token: string): Promise<SubscriptionInfo> {
  const data = await gqlRequest<{ claimStartupPack: SubscriptionInfo }>(
    CLAIM_STARTUP_PACK_MUTATION,
    undefined,
    token,
  )
  return data.claimStartupPack
}

// ── Gold token administration ──────────────────────────────────────────────

export interface GoldTokenBalanceInfo {
  playerId: string
  email: string
  displayName: string
  goldTokenBalance: number
}

export interface GoldTokenTransactionInfo {
  id: string
  playerEmail: string
  amount: number
  balanceBefore: number
  balanceAfter: number
  adminEmail: string
  note: string | null
  createdAtUtc: string
}

const GOLD_TOKEN_BALANCES_QUERY = `
  query GetGoldTokenBalances {
    goldTokenBalances {
      playerId
      email
      displayName
      goldTokenBalance
    }
  }
`

const GOLD_TOKEN_TRANSACTIONS_QUERY = `
  query GetGoldTokenTransactions($targetEmail: String, $limit: Int) {
    goldTokenTransactions(targetEmail: $targetEmail, limit: $limit) {
      id
      playerEmail
      amount
      balanceBefore
      balanceAfter
      adminEmail
      note
      createdAtUtc
    }
  }
`

const ADJUST_GOLD_TOKEN_MUTATION = `
  mutation AdjustGoldToken($input: AdjustGoldTokenInput!) {
    adjustGoldTokenBalance(input: $input) {
      playerId
      email
      displayName
      goldTokenBalance
    }
  }
`

export async function fetchGoldTokenBalances(token: string): Promise<GoldTokenBalanceInfo[]> {
  const data = await gqlRequest<{ goldTokenBalances: GoldTokenBalanceInfo[] }>(
    GOLD_TOKEN_BALANCES_QUERY,
    undefined,
    token,
  )
  return data.goldTokenBalances
}

export async function fetchGoldTokenTransactions(
  token: string,
  targetEmail?: string,
  limit = 50,
): Promise<GoldTokenTransactionInfo[]> {
  const data = await gqlRequest<{ goldTokenTransactions: GoldTokenTransactionInfo[] }>(
    GOLD_TOKEN_TRANSACTIONS_QUERY,
    { targetEmail: targetEmail ?? null, limit },
    token,
  )
  return data.goldTokenTransactions
}

export async function adjustGoldTokenBalance(
  token: string,
  targetEmail: string,
  amount: number,
  note?: string,
): Promise<GoldTokenBalanceInfo> {
  const data = await gqlRequest<{ adjustGoldTokenBalance: GoldTokenBalanceInfo }>(
    ADJUST_GOLD_TOKEN_MUTATION,
    { input: { targetEmail, amount, note: note ?? null } },
    token,
  )
  return data.adjustGoldTokenBalance
}
