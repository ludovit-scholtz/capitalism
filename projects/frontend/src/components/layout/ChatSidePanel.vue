<script setup lang="ts">
import { watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useChatStore } from '@/stores/chat'
import { useChat } from '@/composables/useChat'

const { t } = useI18n()
const chatStore = useChatStore()

const { messages, loading, error, draftMessage, sendError, sending, trimmedDraft, formatSentAt, sendMessage } =
  useChat()

// When new messages arrive, update the unread count in the store
watch(messages, (newMessages) => {
  const ids = newMessages.map((m) => m.id)
  chatStore.updateUnread(ids)
  // If panel is open, mark all as read immediately
  if (chatStore.isChatOpen) {
    const last = ids[ids.length - 1] ?? null
    chatStore.markAllRead(last)
  }
})

// When panel opens, mark all current messages as read
watch(
  () => chatStore.isChatOpen,
  (open) => {
    if (open) {
      const last = messages.value.map((m) => m.id).pop() ?? null
      chatStore.markAllRead(last)
    }
  },
)
</script>

<template>
  <Teleport to="body">
    <!-- Backdrop (mobile only) -->
    <Transition name="backdrop">
      <div
        v-if="chatStore.isChatOpen"
        class="chat-backdrop"
        aria-hidden="true"
        @click="chatStore.closeChat()"
      />
    </Transition>

    <!-- Side panel -->
    <Transition name="slide">
      <aside
        v-if="chatStore.isChatOpen"
        class="chat-side-panel"
        role="complementary"
        :aria-label="t('chat.title')"
      >
        <!-- Panel header -->
        <div class="panel-header">
          <div class="panel-title-row">
            <span class="panel-eyebrow">{{ t('chat.eyebrow') }}</span>
            <button
              class="close-btn"
              :aria-label="t('chat.closeChat')"
              @click="chatStore.closeChat()"
            >
              <font-awesome-icon :icon="['fas', 'times']" />
            </button>
          </div>
          <h2 class="panel-title">{{ t('chat.title') }}</h2>
          <span class="panel-subtitle">{{ t('chat.sharedRoom') }}</span>
        </div>

        <!-- Message log -->
        <div class="panel-body">
          <div v-if="loading" class="chat-state">{{ t('common.loading') }}</div>
          <div v-else-if="error" class="chat-state chat-state-error" role="alert">{{ error }}</div>
          <div v-else-if="messages.length === 0" class="chat-state chat-state-empty">
            {{ t('chat.empty') }}
          </div>
          <div v-else class="chat-log" role="log" aria-live="polite">
            <article
              v-for="message in messages"
              :key="message.id"
              :class="['chat-message', { 'chat-message-own': message.isOwnMessage }]"
            >
              <div class="chat-message-meta">
                <strong>{{ message.playerDisplayName }}</strong>
                <span>{{ formatSentAt(message.sentAtUtc) }}</span>
              </div>
              <p class="chat-message-body">{{ message.message }}</p>
            </article>
          </div>
        </div>

        <!-- Composer pinned at bottom -->
        <div class="panel-footer">
          <p v-if="sendError" class="chat-state chat-state-error" role="alert">{{ sendError }}</p>
          <form class="chat-form" @submit.prevent="sendMessage">
            <label class="chat-input-wrapper">
              <span class="sr-only">{{ t('chat.inputLabel') }}</span>
              <input
                v-model="draftMessage"
                class="chat-input"
                type="text"
                maxlength="300"
                :placeholder="t('chat.placeholder')"
                :aria-label="t('chat.inputLabel')"
              />
            </label>
            <button
              class="btn btn-primary chat-send-button"
              type="submit"
              :disabled="sending || !trimmedDraft"
            >
              {{ sending ? t('common.saving') : t('chat.send') }}
            </button>
          </form>
        </div>
      </aside>
    </Transition>
  </Teleport>
</template>

<style scoped>
/* ──────────────────────────────────────────────────────────────────────────
   Backdrop (mobile-only)
────────────────────────────────────────────────────────────────────────── */
.chat-backdrop {
  display: none;
}

@media (max-width: 767px) {
  .chat-backdrop {
    display: block;
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.55);
    z-index: 199;
  }
}

.backdrop-enter-active,
.backdrop-leave-active {
  transition: opacity 0.2s ease;
}
.backdrop-enter-from,
.backdrop-leave-to {
  opacity: 0;
}

/* ──────────────────────────────────────────────────────────────────────────
   Side panel shell
────────────────────────────────────────────────────────────────────────── */
.chat-side-panel {
  position: fixed;
  top: 64px; /* below the sticky app-header */
  right: 0;
  bottom: 0;
  z-index: 200;
  width: 380px;
  max-width: 100vw;
  background: var(--color-surface);
  border-left: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
  box-shadow: -4px 0 24px rgba(0, 0, 0, 0.18);
}

/* Full-screen on small devices */
@media (max-width: 767px) {
  .chat-side-panel {
    top: 0;
    width: 100vw;
    border-left: none;
    box-shadow: none;
  }
}

/* ──────────────────────────────────────────────────────────────────────────
   Slide transition
────────────────────────────────────────────────────────────────────────── */
.slide-enter-active,
.slide-leave-active {
  transition: transform 0.25s ease;
}
.slide-enter-from,
.slide-leave-to {
  transform: translateX(100%);
}

/* ──────────────────────────────────────────────────────────────────────────
   Panel header
────────────────────────────────────────────────────────────────────────── */
.panel-header {
  padding: 1rem 1.25rem 0.75rem;
  border-bottom: 1px solid var(--color-border);
  flex-shrink: 0;
}

.panel-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.1rem;
}

.panel-eyebrow {
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--color-primary);
  font-weight: 600;
}

.panel-title {
  margin: 0.15rem 0 0.15rem;
  font-size: 1.05rem;
  font-weight: 700;
  color: var(--color-text);
}

.panel-subtitle {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
}

.close-btn {
  background: none;
  border: none;
  color: var(--color-text-secondary);
  cursor: pointer;
  padding: 0.35rem;
  border-radius: var(--radius-sm);
  font-size: 1rem;
  line-height: 1;
  transition: background-color 0.15s;
}
.close-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text);
}

/* ──────────────────────────────────────────────────────────────────────────
   Scrollable message body
────────────────────────────────────────────────────────────────────────── */
.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 0.75rem 1.25rem;
}

.chat-log {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
}

.chat-message {
  background: var(--color-surface-secondary);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 0.65rem 0.875rem;
}

.chat-message-own {
  border-color: var(--color-primary);
}

.chat-message-meta {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 0.25rem;
  color: var(--color-text-secondary);
  font-size: 0.775rem;
}

.chat-message-body {
  margin: 0;
  font-size: 0.875rem;
  white-space: pre-wrap;
  word-break: break-word;
  color: var(--color-text);
}

.chat-state {
  padding: 0.75rem 0;
  color: var(--color-text-secondary);
  font-size: 0.875rem;
}

.chat-state-empty {
  text-align: center;
  padding: 2rem 0;
}

.chat-state-error {
  color: #ff9b9b;
}

/* ──────────────────────────────────────────────────────────────────────────
   Pinned composer
────────────────────────────────────────────────────────────────────────── */
.panel-footer {
  padding: 0.75rem 1.25rem;
  border-top: 1px solid var(--color-border);
  flex-shrink: 0;
  /* Safe area for notched/gesture-bar phones */
  padding-bottom: calc(0.75rem + env(safe-area-inset-bottom, 0px));
  background: var(--color-surface);
}

.chat-form {
  display: flex;
  gap: 0.625rem;
}

.chat-input-wrapper {
  flex: 1;
}

.chat-input {
  width: 100%;
  min-width: 0;
}

.chat-send-button {
  flex-shrink: 0;
}

/* Stack vertically on very narrow screens */
@media (max-width: 400px) {
  .chat-form {
    flex-direction: column;
  }
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
</style>
