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
  personalAccountName?: string
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
        personalAccountName
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
        personalAccountName
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
      personalAccountName
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

// ── Player gold account ────────────────────────────────────────────────────

export interface PlayerGoldTransactionInfo {
  id: string
  amount: number
  balanceBefore: number
  balanceAfter: number
  note: string | null
  createdAtUtc: string
}

export interface PlayerGoldAccountInfo {
  goldTokenBalance: number
  lastUpdatedAtUtc: string | null
  recentTransactions: PlayerGoldTransactionInfo[]
}

const MY_GOLD_ACCOUNT_QUERY = `
  query GetMyGoldAccount {
    myGoldAccount {
      goldTokenBalance
      lastUpdatedAtUtc
      recentTransactions {
        id
        amount
        balanceBefore
        balanceAfter
        note
        createdAtUtc
      }
    }
  }
`

export async function fetchMyGoldAccount(token: string): Promise<PlayerGoldAccountInfo> {
  const data = await gqlRequest<{ myGoldAccount: PlayerGoldAccountInfo }>(
    MY_GOLD_ACCOUNT_QUERY,
    undefined,
    token,
  )
  return data.myGoldAccount
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
  note: string,
): Promise<GoldTokenBalanceInfo> {
  const data = await gqlRequest<{ adjustGoldTokenBalance: GoldTokenBalanceInfo }>(
    ADJUST_GOLD_TOKEN_MUTATION,
    { input: { targetEmail, amount, note } },
    token,
  )
  return data.adjustGoldTokenBalance
}

export interface SupportTicketAuditEventInfo {
  id: string
  eventType: string
  actorEmail: string
  actorDisplayName: string
  note: string
  metadataJson: string
  createdAtUtc: string
}

export interface SupportTicketInfo {
  id: string
  ticketType: 'SUGGESTION' | 'BUG' | 'OTHER'
  status: 'SUBMITTED' | 'IN_PROGRESS' | 'FINISHED'
  title: string
  markdownSource: string
  sanitizedPreviewHtml: string | null
  containsUnsafeContent: boolean
  moderationState: 'PENDING' | 'APPROVED' | 'REJECTED'
  moderationReason: string | null
  moderatedByEmail: string | null
  moderatedAtUtc: string | null
  createdByEmail: string
  createdByDisplayName: string
  createdAtUtc: string
  updatedAtUtc: string
  statusUpdatedAtUtc: string
  extractedUrls: string[]
  extractedImages: string[]
  activity: SupportTicketAuditEventInfo[]
}

export interface SupportTicketListInput {
  ticketType?: string | null
  status?: string | null
  searchTitle?: string | null
  createdFromUtc?: string | null
  createdToUtc?: string | null
  sortBy?: 'CREATED_AT' | 'UPDATED_AT' | 'TITLE'
  sortDirection?: 'ASC' | 'DESC'
  limit?: number
  offset?: number
  unsafeOnly?: boolean
}

const SUPPORT_FIELDS = `
  id
  ticketType
  status
  title
  markdownSource
  sanitizedPreviewHtml
  containsUnsafeContent
  moderationState
  moderationReason
  moderatedByEmail
  moderatedAtUtc
  createdByEmail
  createdByDisplayName
  createdAtUtc
  updatedAtUtc
  statusUpdatedAtUtc
  extractedUrls
  extractedImages
  activity {
    id
    eventType
    actorEmail
    actorDisplayName
    note
    metadataJson
    createdAtUtc
  }
`

const MY_SUPPORT_TICKETS_QUERY = `
  query MySupportTickets($input: ListSupportTicketsInput) {
    mySupportTickets(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

const SUPPORT_TICKETS_ADMIN_QUERY = `
  query SupportTicketsAdmin($input: ListSupportTicketsInput) {
    supportTicketsAdmin(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

const CREATE_SUPPORT_TICKET_MUTATION = `
  mutation CreateSupportTicket($input: CreateSupportTicketInput!) {
    createSupportTicket(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

const UPDATE_SUPPORT_TICKET_CONTENT_MUTATION = `
  mutation UpdateSupportTicketContent($input: UpdateSupportTicketContentInput!) {
    updateSupportTicketContent(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

const UPDATE_SUPPORT_TICKET_STATUS_MUTATION = `
  mutation UpdateSupportTicketStatus($input: UpdateSupportTicketStatusInput!) {
    updateSupportTicketStatus(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

const MODERATE_SUPPORT_TICKET_MUTATION = `
  mutation ModerateSupportTicket($input: ModerateSupportTicketInput!) {
    moderateSupportTicket(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

export async function fetchMySupportTickets(
  token: string,
  input: SupportTicketListInput = {},
): Promise<SupportTicketInfo[]> {
  const data = await gqlRequest<{ mySupportTickets: SupportTicketInfo[] }>(
    MY_SUPPORT_TICKETS_QUERY,
    { input },
    token,
  )
  return data.mySupportTickets
}

export async function fetchSupportTicketsAdmin(
  token: string,
  input: SupportTicketListInput = {},
): Promise<SupportTicketInfo[]> {
  const data = await gqlRequest<{ supportTicketsAdmin: SupportTicketInfo[] }>(
    SUPPORT_TICKETS_ADMIN_QUERY,
    { input },
    token,
  )
  return data.supportTicketsAdmin
}

export async function createSupportTicket(
  token: string,
  input: { ticketType: string; title: string; markdownSource: string },
): Promise<SupportTicketInfo> {
  const data = await gqlRequest<{ createSupportTicket: SupportTicketInfo }>(
    CREATE_SUPPORT_TICKET_MUTATION,
    { input },
    token,
  )
  return data.createSupportTicket
}

export async function updateSupportTicketContent(
  token: string,
  input: { ticketId: string; title: string; markdownSource: string },
): Promise<SupportTicketInfo> {
  const data = await gqlRequest<{ updateSupportTicketContent: SupportTicketInfo }>(
    UPDATE_SUPPORT_TICKET_CONTENT_MUTATION,
    { input },
    token,
  )
  return data.updateSupportTicketContent
}

export async function updateSupportTicketStatus(
  token: string,
  input: { ticketId: string; status: string; note?: string },
): Promise<SupportTicketInfo> {
  const data = await gqlRequest<{ updateSupportTicketStatus: SupportTicketInfo }>(
    UPDATE_SUPPORT_TICKET_STATUS_MUTATION,
    { input },
    token,
  )
  return data.updateSupportTicketStatus
}

export async function moderateSupportTicket(
  token: string,
  input: { ticketId: string; approve: boolean; note?: string },
): Promise<SupportTicketInfo> {
  const data = await gqlRequest<{ moderateSupportTicket: SupportTicketInfo }>(
    MODERATE_SUPPORT_TICKET_MUTATION,
    { input },
    token,
  )
  return data.moderateSupportTicket
}

// ── Master ranking point system ───────────────────────────────────────────

export interface RankingSummaryInfo {
  totalPoints: number
  globalRank: number
  previousGlobalRank: number
  rankMovement: number
  updatedAtUtc: string
}

export interface RankingLeaderboardEntryInfo {
  playerId: string
  displayName: string
  personalAccountName?: string
  totalPoints: number
  globalRank: number
  rankMovement: number
}

export interface RankingRewardHistoryItem {
  id: string
  bountyCode: string
  bountyDisplayName: string
  pointsAwarded: number
  status: string
  serverKey: string | null
  eventDateUtc: string
  awardedAtUtc: string
  metadataJson: string
}

export interface RankingBountyDashboardItemInfo {
  id: string
  code: string
  displayName: string
  description: string
  rewardPoints: number
  cooldownMode: string
  proofRequirement: string
  requiresModeration: boolean
  awardedToday: boolean
  isAvailableNow: boolean
  nextAvailableAtUtc: string | null
  lastAwardedAtUtc: string | null
  totalAwards: number
}

export interface RankingBountyDefinitionInfo {
  id: string
  code: string
  displayName: string
  description: string
  rewardPoints: number
  isEnabled: boolean
  isVisibleToPlayers: boolean
  requiresModeration: boolean
  cooldownMode: string
  sourceEventType: string
  proofRequirement: string
  visibilityScope: string
  validationSettingsJson: string
  updatedAtUtc: string
}

export interface RankingEventModerationItem {
  id: string
  eventType: string
  playerEmail: string
  serverKey: string | null
  proofReference: string | null
  payloadJson: string
  status: string
  occurredAtUtc: string
  createdAtUtc: string
}

export interface RankingRunInfo {
  id: string
  runType: string
  status: string
  startedAtUtc: string
  finishedAtUtc: string
  processedEvents: number
  rewardRecordsCreated: number
  totalPointsAwarded: number
  totalPointsBeforeDecay: number
  totalPointsAfterDecay: number
  notes: string
}

export interface RankingAdminDashboardInfo {
  bounties: RankingBountyDefinitionInfo[]
  pendingModerationEvents: RankingEventModerationItem[]
  recentRuns: RankingRunInfo[]
}

export interface RankingHistoryFilterInput {
  bountyCode?: string | null
  serverKey?: string | null
  status?: string | null
  fromUtc?: string | null
  toUtc?: string | null
  limit?: number
  offset?: number
}

const MY_RANKING_SUMMARY_QUERY = `
  query MyRankingSummary {
    myRankingSummary {
      totalPoints
      globalRank
      previousGlobalRank
      rankMovement
      updatedAtUtc
    }
  }
`

const RANKING_LEADERBOARD_QUERY = `
  query RankingLeaderboard($limit: Int!, $offset: Int!) {
    rankingLeaderboard(limit: $limit, offset: $offset) {
      playerId
      displayName
      personalAccountName
      totalPoints
      globalRank
      rankMovement
    }
  }
`

const MY_RANKING_BOUNTY_HISTORY_QUERY = `
  query MyRankingBountyHistory($input: RankingHistoryFilterInput) {
    myRankingBountyHistory(input: $input) {
      id
      bountyCode
      bountyDisplayName
      pointsAwarded
      status
      serverKey
      eventDateUtc
      awardedAtUtc
      metadataJson
    }
  }
`

const MY_RANKING_BOUNTY_DASHBOARD_QUERY = `
  query MyRankingBountyDashboard {
    myRankingBountyDashboard {
      id
      code
      displayName
      description
      rewardPoints
      cooldownMode
      proofRequirement
      requiresModeration
      awardedToday
      isAvailableNow
      nextAvailableAtUtc
      lastAwardedAtUtc
      totalAwards
    }
  }
`

const RANKING_ADMIN_DASHBOARD_QUERY = `
  query RankingAdminDashboard {
    rankingAdminDashboard {
      bounties {
        id
        code
        displayName
        description
        rewardPoints
        isEnabled
        isVisibleToPlayers
        requiresModeration
        cooldownMode
        sourceEventType
        proofRequirement
        visibilityScope
        validationSettingsJson
        updatedAtUtc
      }
      pendingModerationEvents {
        id
        eventType
        playerEmail
        serverKey
        proofReference
        payloadJson
        status
        occurredAtUtc
        createdAtUtc
      }
      recentRuns {
        id
        runType
        status
        startedAtUtc
        finishedAtUtc
        processedEvents
        rewardRecordsCreated
        totalPointsAwarded
        totalPointsBeforeDecay
        totalPointsAfterDecay
        notes
      }
    }
  }
`

const SUBMIT_RANKING_PROOF_EVENT_MUTATION = `
  mutation SubmitRankingProofEvent($bountyCode: String!, $proofReference: String!, $uniqueScopeKey: String) {
    submitRankingProofEvent(
      bountyCode: $bountyCode
      proofReference: $proofReference
      uniqueScopeKey: $uniqueScopeKey
    ) {
      id
      eventType
      playerEmail
      status
      createdAtUtc
    }
  }
`

const MODERATE_RANKING_EVENT_MUTATION = `
  mutation ModerateRankingEvent($input: ModerateRankingEventInput!) {
    moderateRankingEvent(input: $input) {
      id
      eventType
      playerEmail
      serverKey
      proofReference
      payloadJson
      status
      occurredAtUtc
      createdAtUtc
    }
  }
`

const UPSERT_RANKING_BOUNTY_DEFINITION_MUTATION = `
  mutation UpsertRankingBountyDefinition($input: UpsertRankingBountyDefinitionInput!) {
    upsertRankingBountyDefinition(input: $input) {
      id
      code
      displayName
      description
      rewardPoints
      isEnabled
      isVisibleToPlayers
      requiresModeration
      cooldownMode
      sourceEventType
      proofRequirement
      visibilityScope
      validationSettingsJson
      updatedAtUtc
    }
  }
`

const RUN_RANKING_EVALUATION_NOW_MUTATION = `
  mutation RunRankingEvaluationNow {
    runRankingEvaluationNow {
      id
      runType
      status
      startedAtUtc
      finishedAtUtc
      processedEvents
      rewardRecordsCreated
      totalPointsAwarded
      totalPointsBeforeDecay
      totalPointsAfterDecay
      notes
    }
  }
`

const RUN_RANKING_DAILY_DECAY_NOW_MUTATION = `
  mutation RunRankingDailyDecayNow {
    runRankingDailyDecayNow {
      id
      runType
      status
      startedAtUtc
      finishedAtUtc
      processedEvents
      rewardRecordsCreated
      totalPointsAwarded
      totalPointsBeforeDecay
      totalPointsAfterDecay
      notes
    }
  }
`

const PROBE_GAME_ADMIN_ACCESS_QUERY = `
  query ProbeGameAdminAccess {
    canAccessRankingAdminDashboard
  }
`

export async function fetchMyRankingSummary(token: string): Promise<RankingSummaryInfo> {
  const data = await gqlRequest<{ myRankingSummary: RankingSummaryInfo }>(
    MY_RANKING_SUMMARY_QUERY,
    undefined,
    token,
  )
  return data.myRankingSummary
}

export async function fetchRankingLeaderboard(
  limit = 100,
  offset = 0,
): Promise<RankingLeaderboardEntryInfo[]> {
  const data = await gqlRequest<{ rankingLeaderboard: RankingLeaderboardEntryInfo[] }>(
    RANKING_LEADERBOARD_QUERY,
    { limit, offset },
  )
  return data.rankingLeaderboard
}

export async function fetchMyRankingBountyHistory(
  token: string,
  input: RankingHistoryFilterInput = {},
): Promise<RankingRewardHistoryItem[]> {
  const data = await gqlRequest<{ myRankingBountyHistory: RankingRewardHistoryItem[] }>(
    MY_RANKING_BOUNTY_HISTORY_QUERY,
    { input },
    token,
  )
  return data.myRankingBountyHistory
}

export async function fetchMyRankingBountyDashboard(
  token: string,
): Promise<RankingBountyDashboardItemInfo[]> {
  const data = await gqlRequest<{ myRankingBountyDashboard: RankingBountyDashboardItemInfo[] }>(
    MY_RANKING_BOUNTY_DASHBOARD_QUERY,
    undefined,
    token,
  )
  return data.myRankingBountyDashboard
}

export async function fetchRankingAdminDashboard(
  token: string,
): Promise<RankingAdminDashboardInfo> {
  const data = await gqlRequest<{ rankingAdminDashboard: RankingAdminDashboardInfo }>(
    RANKING_ADMIN_DASHBOARD_QUERY,
    undefined,
    token,
  )
  return data.rankingAdminDashboard
}

export async function submitRankingProofEvent(
  token: string,
  bountyCode: string,
  proofReference: string,
  uniqueScopeKey?: string,
): Promise<RankingEventModerationItem> {
  const data = await gqlRequest<{ submitRankingProofEvent: RankingEventModerationItem }>(
    SUBMIT_RANKING_PROOF_EVENT_MUTATION,
    { bountyCode, proofReference, uniqueScopeKey: uniqueScopeKey ?? null },
    token,
  )
  return data.submitRankingProofEvent
}

export async function moderateRankingEvent(
  token: string,
  input: { eventId: string; approve: boolean; reason?: string },
): Promise<RankingEventModerationItem> {
  const data = await gqlRequest<{ moderateRankingEvent: RankingEventModerationItem }>(
    MODERATE_RANKING_EVENT_MUTATION,
    { input },
    token,
  )
  return data.moderateRankingEvent
}

export async function upsertRankingBountyDefinition(
  token: string,
  input: Partial<RankingBountyDefinitionInfo> & {
    code: string
    displayName: string
    description: string
    rewardPoints: number
    isEnabled: boolean
    isVisibleToPlayers: boolean
    requiresModeration: boolean
    cooldownMode: string
    sourceEventType: string
    proofRequirement: string
    visibilityScope: string
    validationSettingsJson: string
  },
): Promise<RankingBountyDefinitionInfo> {
  const data = await gqlRequest<{ upsertRankingBountyDefinition: RankingBountyDefinitionInfo }>(
    UPSERT_RANKING_BOUNTY_DEFINITION_MUTATION,
    { input },
    token,
  )
  return data.upsertRankingBountyDefinition
}

export async function runRankingEvaluationNow(token: string): Promise<RankingRunInfo> {
  const data = await gqlRequest<{ runRankingEvaluationNow: RankingRunInfo }>(
    RUN_RANKING_EVALUATION_NOW_MUTATION,
    undefined,
    token,
  )
  return data.runRankingEvaluationNow
}

export async function runRankingDailyDecayNow(token: string): Promise<RankingRunInfo> {
  const data = await gqlRequest<{ runRankingDailyDecayNow: RankingRunInfo }>(
    RUN_RANKING_DAILY_DECAY_NOW_MUTATION,
    undefined,
    token,
  )
  return data.runRankingDailyDecayNow
}

export async function probeGameAdminAccess(token: string): Promise<boolean> {
  try {
    const data = await gqlRequest<{ canAccessRankingAdminDashboard: boolean }>(
      PROBE_GAME_ADMIN_ACCESS_QUERY,
      undefined,
      token,
    )
    return data.canAccessRankingAdminDashboard
  } catch {
    return false
  }
}
