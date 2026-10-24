import type { RankingAdminDashboardInfo, RankingBountyDashboardItemInfo, RankingBountyDefinitionInfo, RankingEventModerationItem, RankingHistoryFilterInput, RankingLeaderboardEntryInfo, RankingRewardHistoryItem, RankingRunInfo, RankingSummaryInfo, RankingTelemetryBatchInfo } from './rankingTypes'
import { gqlRequest } from '../graphql'

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
      flaggedTelemetryBatches {
        batchId
        serverKeyMasked
        flagReasonCode
        eventCount
        isQuarantined
        hasAppliedLeaderboardImpact
        quarantineReason
        clearJustification
        createdAtUtc
        lastAttemptAtUtc
      }
    }
  }
`

const QUARANTINED_TELEMETRY_BATCHES_QUERY = `
  query QuarantinedTelemetryBatches {
    quarantinedTelemetryBatches {
      batchId
      serverKeyMasked
      flagReasonCode
      eventCount
      isQuarantined
      hasAppliedLeaderboardImpact
      quarantineReason
      clearJustification
      createdAtUtc
      lastAttemptAtUtc
    }
  }
`

const SUBMIT_RANKING_PROOF_EVENT_MUTATION = `
  mutation SubmitRankingProofEvent($bountyCode: String!, $proofReference: String!, $uniqueScopeKey: String, $idempotencyKey: String) {
    submitRankingProofEvent(
      bountyCode: $bountyCode
      proofReference: $proofReference
      uniqueScopeKey: $uniqueScopeKey
      idempotencyKey: $idempotencyKey
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

const QUARANTINE_TELEMETRY_BATCH_MUTATION = `
  mutation QuarantineTelemetryBatch($batchId: UUID!, $reason: String!) {
    quarantineTelemetryBatch(batchId: $batchId, reason: $reason) {
      batchId
      serverKeyMasked
      flagReasonCode
      eventCount
      isQuarantined
      hasAppliedLeaderboardImpact
      quarantineReason
      clearJustification
      createdAtUtc
      lastAttemptAtUtc
    }
  }
`

const CLEAR_QUARANTINE_MUTATION = `
  mutation ClearQuarantine($batchId: UUID!, $justification: String!) {
    clearQuarantine(batchId: $batchId, justification: $justification) {
      batchId
      serverKeyMasked
      flagReasonCode
      eventCount
      isQuarantined
      hasAppliedLeaderboardImpact
      quarantineReason
      clearJustification
      createdAtUtc
      lastAttemptAtUtc
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
  idempotencyKey?: string,
): Promise<RankingEventModerationItem> {
  const data = await gqlRequest<{ submitRankingProofEvent: RankingEventModerationItem }>(
    SUBMIT_RANKING_PROOF_EVENT_MUTATION,
    {
      bountyCode,
      proofReference,
      uniqueScopeKey: uniqueScopeKey ?? null,
      idempotencyKey: idempotencyKey ?? null,
    },
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

export async function fetchQuarantinedTelemetryBatches(
  token: string,
): Promise<RankingTelemetryBatchInfo[]> {
  const data = await gqlRequest<{ quarantinedTelemetryBatches: RankingTelemetryBatchInfo[] }>(
    QUARANTINED_TELEMETRY_BATCHES_QUERY,
    undefined,
    token,
  )
  return data.quarantinedTelemetryBatches
}

export async function quarantineTelemetryBatch(
  token: string,
  batchId: string,
  reason: string,
): Promise<RankingTelemetryBatchInfo> {
  const data = await gqlRequest<{ quarantineTelemetryBatch: RankingTelemetryBatchInfo }>(
    QUARANTINE_TELEMETRY_BATCH_MUTATION,
    { batchId, reason },
    token,
  )
  return data.quarantineTelemetryBatch
}

export async function clearTelemetryQuarantine(
  token: string,
  batchId: string,
  justification: string,
): Promise<RankingTelemetryBatchInfo> {
  const data = await gqlRequest<{ clearQuarantine: RankingTelemetryBatchInfo }>(
    CLEAR_QUARANTINE_MUTATION,
    { batchId, justification },
    token,
  )
  return data.clearQuarantine
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
