import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

const LAST_SEEN_KEY = 'chat_last_seen_message_id'

export const useChatStore = defineStore('chat', () => {
  const isChatOpen = ref(false)
  /** ID of the last message the user has seen. Persisted in localStorage. */
  const lastSeenMessageId = ref<string | null>(
    typeof localStorage !== 'undefined' ? localStorage.getItem(LAST_SEEN_KEY) : null,
  )
  /** Count of new messages since the panel was last opened. */
  const unreadCount = ref(0)

  const hasUnread = computed(() => unreadCount.value > 0)

  function openChat() {
    isChatOpen.value = true
  }

  function closeChat() {
    isChatOpen.value = false
  }

  function toggleChat() {
    isChatOpen.value = !isChatOpen.value
  }

  /** Call when the user views the chat panel — marks all messages as read. */
  function markAllRead(latestMessageId: string | null) {
    if (latestMessageId) {
      lastSeenMessageId.value = latestMessageId
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(LAST_SEEN_KEY, latestMessageId)
      }
    }
    unreadCount.value = 0
  }

  /**
   * Update the unread count based on the latest known message IDs.
   * A message is "unread" when it comes after `lastSeenMessageId`.
   */
  function updateUnread(messageIds: string[]) {
    if (isChatOpen.value) {
      // Panel is visible — user can read everything, keep count at 0
      unreadCount.value = 0
      return
    }
    if (!lastSeenMessageId.value) {
      unreadCount.value = messageIds.length
      return
    }
    const idx = messageIds.indexOf(lastSeenMessageId.value)
    if (idx === -1) {
      // Unknown — could be old or cleared; treat all as unread but cap to a reasonable number
      unreadCount.value = messageIds.length
    } else {
      unreadCount.value = messageIds.length - (idx + 1)
    }
  }

  function clear() {
    isChatOpen.value = false
    unreadCount.value = 0
  }

  return {
    isChatOpen,
    unreadCount,
    hasUnread,
    lastSeenMessageId,
    openChat,
    closeChat,
    toggleChat,
    markAllRead,
    updateUnread,
    clear,
  }
})
