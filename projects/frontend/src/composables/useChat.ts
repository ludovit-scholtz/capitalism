import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { deepEqual } from '@/lib/utils'
import type { InGameChatMessage } from '@/types'

const CHAT_REFRESH_INTERVAL_MS = 10_000

/** Maximum character length for a single chat message (enforced server-side too). */
export const MAX_CHAT_LENGTH = 500

/** Character count threshold above which the counter widget becomes visible. */
export const CHAR_COUNTER_THRESHOLD = 450

/**
 * Shared composable that encapsulates chat message loading and sending.
 *
 * - Call `startRefresh` / `stopRefresh` to control the polling timer.
 * - The composable mounts / unmounts the timer automatically via lifecycle hooks.
 */
export function useChat() {
  const { t, locale } = useI18n()
  const auth = useAuthStore()

  const messages = ref<InGameChatMessage[]>([])
  const loading = ref(true)
  const error = ref<string | null>(null)
  const draftMessage = ref('')
  const sendError = ref<string | null>(null)
  const sending = ref(false)

  let refreshTimer: ReturnType<typeof setInterval> | null = null

  const trimmedDraft = computed(() => draftMessage.value.trim())
  const charCount = computed(() => draftMessage.value.length)
  const isOverLimit = computed(() => charCount.value > MAX_CHAT_LENGTH)
  const showCharCounter = computed(() => charCount.value >= CHAR_COUNTER_THRESHOLD)

  function formatSentAt(sentAtUtc: string): string {
    return new Intl.DateTimeFormat(locale.value, {
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(sentAtUtc))
  }

  async function loadMessages(isRefresh = false) {
    if (!auth.isAuthenticated) {
      loading.value = false
      return
    }

    if (!isRefresh) {
      loading.value = true
    }

    error.value = null

    try {
      const data = await gqlRequest<{ chatMessages: InGameChatMessage[] }>(
        `query ChatMessages($limit: Int!) {
          chatMessages(limit: $limit) {
            id
            playerId
            playerDisplayName
            message
            sentAtUtc
            isOwnMessage
          }
        }`,
        { limit: 50 },
      )

      if (!deepEqual(messages.value, data.chatMessages)) {
        messages.value = data.chatMessages
      }
    } catch (reason: unknown) {
      error.value = reason instanceof Error ? reason.message : t('chat.loadFailed')
    } finally {
      if (!isRefresh) {
        loading.value = false
      }
    }
  }

  async function sendMessage() {
    if (!trimmedDraft.value || isOverLimit.value) {
      return
    }

    sending.value = true
    sendError.value = null

    try {
      await gqlRequest<{ sendChatMessage: InGameChatMessage }>(
        `mutation SendChatMessage($input: SendChatMessageInput!) {
          sendChatMessage(input: $input) {
            id
          }
        }`,
        { input: { message: trimmedDraft.value } },
      )
      draftMessage.value = ''
      await loadMessages(true)
    } catch (reason: unknown) {
      if (reason instanceof GraphQLError && reason.code === 'RATE_LIMITED') {
        sendError.value = t('chat.rateLimited')
      } else if (reason instanceof GraphQLError && reason.code === 'MESSAGE_TOO_LONG') {
        sendError.value = t('chat.messageTooLong')
      } else {
        sendError.value = reason instanceof Error ? reason.message : t('chat.sendFailed')
      }
    } finally {
      sending.value = false
    }
  }

  function startRefresh() {
    if (refreshTimer) return
    refreshTimer = setInterval(() => {
      void loadMessages(true)
    }, CHAT_REFRESH_INTERVAL_MS)
  }

  function stopRefresh() {
    if (refreshTimer) {
      clearInterval(refreshTimer)
      refreshTimer = null
    }
  }

  onMounted(async () => {
    await loadMessages()
    startRefresh()
  })

  onUnmounted(() => {
    stopRefresh()
  })

  return {
    messages,
    loading,
    error,
    draftMessage,
    sendError,
    sending,
    trimmedDraft,
    charCount,
    isOverLimit,
    showCharCounter,
    formatSentAt,
    loadMessages,
    sendMessage,
    startRefresh,
    stopRefresh,
  }
}
