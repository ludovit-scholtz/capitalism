import { defineStore } from 'pinia'
import { ref } from 'vue'

import { gqlRequest } from '@/lib/graphql'
import type {
  AccountContextType,
  AuthPayload,
  GameAdminDashboard,
  GameAdminPlayer,
  GameAdminSession,
  GamesEntry,
  GamesLocalization,
  GlobalGameAdminGrant,
  RealWorldBillionaireAdminRecord,
} from '@/types'

const BASE_PLAYER_FIELDS = `
  id
  email
  displayName
  role
  isInvisibleInChat
  createdAtUtc
  lastLoginAtUtc
  personalCash
  totalCompanyCash
  totalCompanyEquity
  companyCount
  cityNames
  companies {
    id
    name
    cash
  }
`

const SESSION_PLAYER_FIELDS = `
  id
  email
  displayName
  role
  isInvisibleInChat
  lastLoginAtUtc
  personalCash
  totalCompanyCash
  companyCount
  companies {
    id
    name
    cash
  }
`

const SESSION_FIELDS = `
  isLocalAdmin
  hasGlobalAdminRole
  isRootAdministrator
  canAccessAdminDashboard
  isImpersonating
  effectiveAccountType
  effectiveCompanyId
  effectiveCompanyName
  adminActor {
    ${SESSION_PLAYER_FIELDS}
  }
  effectivePlayer {
    ${SESSION_PLAYER_FIELDS}
  }
`

const DASHBOARD_FIELDS = `
  serverKey
  totalPersonalCash
  totalCompanyCash
  moneySupply
  externalMoneyInflowLast100Ticks
  totalShippingCostsLast100Ticks
  inflowSummaries {
    category
    amount
    description
  }
  shippingCostSummaries {
    companyId
    companyName
    amount
    entryCount
  }
  multiAccountAlerts {
    reason
    exposureAmount
    confidenceScore
    supportingEntityType
    supportingEntityName
    primaryPlayer {
      ${BASE_PLAYER_FIELDS}
    }
    relatedPlayer {
      ${BASE_PLAYER_FIELDS}
    }
  }
  players {
    ${BASE_PLAYER_FIELDS}
  }
  invisiblePlayers {
    ${BASE_PLAYER_FIELDS}
  }
  governmentPlayer {
    ${BASE_PLAYER_FIELDS}
  }
  globalGameAdminGrants {
    id
    email
    grantedByEmail
    grantedAtUtc
    updatedAtUtc
  }
  recentAuditLogs {
    id
    adminActorPlayerId
    adminActorEmail
    adminActorDisplayName
    effectivePlayerId
    effectivePlayerEmail
    effectivePlayerDisplayName
    effectiveAccountType
    effectiveCompanyId
    effectiveCompanyName
    graphQlOperationName
    mutationSummary
    responseStatusCode
    recordedAtUtc
  }
  realWorldBillionaires {
    id
    rank
    name
    wealthUsd
    updatedAtUtc
  }
`

export const useGameAdminStore = defineStore('gameAdmin', () => {
  const session = ref<GameAdminSession | null>(null)
  const dashboard = ref<GameAdminDashboard | null>(null)
  const loadingSession = ref(false)
  const loadingDashboard = ref(false)
  const error = ref<string | null>(null)

  async function fetchSession() {
    loadingSession.value = true
    error.value = null

    try {
      const data = await gqlRequest<{ gameAdminSession: GameAdminSession }>(`{
        gameAdminSession {
          ${SESSION_FIELDS}
        }
      }`)

      session.value = data.gameAdminSession
      return data.gameAdminSession
    } catch (caughtError) {
      error.value = caughtError instanceof Error ? caughtError.message : 'Failed to load game administration session.'
      throw caughtError
    } finally {
      loadingSession.value = false
    }
  }

  async function fetchDashboard() {
    loadingDashboard.value = true
    error.value = null

    try {
      const data = await gqlRequest<{ gameAdminDashboard: GameAdminDashboard }>(`{
        gameAdminDashboard {
          ${DASHBOARD_FIELDS}
        }
      }`)

      dashboard.value = data.gameAdminDashboard
      return data.gameAdminDashboard
    } catch (caughtError) {
      error.value = caughtError instanceof Error ? caughtError.message : 'Failed to load game administration dashboard.'
      throw caughtError
    } finally {
      loadingDashboard.value = false
    }
  }

  async function startImpersonation(targetPlayerId: string, accountType: AccountContextType, companyId?: string | null) {
    const data = await gqlRequest<{ startAdminImpersonation: AuthPayload }>(
      `mutation StartAdminImpersonation($input: StartAdminImpersonationInput!) {
        startAdminImpersonation(input: $input) {
          token
          expiresAtUtc
          player {
            id
            displayName
            email
            role
            createdAtUtc
            lastLoginAtUtc
            personalCash
            activeAccountType
            activeCompanyId
            onboardingCompletedAtUtc
            onboardingCurrentStep
            onboardingIndustry
            onboardingCityId
            onboardingCompanyId
            onboardingFactoryLotId
            onboardingShopBuildingId
            onboardingFirstSaleCompletedAtUtc
            proSubscriptionEndsAtUtc
            companies {
              id
              name
              cash
              foundedAtUtc
              foundedAtTick
            }
          }
        }
      }`,
      {
        input: {
          targetPlayerId,
          accountType,
          companyId: accountType === 'COMPANY' ? companyId : null,
        },
      },
    )

    return data.startAdminImpersonation
  }

  async function stopImpersonation() {
    const data = await gqlRequest<{ stopAdminImpersonation: AuthPayload }>(`mutation StopAdminImpersonation {
      stopAdminImpersonation {
        token
        expiresAtUtc
        player {
          id
          displayName
          email
          role
          createdAtUtc
          lastLoginAtUtc
          personalCash
          activeAccountType
          activeCompanyId
          onboardingCompletedAtUtc
          onboardingCurrentStep
          onboardingIndustry
          onboardingCityId
          onboardingCompanyId
          onboardingFactoryLotId
          onboardingShopBuildingId
          onboardingFirstSaleCompletedAtUtc
          proSubscriptionEndsAtUtc
          companies {
            id
            name
            cash
            foundedAtUtc
            foundedAtTick
          }
        }
      }
    }`)

    return data.stopAdminImpersonation
  }

  async function setPlayerInvisibleInChat(playerId: string, isInvisibleInChat: boolean) {
    const data = await gqlRequest<{ setPlayerInvisibleInChat: GameAdminPlayer }>(
        `mutation SetPlayerInvisibleInChat($input: SetPlayerInvisibleInChatInput!) {
        setPlayerInvisibleInChat(input: $input) {
          ${SESSION_PLAYER_FIELDS}
        }
      }`,
      { input: { playerId, isInvisibleInChat } },
    )

    replaceDashboardPlayer(data.setPlayerInvisibleInChat)
    return data.setPlayerInvisibleInChat
  }

  async function setLocalGameAdminRole(playerId: string, isAdmin: boolean) {
    const data = await gqlRequest<{ setLocalGameAdminRole: GameAdminPlayer }>(
        `mutation SetLocalGameAdminRole($input: SetLocalGameAdminRoleInput!) {
        setLocalGameAdminRole(input: $input) {
          ${SESSION_PLAYER_FIELDS}
        }
      }`,
      { input: { playerId, isAdmin } },
    )

    replaceDashboardPlayer(data.setLocalGameAdminRole)
    return data.setLocalGameAdminRole
  }

  async function assignGlobalGameAdminRole(email: string) {
    const data = await gqlRequest<{ assignGlobalGameAdminRole: GlobalGameAdminGrant }>(
      `mutation AssignGlobalGameAdminRole($input: ManageGlobalGameAdminRoleInput!) {
        assignGlobalGameAdminRole(input: $input) {
          id
          email
          grantedByEmail
          grantedAtUtc
          updatedAtUtc
        }
      }`,
      { input: { email } },
    )

    if (dashboard.value) {
      const remaining = dashboard.value.globalGameAdminGrants.filter((grant) => grant.email !== data.assignGlobalGameAdminRole.email)
      dashboard.value = {
        ...dashboard.value,
        globalGameAdminGrants: [...remaining, data.assignGlobalGameAdminRole].sort((left, right) => left.email.localeCompare(right.email)),
      }
    }

    return data.assignGlobalGameAdminRole
  }

  async function removeGlobalGameAdminRole(email: string) {
    await gqlRequest<{ removeGlobalGameAdminRole: boolean }>(
      `mutation RemoveGlobalGameAdminRole($input: ManageGlobalGameAdminRoleInput!) {
        removeGlobalGameAdminRole(input: $input)
      }`,
      { input: { email } },
    )

    if (dashboard.value) {
      dashboard.value = {
        ...dashboard.value,
        globalGameAdminGrants: dashboard.value.globalGameAdminGrants.filter((grant) => grant.email !== email),
      }
    }

    return true
  }

  async function upsertGamesEntry(entry: { entryId: string | null; entryType: GamesEntry['entryType']; status: GamesEntry['status']; localizations: GamesLocalization[] }) {
    const data = await gqlRequest<{ upsertGameNewsEntry: GamesEntry }>(
      `mutation UpsertGameNewsEntry($input: UpsertGameNewsEntryInput!) {
        upsertGameNewsEntry(input: $input) {
          id
          entryType
          status
          targetServerKey
          createdByEmail
          updatedByEmail
          createdAtUtc
          updatedAtUtc
          publishedAtUtc
          isRead
          localizations {
            locale
            title
            summary
            htmlContent
          }
        }
      }`,
      {
        input: {
          entryId: entry.entryId,
          entryType: entry.entryType,
          status: entry.status,
          localizations: entry.localizations,
        },
      },
    )

    return data.upsertGameNewsEntry
  }

  async function updateRealWorldBillionaire(input: {
    id: string
    rank: number
    name: string
    wealthUsd: number
  }) {
    const data = await gqlRequest<{ updateRealWorldBillionaire: RealWorldBillionaireAdminRecord }>(
      `mutation UpdateRealWorldBillionaire($input: UpdateRealWorldBillionaireInput!) {
        updateRealWorldBillionaire(input: $input) {
          id
          rank
          name
          wealthUsd
          updatedAtUtc
        }
      }`,
      { input },
    )

    if (dashboard.value) {
      const remaining = dashboard.value.realWorldBillionaires.filter((item) => item.id !== data.updateRealWorldBillionaire.id)
      dashboard.value = {
        ...dashboard.value,
        realWorldBillionaires: [...remaining, data.updateRealWorldBillionaire].sort((left, right) => left.rank - right.rank),
      }
    }

    return data.updateRealWorldBillionaire
  }

  function replaceDashboardPlayer(updatedPlayer: GameAdminPlayer) {
    if (!dashboard.value) {
      return
    }

    const existingPlayer = dashboard.value.players.find((player) => player.id === updatedPlayer.id)
    const mergedPlayer = existingPlayer ? { ...existingPlayer, ...updatedPlayer } : updatedPlayer

    dashboard.value = {
      ...dashboard.value,
      players: dashboard.value.players.map((player) => (player.id === updatedPlayer.id ? mergedPlayer : player)),
      invisiblePlayers: updatedPlayer.isInvisibleInChat
        ? [...dashboard.value.invisiblePlayers.filter((player) => player.id !== updatedPlayer.id), mergedPlayer].sort((left, right) => left.displayName.localeCompare(right.displayName))
        : dashboard.value.invisiblePlayers.filter((player) => player.id !== updatedPlayer.id),
    }
  }

  function clear() {
    session.value = null
    dashboard.value = null
    loadingSession.value = false
    loadingDashboard.value = false
    error.value = null
  }

  return {
    session,
    dashboard,
    loadingSession,
    loadingDashboard,
    error,
    fetchSession,
    fetchDashboard,
    startImpersonation,
    stopImpersonation,
    setPlayerInvisibleInChat,
    setLocalGameAdminRole,
    assignGlobalGameAdminRole,
    removeGlobalGameAdminRole,
    upsertGamesEntry,
    updateRealWorldBillionaire,
    clear,
  }
})
