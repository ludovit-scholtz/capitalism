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
  createdAtUtc: string
  startupPackClaimedAtUtc: string | null
  canClaimStartupPack: boolean
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
    ...overrides,
  }
}

export function makePlayer(overrides: Partial<MockPlayer> = {}): MockPlayer {
  return {
    id: 'player-001',
    email: 'alice@example.com',
    displayName: 'Alice',
    createdAtUtc: '2026-01-01T00:00:00.000Z',
    startupPackClaimedAtUtc: null,
    canClaimStartupPack: true,
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
            errors: [{ message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } }],
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
            errors: [{ message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } }],
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
              { message: 'Support ticket not found.', extensions: { code: 'SUPPORT_TICKET_NOT_FOUND' } },
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
            errors: [
              { message: 'Forbidden.', extensions: { code: 'SUPPORT_TICKET_FORBIDDEN' } },
            ],
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
            errors: [{ message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } }],
          }),
        })
        return
      }

      const vars = body.variables as { input: { ticketId: string; status: MockSupportTicket['status']; note?: string } }
      const ticket = state.supportTickets.find((item) => item.id === vars.input.ticketId)
      if (!ticket) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Support ticket not found.', extensions: { code: 'SUPPORT_TICKET_NOT_FOUND' } },
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
            errors: [{ message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } }],
          }),
        })
        return
      }

      const vars = body.variables as { input: { ticketId: string; approve: boolean; note?: string } }
      const ticket = state.supportTickets.find((item) => item.id === vars.input.ticketId)
      if (!ticket) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [
              { message: 'Support ticket not found.', extensions: { code: 'SUPPORT_TICKET_NOT_FOUND' } },
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
        (vars.input.approve ? 'Content approved by administrator.' : 'Content rejected by administrator.')
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
            errors: [{ message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } }],
          }),
        })
        return
      }

      const vars = body.variables as { input?: { searchTitle?: string; ticketType?: string; status?: string; sortBy?: string; sortDirection?: string } } | undefined
      let list = state.supportTickets.filter((ticket) => ticket.createdByPlayerId === state.currentPlayer?.id)
      const filter = vars?.input
      if (filter?.ticketType) list = list.filter((ticket) => ticket.ticketType === filter.ticketType)
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
            errors: [{ message: 'Global admin required.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } }],
          }),
        })
        return
      }

      const vars = body.variables as { input?: { searchTitle?: string; ticketType?: string; status?: string; sortBy?: string; sortDirection?: string; unsafeOnly?: boolean } } | undefined
      let list = [...state.supportTickets]
      const filter = vars?.input
      if (filter?.ticketType) list = list.filter((ticket) => ticket.ticketType === filter.ticketType)
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

    // Me query — must not match gameServers, mySubscription, prolongSubscription, or gold token queries
    if (
      query.includes('me') &&
      !query.includes('gameServers') &&
      !query.includes('mySubscription') &&
      !query.includes('prolongSubscription') &&
      !query.includes('goldTokenBalances') &&
      !query.includes('goldTokenTransactions') &&
      !query.includes('adjustGoldTokenBalance') &&
      !query.includes('myGoldAccount')
    ) {
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
            errors: [{ message: 'Not authenticated.', extensions: { code: 'AUTH_NOT_AUTHENTICATED' } }],
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
      if (!state.currentPlayer || (!state.isGlobalAdmin)) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'Gold token administration requires global admin access.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } }],
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
            errors: [{ message: 'Gold token administration requires global admin access.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } }],
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
            errors: [{ message: 'Gold token administration requires global admin access.', extensions: { code: 'GLOBAL_ADMIN_REQUIRED' } }],
          }),
        })
        return
      }

      const vars = body.variables as { input: { targetEmail: string; amount: number; note?: string } }
      const targetEmail = vars?.input?.targetEmail?.toLowerCase()
      const amount = vars?.input?.amount ?? 0
      const note = vars?.input?.note ?? null

      if (amount === 0) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'Amount must be non-zero.', extensions: { code: 'INVALID_AMOUNT' } }],
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
            errors: [{ message: 'Target player not found.', extensions: { code: 'PLAYER_NOT_FOUND' } }],
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
            errors: [{ message: 'Insufficient balance.', extensions: { code: 'INSUFFICIENT_BALANCE' } }],
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
    ({ tok, exp }) => {
      localStorage.setItem('master_auth_token', tok)
      localStorage.setItem('master_auth_expires', exp)
    },
    { tok: token, exp: new Date(Date.now() + 7200000).toISOString() },
  )
}
