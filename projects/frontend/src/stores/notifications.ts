import { defineStore } from 'pinia'
import { ref } from 'vue'

import { gqlRequest } from '@/lib/graphql'
import type { PlayerNotificationInbox, PlayerNotificationItem } from '@/types'

const NOTIFICATION_FIELDS = `
  id
  type
  title
  message
  isRead
  createdAtTick
  createdAtUtc
  companyId
  buildingId
  buildingUnitId
  bankAccountId
  loanId
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
      const data = await gqlRequest<{ playerNotificationInbox: PlayerNotificationInbox }>(
        `query PlayerNotificationInbox($limit: Int!) {
          playerNotificationInbox(limit: $limit) {
            unreadCount
            items {
              ${NOTIFICATION_FIELDS}
            }
          }
        }`,
        { limit },
      )

      inbox.value = data.playerNotificationInbox ?? { unreadCount: 0, items: [] }
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
      const data = await gqlRequest<{ playerNotificationUnreadCount: number }>(
        `query PlayerNotificationUnreadCount {
          playerNotificationUnreadCount
        }`,
      )

      unreadCount.value = data.playerNotificationUnreadCount ?? 0
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

    await gqlRequest<{ markPlayerNotificationsRead: boolean }>(
      `mutation MarkPlayerNotificationsRead($input: MarkPlayerNotificationsReadInput!) {
        markPlayerNotificationsRead(input: $input)
      }`,
      { input: { notificationIds } },
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
    const data = await gqlRequest<{ markAllPlayerNotificationsRead: number }>(
      `mutation MarkAllPlayerNotificationsRead {
        markAllPlayerNotificationsRead
      }`,
    )

    const changed = data.markAllPlayerNotificationsRead ?? 0

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
