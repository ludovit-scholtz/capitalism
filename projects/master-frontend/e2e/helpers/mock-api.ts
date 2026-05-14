import type { Page } from '@playwright/test'

export interface MockGameServer {
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
  isActive?: boolean
  expiresAtUtc?: string
  keyStatus?: string
}

export interface MockSubscription {
  tier: 'FREE' | 'PRO'
  status: 'NONE' | 'ACTIVE' | 'EXPIRED'
  isActive: boolean
  daysRemaining: number | null
  canProlong: boolean
  expiresAtUtc: string | null
  startsAtUtc: string | null
}

export interface MockPlayer {
  id: string
  email: string
  displayName: string
  personalAccountName?: string
  gender?: 'MALE' | 'FEMALE' | 'UNSPECIFIED'
  createdAtUtc: string
  startupPackClaimedAtUtc: string | null
  canClaimStartupPack: boolean
}

export interface MockGameCompany {
  id: string
  name: string
}

export interface MockApiKey {
  id: string
  name: string
  createdAtUtc: string
  lastUsedAtUtc: string | null
  totalCallCount: number
  revokedAtUtc: string | null
  scopes: string[]
  companyIds: string[]
  playerId: string
  playerEmail: string
  playerDisplayName: string
}

export interface MockApiKeyAuditLog {
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

export interface MockGoldBalance {
  playerId: string
  email: string
  displayName: string
  goldTokenBalance: number
}

export interface MockGoldTransaction {
  id: string
  playerEmail: string
  amount: number
  balanceBefore: number
  balanceAfter: number
  adminEmail: string
  note: string | null
  createdAtUtc: string
}

export interface MockPlayerGoldTransaction {
  id: string
  amount: number
  balanceBefore: number
  balanceAfter: number
  note: string | null
  createdAtUtc: string
}

export interface MockSupportTicketAuditEvent {
  id: string
  eventType: string
  actorEmail: string
  actorDisplayName: string
  note: string
  metadataJson: string
  createdAtUtc: string
}

export interface MockSupportTicket {
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
  createdByPlayerId: string
  createdAtUtc: string
  updatedAtUtc: string
  statusUpdatedAtUtc: string
  extractedUrls: string[]
  extractedImages: string[]
  activity: MockSupportTicketAuditEvent[]
}

export interface MockRankingSummary {
  totalPoints: number
  globalRank: number
  previousGlobalRank: number
  rankMovement: number
  updatedAtUtc: string
}

export interface MockRankingLeaderboardEntry {
  playerId: string
  displayName: string
  personalAccountName?: string
  totalPoints: number
  globalRank: number
  rankMovement: number
}

export interface MockRankingRewardHistoryItem {
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

export interface MockRankingBountyDashboardItem {
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

export interface MockRankingBountyDefinition {
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

export interface MockRankingEventModerationItem {
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

export interface MockRankingRunInfo {
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

export interface MockRankingTelemetryBatchInfo {
  batchId: string
  serverKeyMasked: string
  flagReasonCode: string
  eventCount: number
  isQuarantined: boolean
  hasAppliedLeaderboardImpact: boolean
  quarantineReason: string | null
  clearJustification: string | null
  createdAtUtc: string
  lastAttemptAtUtc: string
}

export interface MockState {
  servers: MockGameServer[]
  currentToken: string | null
  currentPlayer: MockPlayer | null
  subscription: MockSubscription | null
  goldBalances: MockGoldBalance[]
  goldTransactions: MockGoldTransaction[]
  isGlobalAdmin: boolean
  /** Player-facing gold account data (for myGoldAccount query). Defaults to zero balance with no transactions. */
  playerGoldAccount: {
    goldTokenBalance: number
    lastUpdatedAtUtc: string | null
    recentTransactions: MockPlayerGoldTransaction[]
  } | null
  supportTickets: MockSupportTicket[]
  myCompanies: MockGameCompany[]
  apiKeys: MockApiKey[]
  apiKeyAuditLogs: MockApiKeyAuditLog[]
  rankingSummary: MockRankingSummary | null
  rankingLeaderboard: MockRankingLeaderboardEntry[]
  rankingBountyDashboard: MockRankingBountyDashboardItem[]
  rankingHistory: MockRankingRewardHistoryItem[]
  rankingBounties: MockRankingBountyDefinition[]
  rankingModerationEvents: MockRankingEventModerationItem[]
  rankingRuns: MockRankingRunInfo[]
  rankingTelemetryBatches: MockRankingTelemetryBatchInfo[]
}

export function makeSupportTicket(overrides: Partial<MockSupportTicket> = {}): MockSupportTicket {
  const now = new Date().toISOString()
  return {
    id: `support-${Math.random().toString(36).slice(2)}`,
    ticketType: 'BUG',
    status: 'SUBMITTED',
    title: 'Sample support ticket',
    markdownSource: 'Sample markdown body long enough to pass support ticket validation in tests.',
    sanitizedPreviewHtml: null,
    containsUnsafeContent: false,
    moderationState: 'PENDING',
    moderationReason: 'Awaiting administrator moderation.',
    moderatedByEmail: null,
    moderatedAtUtc: null,
    createdByEmail: 'alice@example.com',
    createdByDisplayName: 'Alice',
    createdByPlayerId: 'player-001',
    createdAtUtc: now,
    updatedAtUtc: now,
    statusUpdatedAtUtc: now,
    extractedUrls: [],
    extractedImages: [],
    activity: [],
    ...overrides,
  }
}

export function makeServer(overrides: Partial<MockGameServer> = {}): MockGameServer {
  return {
    id: 'server-001',
    serverKey: 'capitalism-eu-1',
    displayName: 'Capitalism EU #1',
    description: 'First production economy for EU players',
    region: 'EU',
    environment: 'production',
    backendUrl: 'https://game.example.com',
    graphqlUrl: 'https://game.example.com/graphql',
    frontendUrl: 'https://game.example.com/app',
    version: '1.0.0',
    playerCount: 42,
    companyCount: 128,
    currentTick: 5000,
    registeredAtUtc: '2026-04-01T00:00:00.000Z',
    lastHeartbeatAtUtc: new Date().toISOString(),
    isOnline: true,
    isActive: true,
    expiresAtUtc: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
    keyStatus: 'ACTIVE',
    ...overrides,
  }
}

export function makePlayer(overrides: Partial<MockPlayer> = {}): MockPlayer {
  return {
    id: 'player-001',
    email: 'alice@example.com',
    displayName: overrides.displayName ?? 'Alice',
    personalAccountName: overrides.personalAccountName ?? overrides.displayName ?? 'Alice',
    gender: overrides.gender ?? 'UNSPECIFIED',
    createdAtUtc: '2026-01-01T00:00:00.000Z',
    startupPackClaimedAtUtc: null,
    canClaimStartupPack: true,
    ...overrides,
  }
}

export function makeGameCompany(overrides: Partial<MockGameCompany> = {}): MockGameCompany {
  return {
    id: `company-${Math.random().toString(36).slice(2)}`,
    name: 'Example Company',
    ...overrides,
  }
}

export function makeApiKey(overrides: Partial<MockApiKey> = {}): MockApiKey {
  return {
    id: `api-key-${Math.random().toString(36).slice(2)}`,
    name: 'Example API Key',
    createdAtUtc: new Date().toISOString(),
    lastUsedAtUtc: null,
    totalCallCount: 0,
    revokedAtUtc: null,
    scopes: ['read-only'],
    companyIds: [],
    playerId: overrides.playerId ?? 'player-001',
    playerEmail: overrides.playerEmail ?? 'alice@example.com',
    playerDisplayName: overrides.playerDisplayName ?? 'Alice',
    ...overrides,
  }
}

export function makeApiKeyAuditLog(
  overrides: Partial<MockApiKeyAuditLog> = {},
): MockApiKeyAuditLog {
  return {
    id: `api-audit-${Math.random().toString(36).slice(2)}`,
    keyId: overrides.keyId ?? 'api-key-001',
    keyName: overrides.keyName ?? 'Example API Key',
    playerId: overrides.playerId ?? 'player-001',
    playerEmail: overrides.playerEmail ?? 'alice@example.com',
    playerDisplayName: overrides.playerDisplayName ?? 'Alice',
    operationName: 'me',
    operationType: 'query',
    scopeUsed: 'read-only',
    wasAllowed: true,
    denialCode: null,
    denialReason: null,
    attemptedObjectId: null,
    ipAddress: '127.0.0.1',
    sessionContext: 'mock-session',
    occurredAtUtc: new Date().toISOString(),
    ...overrides,
  }
}

export function makeSubscription(overrides: Partial<MockSubscription> = {}): MockSubscription {
  const expiresAtUtc = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString()
  return {
    tier: 'PRO',
    status: 'ACTIVE',
    isActive: true,
    daysRemaining: 30,
    canProlong: true,
    expiresAtUtc,
    startsAtUtc: '2026-04-01T00:00:00.000Z',
    ...overrides,
  }
}

export function setupMockApi(page: Page, initialState: Partial<MockState> = {}): MockState {
  const now = new Date().toISOString()

  const state: MockState = {
    servers: initialState.servers ?? [],
    currentToken: initialState.currentToken ?? null,
    currentPlayer: initialState.currentPlayer ?? null,
    subscription: initialState.subscription ?? null,
    goldBalances: initialState.goldBalances ?? [],
    goldTransactions: initialState.goldTransactions ?? [],
    isGlobalAdmin: initialState.isGlobalAdmin ?? false,
    playerGoldAccount: initialState.playerGoldAccount ?? null,
    supportTickets: initialState.supportTickets ?? [],
    myCompanies: initialState.myCompanies ?? [],
    apiKeys: initialState.apiKeys ?? [],
    apiKeyAuditLogs: initialState.apiKeyAuditLogs ?? [],
    rankingSummary: initialState.rankingSummary ?? {
      totalPoints: 125,
      globalRank: 4,
      previousGlobalRank: 6,
      rankMovement: 2,
      updatedAtUtc: now,
    },
    rankingLeaderboard: initialState.rankingLeaderboard ?? [
      {
        playerId: 'rank-1',
        displayName: 'Alpha',
        personalAccountName: 'Alpha',
        totalPoints: 320,
        globalRank: 1,
        rankMovement: 1,
      },
      {
        playerId: 'rank-2',
        displayName: 'Bravo',
        personalAccountName: 'Bravo',
        totalPoints: 240,
        globalRank: 2,
        rankMovement: -1,
      },
    ],
    rankingBountyDashboard: initialState.rankingBountyDashboard ?? [
      {
        id: 'bounty-dashboard-1',
        code: 'GAME_IMPROVER',
        displayName: 'Game improver',
        description: 'Submit suggestion ticket',
        rewardPoints: 5,
        cooldownMode: 'UTC_DAY',
        proofRequirement: 'NONE',
        requiresModeration: false,
        awardedToday: false,
        isAvailableNow: true,
        nextAvailableAtUtc: null,
        lastAwardedAtUtc: null,
        totalAwards: 0,
      },
    ],
    rankingHistory: initialState.rankingHistory ?? [
      {
        id: 'reward-1',
        bountyCode: 'GAME_IMPROVER',
        bountyDisplayName: 'Game improver',
        pointsAwarded: 5,
        status: 'AWARDED',
        serverKey: 'capitalism-eu-1',
        eventDateUtc: now,
        awardedAtUtc: now,
        metadataJson: '{}',
      },
    ],
    rankingBounties: initialState.rankingBounties ?? [
      {
        id: 'bounty-1',
        code: 'GAME_IMPROVER',
        displayName: 'Game improver',
        description: 'Submit suggestion ticket',
        rewardPoints: 5,
        isEnabled: true,
        isVisibleToPlayers: true,
        requiresModeration: false,
        cooldownMode: 'UTC_DAY',
        sourceEventType: 'GAME_IMPROVER',
        proofRequirement: 'NONE',
        visibilityScope: 'PLAYER_HISTORY',
        validationSettingsJson: '{}',
        updatedAtUtc: now,
      },
    ],
    rankingModerationEvents: initialState.rankingModerationEvents ?? [],
    rankingRuns: initialState.rankingRuns ?? [],
    rankingTelemetryBatches: initialState.rankingTelemetryBatches ?? [],
  }

  page.route('**/graphql', async (route) => {
    const body = route.request().postDataJSON() as { query: string; variables?: unknown }
    const query = body.query ?? ''

    // Register mutation
    if (query.includes('mutation') && query.includes('register')) {
      const vars = body.variables as { input: { email: string; displayName: string } }
      const player: MockPlayer = {
        id: 'new-player-001',
        email: vars?.input?.email ?? 'test@example.com',
        displayName: vars?.input?.displayName ?? 'Test Player',
        personalAccountName: vars?.input?.displayName ?? 'Test Player',
        gender: 'UNSPECIFIED',
        createdAtUtc: new Date().toISOString(),
        startupPackClaimedAtUtc: null,
        canClaimStartupPack: true,
      }
      state.currentPlayer = player
      state.currentToken = 'mock-token-abc'
      state.subscription = {
        tier: 'FREE',
        status: 'NONE',
        isActive: false,
        daysRemaining: null,
        canProlong: true,
        expiresAtUtc: null,
        startsAtUtc: null,
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            register: {
              token: state.currentToken,
              expiresAtUtc: new Date(Date.now() + 7200000).toISOString(),
              player,
            },
          },
        }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('createSupportTicket')) {
      if (!state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as {
        input: { ticketType: 'SUGGESTION' | 'BUG' | 'OTHER'; title: string; markdownSource: string }
      }
      const now = new Date().toISOString()
      const ticket = makeSupportTicket({
        id: `support-${Date.now()}`,
        ticketType: vars.input.ticketType,
        title: vars.input.title,
        markdownSource: vars.input.markdownSource,
        createdByEmail: state.currentPlayer.email,
        createdByDisplayName: state.currentPlayer.displayName,
        createdByPlayerId: state.currentPlayer.id,
        createdAtUtc: now,
        updatedAtUtc: now,
        statusUpdatedAtUtc: now,
        activity: [
          {
            id: `evt-${Date.now()}`,
            eventType: 'CREATED',
            actorEmail: state.currentPlayer.email,
            actorDisplayName: state.currentPlayer.displayName,
            note: 'Support ticket created.',
            metadataJson: '{}',
            createdAtUtc: now,
          },
        ],
      })
      state.supportTickets.unshift(ticket)

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { createSupportTicket: ticket } }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('updateSupportTicketContent')) {
      if (!state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as {
        input: { ticketId: string; title: string; markdownSource: string }
      }
      const ticket = state.supportTickets.find((item) => item.id === vars.input.ticketId)
      if (!ticket) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Support ticket not found.',
                extensions: { code: 'SUPPORT_TICKET_NOT_FOUND' },
              },
            ],
          }),
        })
        return
      }

      if (ticket.createdByPlayerId !== state.currentPlayer.id) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'Forbidden.', extensions: { code: 'SUPPORT_TICKET_FORBIDDEN' } }],
          }),
        })
        return
      }

      const now = new Date().toISOString()
      ticket.title = vars.input.title
      ticket.markdownSource = vars.input.markdownSource
      ticket.moderationState = 'PENDING'
      ticket.sanitizedPreviewHtml = null
      ticket.moderationReason = 'Content changed. Awaiting administrator moderation.'
      ticket.moderatedAtUtc = null
      ticket.moderatedByEmail = null
      ticket.updatedAtUtc = now
      ticket.activity.unshift({
        id: `evt-${Date.now()}`,
        eventType: 'CONTENT_UPDATED',
        actorEmail: state.currentPlayer.email,
        actorDisplayName: state.currentPlayer.displayName,
        note: 'Support ticket content was updated.',
        metadataJson: '{}',
        createdAtUtc: now,
      })

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { updateSupportTicketContent: ticket } }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('updateSupportTicketStatus')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as {
        input: { ticketId: string; status: MockSupportTicket['status']; note?: string }
      }
      const ticket = state.supportTickets.find((item) => item.id === vars.input.ticketId)
      if (!ticket) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Support ticket not found.',
                extensions: { code: 'SUPPORT_TICKET_NOT_FOUND' },
              },
            ],
          }),
        })
        return
      }

      const now = new Date().toISOString()
      ticket.status = vars.input.status
      ticket.statusUpdatedAtUtc = now
      ticket.updatedAtUtc = now
      ticket.activity.unshift({
        id: `evt-${Date.now()}`,
        eventType: 'STATUS_UPDATED',
        actorEmail: state.currentPlayer.email,
        actorDisplayName: state.currentPlayer.displayName,
        note: vars.input.note ?? `Support ticket status changed to ${vars.input.status}.`,
        metadataJson: JSON.stringify({ status: vars.input.status }),
        createdAtUtc: now,
      })

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { updateSupportTicketStatus: ticket } }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('moderateSupportTicket')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as {
        input: { ticketId: string; approve: boolean; note?: string }
      }
      const ticket = state.supportTickets.find((item) => item.id === vars.input.ticketId)
      if (!ticket) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Support ticket not found.',
                extensions: { code: 'SUPPORT_TICKET_NOT_FOUND' },
              },
            ],
          }),
        })
        return
      }

      const now = new Date().toISOString()
      ticket.moderationState = vars.input.approve ? 'APPROVED' : 'REJECTED'
      ticket.moderatedAtUtc = now
      ticket.moderatedByEmail = state.currentPlayer.email
      ticket.moderationReason =
        vars.input.note ??
        (vars.input.approve
          ? 'Content approved by administrator.'
          : 'Content rejected by administrator.')
      ticket.updatedAtUtc = now
      ticket.sanitizedPreviewHtml = vars.input.approve
        ? `<p>${ticket.markdownSource.replaceAll('<', '&lt;').replaceAll('>', '&gt;')}</p>`
        : null
      ticket.activity.unshift({
        id: `evt-${Date.now()}`,
        eventType: 'MODERATION_UPDATED',
        actorEmail: state.currentPlayer.email,
        actorDisplayName: state.currentPlayer.displayName,
        note: ticket.moderationReason,
        metadataJson: JSON.stringify({ moderationState: ticket.moderationState }),
        createdAtUtc: now,
      })

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { moderateSupportTicket: ticket } }),
      })
      return
    }

    if (query.includes('mySupportTickets')) {
      if (!state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as
        | {
            input?: {
              searchTitle?: string
              ticketType?: string
              status?: string
              sortBy?: string
              sortDirection?: string
            }
          }
        | undefined
      let list = state.supportTickets.filter(
        (ticket) => ticket.createdByPlayerId === state.currentPlayer?.id,
      )
      const filter = vars?.input
      if (filter?.ticketType)
        list = list.filter((ticket) => ticket.ticketType === filter.ticketType)
      if (filter?.status) list = list.filter((ticket) => ticket.status === filter.status)
      if (filter?.searchTitle?.trim()) {
        const q = filter.searchTitle.trim().toLowerCase()
        list = list.filter((ticket) => ticket.title.toLowerCase().includes(q))
      }

      const direction = filter?.sortDirection === 'ASC' ? 1 : -1
      const sortBy = filter?.sortBy ?? 'CREATED_AT'
      list = [...list].sort((a, b) => {
        if (sortBy === 'TITLE') {
          return a.title.localeCompare(b.title) * direction
        }
        const left = sortBy === 'UPDATED_AT' ? a.updatedAtUtc : a.createdAtUtc
        const right = sortBy === 'UPDATED_AT' ? b.updatedAtUtc : b.createdAtUtc
        return (left < right ? -1 : left > right ? 1 : 0) * direction
      })

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { mySupportTickets: list } }),
      })
      return
    }

    if (query.includes('supportTicketsAdmin')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as
        | {
            input?: {
              searchTitle?: string
              ticketType?: string
              status?: string
              sortBy?: string
              sortDirection?: string
              unsafeOnly?: boolean
            }
          }
        | undefined
      let list = [...state.supportTickets]
      const filter = vars?.input
      if (filter?.ticketType)
        list = list.filter((ticket) => ticket.ticketType === filter.ticketType)
      if (filter?.status) list = list.filter((ticket) => ticket.status === filter.status)
      if (filter?.unsafeOnly) list = list.filter((ticket) => ticket.containsUnsafeContent)
      if (filter?.searchTitle?.trim()) {
        const q = filter.searchTitle.trim().toLowerCase()
        list = list.filter((ticket) => ticket.title.toLowerCase().includes(q))
      }

      const direction = filter?.sortDirection === 'ASC' ? 1 : -1
      const sortBy = filter?.sortBy ?? 'CREATED_AT'
      list = list.sort((a, b) => {
        if (sortBy === 'TITLE') {
          return a.title.localeCompare(b.title) * direction
        }
        const left = sortBy === 'UPDATED_AT' ? a.updatedAtUtc : a.createdAtUtc
        const right = sortBy === 'UPDATED_AT' ? b.updatedAtUtc : b.createdAtUtc
        return (left < right ? -1 : left > right ? 1 : 0) * direction
      })

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { supportTicketsAdmin: list } }),
      })
      return
    }

    if (query.includes('myRankingSummary')) {
      if (!state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } },
            ],
          }),
        })
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myRankingSummary: state.rankingSummary } }),
      })
      return
    }

    if (query.includes('rankingLeaderboard')) {
      const vars = body.variables as { limit?: number; offset?: number } | undefined
      const offset = Math.max(0, vars?.offset ?? 0)
      const limit = Math.max(1, vars?.limit ?? 100)
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            rankingLeaderboard: state.rankingLeaderboard.slice(offset, offset + limit),
          },
        }),
      })
      return
    }

    if (query.includes('myRankingBountyHistory')) {
      if (!state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as {
        input?: { bountyCode?: string; serverKey?: string; status?: string }
      }
      let history = [...state.rankingHistory]
      if (vars?.input?.bountyCode) {
        history = history.filter((entry) => entry.bountyCode === vars.input?.bountyCode)
      }
      if (vars?.input?.serverKey) {
        history = history.filter((entry) => entry.serverKey === vars.input?.serverKey)
      }
      if (vars?.input?.status) {
        history = history.filter((entry) => entry.status === vars.input?.status)
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myRankingBountyHistory: history } }),
      })
      return
    }

    if (query.includes('myRankingBountyDashboard')) {
      if (!state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } },
            ],
          }),
        })
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myRankingBountyDashboard: state.rankingBountyDashboard } }),
      })
      return
    }

    if (query.includes('canAccessRankingAdminDashboard')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { canAccessRankingAdminDashboard: !!state.isGlobalAdmin } }),
      })
      return
    }

    if (query.includes('rankingAdminDashboard')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } },
            ],
          }),
        })
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            rankingAdminDashboard: {
              bounties: state.rankingBounties,
              pendingModerationEvents: state.rankingModerationEvents,
              recentRuns: state.rankingRuns,
              flaggedTelemetryBatches: state.rankingTelemetryBatches,
            },
          },
        }),
      })
      return
    }

    if (query.includes('quarantinedTelemetryBatches')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            quarantinedTelemetryBatches: state.rankingTelemetryBatches.filter(
              (item) => item.isQuarantined,
            ),
          },
        }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('submitRankingProofEvent')) {
      if (!state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as {
        bountyCode: string
        proofReference: string
        uniqueScopeKey?: string
      }
      const nowIso = new Date().toISOString()
      const item: MockRankingEventModerationItem = {
        id: `rank-proof-${Date.now()}`,
        eventType: vars.bountyCode,
        playerEmail: state.currentPlayer.email,
        serverKey: null,
        proofReference: vars.proofReference,
        payloadJson: JSON.stringify({ uniqueScopeKey: vars.uniqueScopeKey ?? null }),
        status: 'PENDING_MODERATION',
        occurredAtUtc: nowIso,
        createdAtUtc: nowIso,
      }
      state.rankingModerationEvents.unshift(item)

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { submitRankingProofEvent: item } }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('moderateRankingEvent')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as { input: { eventId: string; approve: boolean } }
      const item = state.rankingModerationEvents.find(
        (eventItem) => eventItem.id === vars.input.eventId,
      )
      if (!item) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Ranking event not found.',
                extensions: { code: 'RANKING_EVENT_NOT_FOUND' },
              },
            ],
          }),
        })
        return
      }

      item.status = vars.input.approve ? 'APPROVED' : 'REJECTED'

      if (vars.input.approve) {
        state.rankingHistory.unshift({
          id: `reward-${Date.now()}`,
          bountyCode: item.eventType,
          bountyDisplayName: item.eventType,
          pointsAwarded: 5,
          status: 'AWARDED',
          serverKey: item.serverKey,
          eventDateUtc: item.occurredAtUtc,
          awardedAtUtc: new Date().toISOString(),
          metadataJson: '{}',
        })
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { moderateRankingEvent: item } }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('quarantineTelemetryBatch')) {
      const vars = body.variables as { batchId: string; reason: string }
      const batch = state.rankingTelemetryBatches.find((item) => item.batchId === vars.batchId)
      if (!batch) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Telemetry batch not found.',
                extensions: { code: 'TELEMETRY_BATCH_NOT_FOUND' },
              },
            ],
          }),
        })
        return
      }

      batch.isQuarantined = true
      batch.quarantineReason = vars.reason
      batch.clearJustification = null
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { quarantineTelemetryBatch: batch } }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('clearQuarantine')) {
      const vars = body.variables as { batchId: string; justification: string }
      const batch = state.rankingTelemetryBatches.find((item) => item.batchId === vars.batchId)
      if (!batch) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Telemetry batch not found.',
                extensions: { code: 'TELEMETRY_BATCH_NOT_FOUND' },
              },
            ],
          }),
        })
        return
      }

      batch.isQuarantined = false
      batch.clearJustification = vars.justification
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { clearQuarantine: batch } }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('upsertRankingBountyDefinition')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as { input: MockRankingBountyDefinition }
      const index = state.rankingBounties.findIndex((item) => item.code === vars.input.code)
      const value = {
        ...vars.input,
        id: vars.input.id ?? `bounty-${Date.now()}`,
        updatedAtUtc: new Date().toISOString(),
      }
      if (index >= 0) {
        state.rankingBounties[index] = value
      } else {
        state.rankingBounties.push(value)
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { upsertRankingBountyDefinition: value } }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('runRankingEvaluationNow')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } },
            ],
          }),
        })
        return
      }

      const nowIso = new Date().toISOString()
      const run: MockRankingRunInfo = {
        id: `run-eval-${Date.now()}`,
        runType: 'HOURLY_EVALUATION',
        status: 'SUCCEEDED',
        startedAtUtc: nowIso,
        finishedAtUtc: nowIso,
        processedEvents: 12,
        rewardRecordsCreated: 8,
        totalPointsAwarded: 32,
        totalPointsBeforeDecay: 0,
        totalPointsAfterDecay: 0,
        notes: 'Mock hourly run',
      }
      state.rankingRuns.unshift(run)

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { runRankingEvaluationNow: run } }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('runRankingDailyDecayNow')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } },
            ],
          }),
        })
        return
      }

      const nowIso = new Date().toISOString()
      const run: MockRankingRunInfo = {
        id: `run-decay-${Date.now()}`,
        runType: 'DAILY_DECAY',
        status: 'SUCCEEDED',
        startedAtUtc: nowIso,
        finishedAtUtc: nowIso,
        processedEvents: 0,
        rewardRecordsCreated: 0,
        totalPointsAwarded: 0,
        totalPointsBeforeDecay: 1200,
        totalPointsAfterDecay: 1188,
        notes: 'Mock daily decay run',
      }
      state.rankingRuns.unshift(run)

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { runRankingDailyDecayNow: run } }),
      })
      return
    }

    // Login mutation
    if (query.includes('mutation') && query.includes('login')) {
      if (state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            data: {
              login: {
                token: state.currentToken ?? 'mock-token-abc',
                expiresAtUtc: new Date(Date.now() + 7200000).toISOString(),
                player: state.currentPlayer,
              },
            },
          }),
        })
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Invalid email or password.',
                extensions: { code: 'INVALID_CREDENTIALS' },
              },
            ],
          }),
        })
      }
      return
    }

    if (query.includes('mutation') && query.includes('updatePersonalAccountName')) {
      if (!state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } },
            ],
          }),
        })
        return
      }

      const vars = body.variables as {
        input?: { personalAccountName?: string; gender?: 'MALE' | 'FEMALE' | 'UNSPECIFIED' }
      } | undefined
      const personalAccountName = vars?.input?.personalAccountName?.trim() ?? ''
      const gender = vars?.input?.gender ?? state.currentPlayer.gender ?? 'UNSPECIFIED'
      if (!personalAccountName) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Personal account name is required.',
                extensions: { code: 'PERSONAL_ACCOUNT_NAME_REQUIRED' },
              },
            ],
          }),
        })
        return
      }

      const duplicate = state.rankingLeaderboard.some(
        (entry) =>
          entry.playerId !== state.currentPlayer?.id &&
          (entry.personalAccountName ?? entry.displayName).toLowerCase() ===
            personalAccountName.toLowerCase(),
      )
      if (duplicate) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'This personal account name is already taken.',
                extensions: { code: 'PERSONAL_ACCOUNT_NAME_NOT_UNIQUE' },
              },
            ],
          }),
        })
        return
      }

      state.currentPlayer = {
        ...state.currentPlayer,
        displayName: personalAccountName,
        personalAccountName,
        gender,
      }
      state.rankingLeaderboard = state.rankingLeaderboard.map((entry) =>
        entry.playerId === state.currentPlayer?.id
          ? {
              ...entry,
              displayName: personalAccountName,
              personalAccountName,
            }
          : entry,
      )

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            updatePersonalAccountName: {
              personalAccountName,
              gender,
            },
          },
        }),
      })
      return
    }

    // ProlongSubscription mutation
    if (query.includes('mutation') && query.includes('prolongSubscription')) {
      const newSub: MockSubscription = {
        tier: 'PRO',
        status: 'ACTIVE',
        isActive: true,
        daysRemaining: 30,
        canProlong: true,
        expiresAtUtc: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
        startsAtUtc: new Date().toISOString(),
      }
      state.subscription = newSub
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { prolongSubscription: newSub } }),
      })
      return
    }

    if (query.includes('mutation') && query.includes('claimStartupPack')) {
      const now = new Date().toISOString()
      if (state.currentPlayer?.canClaimStartupPack) {
        state.currentPlayer = {
          ...state.currentPlayer,
          canClaimStartupPack: false,
          startupPackClaimedAtUtc: now,
        }
      }

      state.subscription = {
        tier: 'PRO',
        status: 'ACTIVE',
        isActive: true,
        daysRemaining: 90,
        canProlong: true,
        expiresAtUtc: new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString(),
        startsAtUtc: state.subscription?.startsAtUtc ?? now,
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { claimStartupPack: state.subscription } }),
      })
      return
    }

    if (query.includes('generateApiKey')) {
      const vars = body.variables as
        | { input?: { name?: string; scopes?: string[]; companyIds?: string[] } }
        | undefined
      const created = makeApiKey({
        name: vars?.input?.name ?? 'Generated Key',
        scopes: vars?.input?.scopes ?? ['read-only'],
        companyIds: vars?.input?.companyIds ?? [],
        playerId: state.currentPlayer?.id ?? 'player-001',
        playerEmail: state.currentPlayer?.email ?? 'alice@example.com',
        playerDisplayName: state.currentPlayer?.displayName ?? 'Alice',
      })
      state.apiKeys = [created, ...state.apiKeys]
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            generateApiKey: {
              apiKey: created,
              plaintextKey: `plain-${created.id}`,
            },
          },
        }),
      })
      return
    }

    if (query.includes('forceRevokeApiKey')) {
      const vars = body.variables as { keyId?: string } | undefined
      state.apiKeys = state.apiKeys.map((key) =>
        key.id === vars?.keyId ? { ...key, revokedAtUtc: new Date().toISOString() } : key,
      )
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { forceRevokeApiKey: true } }),
      })
      return
    }

    if (query.includes('revokeAllPlayerApiKeys')) {
      const vars = body.variables as { playerId?: string } | undefined
      let revokedCount = 0
      state.apiKeys = state.apiKeys.map((key) => {
        if (key.playerId === vars?.playerId && !key.revokedAtUtc) {
          revokedCount += 1
          return { ...key, revokedAtUtc: new Date().toISOString() }
        }
        return key
      })
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { revokeAllPlayerApiKeys: revokedCount } }),
      })
      return
    }

    if (query.includes('revokeApiKey')) {
      const vars = body.variables as { input?: { keyId?: string } } | undefined
      state.apiKeys = state.apiKeys.map((key) =>
        key.id === vars?.input?.keyId ? { ...key, revokedAtUtc: new Date().toISOString() } : key,
      )
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { revokeApiKey: true } }),
      })
      return
    }

    if (query.includes('executeForexSwap')) {
      const authorization = route.request().headers().authorization ?? ''
      if (authorization.startsWith('ApiKey ')) {
        const rawKey = authorization.slice('ApiKey '.length)
        const matchedKey = state.apiKeys.find((key) => `plain-${key.id}` === rawKey)
        if (!matchedKey) {
          await route.fulfill({
            status: 403,
            contentType: 'application/json',
            body: JSON.stringify({
              data: null,
              errors: [{ message: 'Forbidden.', extensions: { code: 'API_KEY_SCOPE_FORBIDDEN' } }],
            }),
          })
          return
        }

        const allowed = matchedKey.scopes.includes('trading-only')
        const usedScope =
          matchedKey.scopes.find((scope) => scope !== 'company-bound') ??
          matchedKey.scopes[0] ??
          'none'
        state.apiKeyAuditLogs = [
          makeApiKeyAuditLog({
            keyId: matchedKey.id,
            keyName: matchedKey.name,
            playerId: matchedKey.playerId,
            playerEmail: matchedKey.playerEmail,
            playerDisplayName: matchedKey.playerDisplayName,
            operationName: 'executeForexSwap',
            operationType: 'mutation',
            scopeUsed: allowed ? 'trading-only' : usedScope,
            wasAllowed: allowed,
            denialCode: allowed ? null : 'API_KEY_SCOPE_FORBIDDEN',
          }),
          ...state.apiKeyAuditLogs,
        ]

        await route.fulfill({
          status: allowed ? 200 : 403,
          contentType: 'application/json',
          body: JSON.stringify(
            allowed
              ? { data: { executeForexSwap: { fromAmount: 100 } } }
              : {
                  data: null,
                  errors: [
                    { message: 'Forbidden.', extensions: { code: 'API_KEY_SCOPE_FORBIDDEN' } },
                  ],
                },
          ),
        })
        return
      }
    }

    if (query.includes('myCompanies')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myCompanies: state.myCompanies } }),
      })
      return
    }

    if (query.includes('myApiKeys')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            myApiKeys: state.apiKeys.filter((key) => key.playerId === state.currentPlayer?.id),
          },
        }),
      })
      return
    }

    if (query.includes('myApiKeyAuditLog')) {
      const vars = body.variables as { keyId?: string } | undefined
      const items = state.apiKeyAuditLogs.filter(
        (entry) =>
          entry.playerId === state.currentPlayer?.id &&
          (!vars?.keyId || entry.keyId === vars.keyId),
      )
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myApiKeyAuditLog: items } }),
      })
      return
    }

    if (query.includes('adminApiKeys')) {
      const vars = body.variables as { playerEmail?: string | null } | undefined
      const filter = vars?.playerEmail?.toLowerCase()
      const items = state.apiKeys
        .filter((key) => !filter || key.playerEmail.toLowerCase().includes(filter))
        .map((key) => ({
          playerId: key.playerId,
          playerEmail: key.playerEmail,
          playerDisplayName: key.playerDisplayName,
          key,
        }))
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { adminApiKeys: items } }),
      })
      return
    }

    if (query.includes('adminApiKeyAuditLog')) {
      const vars = body.variables as
        | { playerEmail?: string | null; keyId?: string | null }
        | undefined
      const filter = vars?.playerEmail?.toLowerCase()
      const items = state.apiKeyAuditLogs.filter(
        (entry) =>
          (!filter || entry.playerEmail.toLowerCase().includes(filter)) &&
          (!vars?.keyId || entry.keyId === vars.keyId),
      )
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { adminApiKeyAuditLog: items } }),
      })
      return
    }

    // Me query must match only the standalone `me { ... }` field selection.
    const isStandaloneMeQuery =
      /\bme\s*\{/.test(query) &&
      !query.includes('gameServers') &&
      !query.includes('mySubscription') &&
      !query.includes('prolongSubscription') &&
      !query.includes('goldTokenBalances') &&
      !query.includes('goldTokenTransactions') &&
      !query.includes('adjustGoldTokenBalance') &&
      !query.includes('myGoldAccount')

    if (isStandaloneMeQuery) {
      if (state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { me: state.currentPlayer } }),
        })
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } },
            ],
          }),
        })
      }
      return
    }

    // myGoldAccount query
    if (query.includes('myGoldAccount')) {
      if (!state.currentPlayer) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } },
            ],
          }),
        })
        return
      }
      const goldAccount = state.playerGoldAccount ?? {
        goldTokenBalance: 0,
        lastUpdatedAtUtc: null,
        recentTransactions: [],
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myGoldAccount: goldAccount } }),
      })
      return
    }

    // MySubscription query
    if (query.includes('mySubscription')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            mySubscription: state.subscription ?? {
              tier: 'FREE',
              status: 'NONE',
              isActive: false,
              daysRemaining: null,
              canProlong: true,
              expiresAtUtc: null,
              startsAtUtc: null,
            },
          },
        }),
      })
      return
    }

    // GameServers query
    if (query.includes('gameServers')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { gameServers: state.servers } }),
      })
      return
    }

    // goldTokenBalances query
    if (query.includes('goldTokenBalances')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Gold token administration requires global admin access.',
                extensions: { code: 'GLOBAL_ADMIN_REQUIRED' },
              },
            ],
          }),
        })
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { goldTokenBalances: state.goldBalances } }),
        })
      }
      return
    }

    // goldTokenTransactions query
    if (query.includes('goldTokenTransactions')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Gold token administration requires global admin access.',
                extensions: { code: 'GLOBAL_ADMIN_REQUIRED' },
              },
            ],
          }),
        })
      } else {
        const vars = body.variables as { targetEmail?: string } | undefined
        const emailFilter = vars?.targetEmail?.toLowerCase()
        const filtered = emailFilter
          ? state.goldTransactions.filter((tx) => tx.playerEmail.toLowerCase() === emailFilter)
          : state.goldTransactions
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { goldTokenTransactions: filtered } }),
        })
      }
      return
    }

    // adjustGoldTokenBalance mutation
    if (query.includes('mutation') && query.includes('adjustGoldTokenBalance')) {
      if (!state.currentPlayer || !state.isGlobalAdmin) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              {
                message: 'Gold token administration requires global admin access.',
                extensions: { code: 'GLOBAL_ADMIN_REQUIRED' },
              },
            ],
          }),
        })
        return
      }

      const vars = body.variables as {
        input: { targetEmail: string; amount: number; note?: string }
      }
      const targetEmail = vars?.input?.targetEmail?.toLowerCase()
      const amount = vars?.input?.amount ?? 0
      const note = vars?.input?.note ?? null

      if (amount === 0) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Amount must be non-zero.', extensions: { code: 'INVALID_AMOUNT' } },
            ],
          }),
        })
        return
      }

      const target = state.goldBalances.find((b) => b.email.toLowerCase() === targetEmail)
      if (!target) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Target player not found.', extensions: { code: 'PLAYER_NOT_FOUND' } },
            ],
          }),
        })
        return
      }

      const newBalance = target.goldTokenBalance + amount
      if (newBalance < 0) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Insufficient balance.', extensions: { code: 'INSUFFICIENT_BALANCE' } },
            ],
          }),
        })
        return
      }

      const balanceBefore = target.goldTokenBalance
      target.goldTokenBalance = newBalance

      const tx: MockGoldTransaction = {
        id: `tx-${Date.now()}`,
        playerEmail: target.email,
        amount,
        balanceBefore,
        balanceAfter: newBalance,
        adminEmail: state.currentPlayer.email,
        note,
        createdAtUtc: new Date().toISOString(),
      }
      state.goldTransactions.unshift(tx)

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            adjustGoldTokenBalance: {
              playerId: target.playerId,
              email: target.email,
              displayName: target.displayName,
              goldTokenBalance: target.goldTokenBalance,
            },
          },
        }),
      })
      return
    }

    // Fallback: pass through or return empty
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: {} }),
    })
  })

  return state
}

export async function loginAs(
  page: Page,
  state: MockState,
  player: MockPlayer,
  token = 'mock-token-abc',
) {
  state.currentPlayer = player
  state.currentToken = token
  await page.addInitScript(
    ({ expiresAtUtc }: { expiresAtUtc: string }) => {
      localStorage.setItem('master_auth_expires', expiresAtUtc)
      localStorage.setItem('master_auth_provider', 'local')
    },
    {
      expiresAtUtc: new Date(Date.now() + 7_200_000).toISOString(),
    },
  )
  await page.context().addCookies([
    {
      name: 'auth_token',
      value: token,
      url: process.env.CI ? 'http://localhost:4174' : 'http://localhost:5174',
      httpOnly: true,
      sameSite: 'Strict',
    },
  ])
}
