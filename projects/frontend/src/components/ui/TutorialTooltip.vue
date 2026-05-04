<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'

const props = withDefaults(
  defineProps<{
    /** Tutorial milestone identifier this tooltip belongs to (used for persistence). */
    milestone: string
    /** Tooltip title (i18n key resolved before passing in, max ~60 chars). */
    title: string
    /** Tooltip description body (i18n key resolved before passing in, max 100 chars). */
    description: string
    /** Preferred position of the tooltip relative to its anchor element. */
    position?: 'top' | 'bottom' | 'left' | 'right'
    /** Whether the tooltip is currently visible. Controlled by parent. */
    visible?: boolean
  }>(),
  {
    position: 'bottom',
    visible: true,
  },
)

const emit = defineEmits<{
  /** Emitted when the player dismisses the tooltip via "Got it" or Escape. */
  dismiss: [milestone: string]
}>()

const { t } = useI18n()

const dismissed = ref(false)
let autoTimer: ReturnType<typeof setTimeout> | null = null

function handleDismiss() {
  dismissed.value = true
  if (autoTimer) {
    clearTimeout(autoTimer)
    autoTimer = null
  }
  emit('dismiss', props.milestone)
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    handleDismiss()
  }
}

onMounted(() => {
  document.addEventListener('keydown', handleKeydown)
  // Auto-dismiss after 30 seconds of inactivity
  autoTimer = setTimeout(() => {
    if (!dismissed.value) {
      handleDismiss()
    }
  }, 30_000)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeydown)
  if (autoTimer) {
    clearTimeout(autoTimer)
    autoTimer = null
  }
})
</script>

<template>
  <Transition name="tt-fade">
    <div
      v-if="visible && !dismissed"
      :class="['tutorial-tooltip', `tutorial-tooltip--${position}`]"
      role="dialog"
      :aria-label="title"
      aria-live="polite"
    >
      <!-- Overlay backdrop (optional; only on first-visit) -->
      <div class="tutorial-tooltip__overlay" aria-hidden="true" @click="handleDismiss" />

      <div class="tutorial-tooltip__card">
        <div class="tutorial-tooltip__header">
          <span class="tutorial-tooltip__icon" aria-hidden="true">💡</span>
          <strong class="tutorial-tooltip__title">{{ title }}</strong>
        </div>
        <p class="tutorial-tooltip__body">{{ description }}</p>
        <div class="tutorial-tooltip__footer">
          <button
            class="tutorial-tooltip__dismiss-btn"
            type="button"
            @click="handleDismiss"
          >
            {{ t('tutorial.gotIt') }}
          </button>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
/* ── Fullscreen fixed overlay wrapper ────────────────────────────────────── */
.tutorial-tooltip {
  position: fixed;
  inset: 0;
  z-index: 900;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
}

/* Semi-transparent glassmorphic backdrop */
.tutorial-tooltip__overlay {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(2px);
  cursor: pointer;
}

/* Centered content card */
.tutorial-tooltip__card {
  position: relative;
  z-index: 1;
  background: var(--color-card, #1e293b);
  border: 1px solid var(--color-accent, #6366f1);
  border-radius: 14px;
  padding: 24px 28px;
  max-width: 440px;
  width: 100%;
  box-shadow:
    0 4px 6px -1px rgba(0, 0, 0, 0.3),
    0 20px 48px rgba(0, 0, 0, 0.5),
    inset 0 1px 0 rgba(255, 255, 255, 0.05);
}

.tutorial-tooltip__header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 12px;
}

.tutorial-tooltip__icon {
  font-size: 1.4rem;
  flex-shrink: 0;
}

.tutorial-tooltip__title {
  font-size: 1.05rem;
  font-weight: 700;
  color: var(--color-text-primary, #f1f5f9);
  line-height: 1.3;
}

.tutorial-tooltip__body {
  font-size: 0.88rem;
  color: var(--color-text-secondary, #94a3b8);
  line-height: 1.6;
  margin: 0 0 20px;
}

.tutorial-tooltip__footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.tutorial-tooltip__dismiss-btn {
  padding: 8px 22px;
  font-size: 0.9rem;
  font-weight: 600;
  background: var(--color-accent, #6366f1);
  color: #fff;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  transition: opacity 0.15s, transform 0.1s;
}

.tutorial-tooltip__dismiss-btn:hover {
  opacity: 0.88;
  transform: translateY(-1px);
}

.tutorial-tooltip__dismiss-btn:focus-visible {
  outline: 2px solid var(--color-accent, #6366f1);
  outline-offset: 3px;
}

/* Fade animation */
.tt-fade-enter-active,
.tt-fade-leave-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.tt-fade-enter-active .tutorial-tooltip__card,
.tt-fade-leave-active .tutorial-tooltip__card {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.tt-fade-enter-from,
.tt-fade-leave-to {
  opacity: 0;
}

.tt-fade-enter-from .tutorial-tooltip__card,
.tt-fade-leave-to .tutorial-tooltip__card {
  opacity: 0;
  transform: scale(0.95) translateY(8px);
}
</style>
