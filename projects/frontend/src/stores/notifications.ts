import { defineStore } from 'pinia'
import { ref } from 'vue'

import { gqlRequest } from '@/lib/graphql'
import type { PlayerNotificationInbox, PlayerNotificationItem } from '@/types'

const NOTIFICATION_FIELDS = `
  id
  type
  severity
  title
  message
  titleKey
  bodyKey
  bodyParamsJson
  isRead
  createdAtTick
  createdAtUtc
  expiresAtUtc
  companyId
  buildingId
  buildingUnitId
  bankAccountId
  loanId
  relatedEntityType
  relatedEntityId
`

export const useNotificationsStore = defineStore('notifications', () => {
  const inbox = ref<PlayerNotificationInbox | null>(null)
  const unreadCount = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchInbox(limit = 20) {
    loading.value = true
    error.value = null

    try {
      const [itemsData, countData] = await Promise.all([
        gqlRequest<{ myNotifications: { edges: Array<{ node: PlayerNotificationItem }> } }>(
          `query MyNotifications($first: Int!, $onlyUnread: Boolean!) {
            myNotifications(first: $first, onlyUnread: $onlyUnread) {
              edges {
                node {
                  ${NOTIFICATION_FIELDS}
                }
              }
            }
          }`,
          { first: limit, onlyUnread: false },
        ),
        gqlRequest<{ notificationCount: number }>(
          `query NotificationCount {
            notificationCount
          }`,
        ),
      ])

      inbox.value = {
        unreadCount: countData.notificationCount ?? 0,
        items: itemsData.myNotifications?.edges?.map((edge) => edge.node) ?? [],
      }
      unreadCount.value = inbox.value.unreadCount
      return inbox.value
    } catch (caughtError) {
      error.value = caughtError instanceof Error ? caughtError.message : 'Failed to load notifications.'
      throw caughtError
    } finally {
      loading.value = false
    }
  }

  async function fetchUnreadCount() {
    try {
      const data = await gqlRequest<{ notificationCount: number }>(
        `query NotificationCount {
          notificationCount
        }`,
      )

      unreadCount.value = data.notificationCount ?? 0
      return unreadCount.value
    } catch (caughtError) {
      error.value = caughtError instanceof Error ? caughtError.message : 'Failed to load unread notifications.'
      throw caughtError
    }
  }

  async function markRead(notificationIds: string[]) {
    if (notificationIds.length === 0) {
      return true
    }

    await gqlRequest<{ markNotificationsRead: boolean }>(
      `mutation MarkNotificationsRead($ids: [ID!]!) {
        markNotificationsRead(ids: $ids)
      }`,
      { ids: notificationIds },
    )

    if (inbox.value) {
      const updatedItems = inbox.value.items.map((item) =>
        notificationIds.includes(item.id)
          ? {
              ...item,
              isRead: true,
            }
          : item,
      )
      inbox.value = {
        unreadCount: Math.max(0, updatedItems.filter((item) => !item.isRead).length),
        items: updatedItems,
      }
    }

    unreadCount.value = Math.max(0, unreadCount.value - notificationIds.length)
    return true
  }

  async function markAllRead() {
    const data = await gqlRequest<{ markAllNotificationsRead: boolean }>(
      `mutation MarkAllNotificationsRead {
        markAllNotificationsRead
      }`,
    )

    const changed = data.markAllNotificationsRead ? unreadCount.value : 0

    if (inbox.value) {
      inbox.value = {
        unreadCount: 0,
        items: inbox.value.items.map((item: PlayerNotificationItem) => ({
          ...item,
          isRead: true,
        })),
      }
    }

    unreadCount.value = 0
    return changed
  }

  function clear() {
    inbox.value = null
    unreadCount.value = 0
    loading.value = false
    error.value = null
  }

  return {
    inbox,
    unreadCount,
    loading,
    error,
    fetchInbox,
    fetchUnreadCount,
    markRead,
    markAllRead,
    clear,
  }
})
