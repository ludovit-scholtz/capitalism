import { defineStore } from 'pinia'
import { ref } from 'vue'

import { gqlRequest } from '@/lib/graphql'
import type { GamesFeed } from '@/types'

const FEED_FIELDS = `
  unreadCount
  items {
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
`

export const usesStore = defineStore('news', () => {
  const feed = ref<GamesFeed | null>(null)
  const unreadCount = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchFeed(includeDrafts = false) {
    loading.value = true
    error.value = null

    try {
      const data = await gqlRequest<{ gameNewsFeed: GamesFeed }>(
        `query GameNewsFeed($includeDrafts: Boolean!) {
          gameNewsFeed(includeDrafts: $includeDrafts) {
            ${FEED_FIELDS}
          }
        }`,
        { includeDrafts },
      )

      const nextFeed = data.gameNewsFeed ?? { unreadCount: 0, items: [] }
      feed.value = nextFeed
      unreadCount.value = nextFeed.unreadCount
      return nextFeed
    } catch (caughtError) {
      error.value = caughtError instanceof Error ? caughtError.message : 'Failed to load news feed.'
      throw caughtError
    } finally {
      loading.value = false
    }
  }

  async function fetchUnreadCount() {
    try {
      const data = await gqlRequest<{ gameNewsFeed: Pick<GamesFeed, 'unreadCount'> | null }>(
        `query GameNewsUnreadCount($includeDrafts: Boolean!) {
          gameNewsFeed(includeDrafts: $includeDrafts) {
            unreadCount
          }
        }`,
        { includeDrafts: false },
      )

      unreadCount.value = data.gameNewsFeed?.unreadCount ?? 0
      return unreadCount.value
    } catch (caughtError) {
      error.value = caughtError instanceof Error ? caughtError.message : 'Failed to load unread news count.'
      throw caughtError
    }
  }

  async function markRead(entryIds: string[]) {
    if (entryIds.length === 0) {
      return true
    }

    await gqlRequest<{ markGameNewsRead: boolean }>(
      `mutation MarkGameNewsRead($input: MarkGameNewsReadInput!) {
        markGameNewsRead(input: $input)
      }`,
      { input: { entryIds } },
    )

    if (feed.value) {
      feed.value = {
        ...feed.value,
        items: feed.value.items.map((entry) =>
          entryIds.includes(entry.id)
            ? {
                ...entry,
                isRead: true,
              }
            : entry,
        ),
      }
    }

    unreadCount.value = Math.max(0, unreadCount.value - entryIds.length)
    return true
  }

  function clear() {
    feed.value = null
    unreadCount.value = 0
    loading.value = false
    error.value = null
  }

  return {
    feed,
    unreadCount,
    loading,
    error,
    fetchFeed,
    fetchUnreadCount,
    markRead,
    clear,
  }
})
