<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useChat } from '@/composables/useChat'

const { t } = useI18n()
const { messages, loading, error, draftMessage, sendError, sending, trimmedDraft, formatSentAt, sendMessage } =
  useChat()
</script>

<template>
  <section class="chat-panel mb-6 rounded-lg border border-divider bg-card p-5" aria-labelledby="dashboard-chat-title">
    <div class="chat-header flex items-start justify-between gap-4">
      <div>
        <p class="chat-eyebrow mb-1 text-xs uppercase tracking-[0.08em] text-brand">{{ t('chat.eyebrow') }}</p>
        <h2 id="dashboard-chat-title" class="m-0">{{ t('chat.title') }}</h2>
      </div>
      <span class="chat-online-indicator text-sm text-muted">{{ t('chat.sharedRoom') }}</span>
    </div>

    <p class="chat-description my-3 text-muted">{{ t('chat.description') }}</p>

    <div v-if="loading" class="chat-state py-3 text-muted">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="chat-state chat-state-error py-3 text-[#ff9b9b]" role="alert">{{ error }}</div>
    <div v-else-if="messages.length === 0" class="chat-state py-3 text-muted">{{ t('chat.empty') }}</div>
    <div v-else class="chat-log flex max-h-80 flex-col gap-3 overflow-y-auto pr-1" role="log" aria-live="polite">
      <article
        v-for="message in messages"
        :key="message.id"
        :class="['chat-message rounded-md border border-divider bg-panel-secondary p-3', { 'chat-message-own border-brand': message.isOwnMessage }]"
      >
        <div class="chat-message-meta mb-1.5 flex justify-between gap-4 text-[0.8rem] text-muted">
          <strong>{{ message.playerDisplayName }}</strong>
          <span>{{ formatSentAt(message.sentAtUtc) }}</span>
        </div>
        <p class="chat-message-body m-0 break-words whitespace-pre-wrap">{{ message.message }}</p>
      </article>
    </div>

    <form class="chat-form mt-4 flex gap-3 max-[720px]:flex-col" @submit.prevent="sendMessage">
      <label class="chat-input-wrapper flex-1">
        <span class="sr-only">{{ t('chat.inputLabel') }}</span>
        <input
          v-model="draftMessage"
          class="chat-input min-w-0 w-full"
          type="text"
          maxlength="300"
          :placeholder="t('chat.placeholder')"
          :aria-label="t('chat.inputLabel')"
        />
      </label>
      <button class="btn btn-primary chat-send-button" type="submit" :disabled="sending || !trimmedDraft">
        {{ sending ? t('common.saving') : t('chat.send') }}
      </button>
    </form>

    <p v-if="sendError" class="chat-state chat-state-error py-3 text-[#ff9b9b]" role="alert">{{ sendError }}</p>
  </section>
</template>
