import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { deepEqual } from '@/lib/utils'
import type { InGameChatMessage } from '@/types'

const CHAT_REFRESH_INTERVAL_MS = 3_000

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
  const route = useRoute()

  const messages = ref<InGameChatMessage[]>([])
  const loading = ref(true)
  const error = ref<string | null>(null)
  const draftMessage = ref('')
  const sendError = ref<string | null>(null)
  const sending = ref(false)
  const activeChannel = ref<'GLOBAL' | 'CITY'>('GLOBAL')
  const activeCityId = ref<string | null>(null)
  const activeCityName = ref<string | null>(null)

  let refreshTimer: ReturnType<typeof setInterval> | null = null

  const trimmedDraft = computed(() => draftMessage.value.trim())
  const charCount = computed(() => draftMessage.value.length)
  const isOverLimit = computed(() => charCount.value > MAX_CHAT_LENGTH)
  const showCharCounter = computed(() => charCount.value >= CHAR_COUNTER_THRESHOLD)
  const candidateCityId = computed(() => {
    const routeCityId = typeof route.params.id === 'string' && route.path.startsWith('/city/')
      ? route.params.id
      : null
    return routeCityId ?? auth.selectedCityId ?? null
  })
  const selectedCityId = computed(() => activeChannel.value === 'CITY' ? activeCityId.value : null)
  const inputPlaceholder = computed(() =>
    activeChannel.value === 'CITY'
      ? t('chat.placeholderCity', { city: activeCityName.value ?? t('chat.thisCity') })
      : t('chat.placeholderGlobal'),
  )

  function formatSentAt(createdAtUtc: string): string {
    return new Intl.DateTimeFormat(locale.value, {
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(createdAtUtc))
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
        `query ChatMessages($cityId: UUID, $lastN: Int!) {
          chatMessages(cityId: $cityId, lastN: $lastN) {
            id
            authorPlayerId
            authorDisplayName
            cityId
            content
            createdAtUtc
            isVisible
            isRemovedForViewer
            isOwnMessage
          }
        }`,
        { cityId: selectedCityId.value, lastN: 100 },
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
        { input: { cityId: selectedCityId.value, content: trimmedDraft.value } },
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

  async function loadActiveCityName() {
    if (!candidateCityId.value) {
      activeCityId.value = null
      activeCityName.value = null
      return
    }

    try {
      const result = await gqlRequest<{ city: { id: string; name: string } | null }>(
        `query ChatCityName($id: UUID!) {
          city(id: $id) { id name }
        }`,
        { id: candidateCityId.value },
      )
      activeCityId.value = result.city?.id ?? null
      activeCityName.value = result.city?.name ?? null
    } catch {
      activeCityId.value = null
      activeCityName.value = null
    }
  }

  function selectGlobalChannel() {
    activeChannel.value = 'GLOBAL'
  }

  function selectCityChannel() {
    if (!activeCityId.value) {
      activeChannel.value = 'GLOBAL'
      return
    }
    activeChannel.value = 'CITY'
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
    await loadActiveCityName()
    if (route.path.startsWith('/city/') && activeCityId.value) {
      activeChannel.value = 'CITY'
    }
    await loadMessages()
    startRefresh()
  })

  onUnmounted(() => {
    stopRefresh()
  })

  watch(candidateCityId, async () => {
    await loadActiveCityName()
    if (activeChannel.value === 'CITY' || route.path.startsWith('/city/')) {
      selectCityChannel()
    }
    await loadMessages(true)
  })

  watch(
    () => route.path,
    (path) => {
      if (path.startsWith('/city/') && activeCityId.value) {
        selectCityChannel()
        return
      }
      if (!path.startsWith('/city/')) {
        selectGlobalChannel()
      }
    },
  )

  watch(activeChannel, () => {
    void loadMessages(true)
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
    activeChannel,
    activeCityId,
    activeCityName,
    inputPlaceholder,
    formatSentAt,
    loadMessages,
    sendMessage,
    selectGlobalChannel,
    selectCityChannel,
    startRefresh,
    stopRefresh,
  }
}
