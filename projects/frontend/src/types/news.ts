import type { PlayerRole, AccountContextType } from './auth'

export interface GamesFeed {
  unreadCount: number
  items: GamesEntry[]
}

export interface GamesEntry {
  id: string
  entryType: 'NEWS' | 'CHANGELOG' | 'MARKET_REPORT'
  status: 'DRAFT' | 'PUBLISHED'
  targetServerKey: string | null
  createdByEmail: string
  updatedByEmail: string
  createdAtUtc: string
  updatedAtUtc: string
  publishedAtUtc: string | null
  isRead: boolean
  localizations: GamesLocalization[]
}

export interface GamesLocalization {
  locale: string
  title: string
  summary: string
  htmlContent: string
}

export interface GameAdminCompany {
  id: string
  name: string
  cash: number
}

export interface GameAdminPlayer {
  id: string
  email: string
  displayName: string
  role: PlayerRole
  isInvisibleInChat: boolean
  createdAtUtc?: string
  lastLoginAtUtc: string | null
  personalCash: number
  totalCompanyCash: number
  totalCompanyEquity?: number
  companyCount: number
  cityNames?: string[]
  companies: GameAdminCompany[]
}

export interface GameAdminSession {
  isLocalAdmin: boolean
  hasGlobalAdminRole: boolean
  isRootAdministrator: boolean
  canAccessAdminDashboard: boolean
  isImpersonating: boolean
  adminActor: GameAdminPlayer
  effectivePlayer: GameAdminPlayer | null
  effectiveAccountType: AccountContextType
  effectiveCompanyId: string | null
  effectiveCompanyName: string | null
}

export interface GameAdminMoneyInflowSummary {
  category: string
  amount: number
  description: string
}

export interface GameAdminShippingCostSummary {
  companyId: string
  companyName: string
  amount: number
  entryCount: number
}

export interface GameAdminMultiAccountAlert {
  reason: string
  exposureAmount: number
  confidenceScore: number
  supportingEntityType: string
  supportingEntityName: string
  primaryPlayer: GameAdminPlayer
  relatedPlayer: GameAdminPlayer
}

export interface GlobalGameAdminGrant {
  id: string
  email: string
  grantedByEmail: string
  grantedAtUtc: string
  updatedAtUtc: string
}

export interface GameAdminAuditLog {
  id: string
  adminActorPlayerId: string
  adminActorEmail: string
  adminActorDisplayName: string
  effectivePlayerId: string
  effectivePlayerEmail: string
  effectivePlayerDisplayName: string
  effectiveAccountType: AccountContextType
  effectiveCompanyId: string | null
  effectiveCompanyName: string | null
  graphQlOperationName: string
  mutationSummary: string
  responseStatusCode: number
  recordedAtUtc: string
}

export interface GameAdminDashboard {
  serverKey: string
  totalPersonalCash: number
  totalCompanyCash: number
  moneySupply: number
  externalMoneyInflowLast100Ticks: number
  totalShippingCostsLast100Ticks: number
  inflowSummaries: GameAdminMoneyInflowSummary[]
  shippingCostSummaries: GameAdminShippingCostSummary[]
  multiAccountAlerts: GameAdminMultiAccountAlert[]
  players: GameAdminPlayer[]
  invisiblePlayers: GameAdminPlayer[]
  globalGameAdminGrants: GlobalGameAdminGrant[]
  recentAuditLogs: GameAdminAuditLog[]
  /** The government system account, separated from regular players for admin impersonation. */
  governmentPlayer: GameAdminPlayer | null
}
