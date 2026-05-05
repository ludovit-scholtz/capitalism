export interface PlayerNotificationItem {
  id: string
  type: string
  title: string
  message: string
  isRead: boolean
  createdAtTick: number
  createdAtUtc: string
  companyId: string | null
  buildingId: string | null
  buildingUnitId: string | null
  bankAccountId: string | null
  loanId: string | null
}

export interface PlayerNotificationInbox {
  unreadCount: number
  items: PlayerNotificationItem[]
}
