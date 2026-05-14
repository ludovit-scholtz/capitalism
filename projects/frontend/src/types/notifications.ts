export interface PlayerNotificationItem {
  id: string
  type: string
  severity: 'INFO' | 'WARNING' | 'CRITICAL'
  title: string
  message: string
  titleKey?: string | null
  bodyKey?: string | null
  bodyParamsJson?: string | null
  isRead: boolean
  createdAtTick: number
  createdAtUtc: string
  expiresAtUtc?: string | null
  companyId: string | null
  buildingId: string | null
  buildingUnitId: string | null
  bankAccountId: string | null
  loanId: string | null
  relatedEntityType?: string | null
  relatedEntityId?: string | null
}

export interface PlayerNotificationInbox {
  unreadCount: number
  items: PlayerNotificationItem[]
}
