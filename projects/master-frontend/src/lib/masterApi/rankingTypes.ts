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

export interface RankingTelemetryBatchInfo {
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

export interface RankingAdminDashboardInfo {
  bounties: RankingBountyDefinitionInfo[]
  pendingModerationEvents: RankingEventModerationItem[]
  recentRuns: RankingRunInfo[]
  flaggedTelemetryBatches: RankingTelemetryBatchInfo[]
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
