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
.tutorial-tooltip {
  position: absolute;
  z-index: 500;
}

.tutorial-tooltip--bottom {
  top: calc(100% + 8px);
  left: 0;
}

.tutorial-tooltip--top {
  bottom: calc(100% + 8px);
  left: 0;
}

.tutorial-tooltip--left {
  right: calc(100% + 8px);
  top: 0;
}

.tutorial-tooltip--right {
  left: calc(100% + 8px);
  top: 0;
}

.tutorial-tooltip__overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.35);
  z-index: -1;
}

.tutorial-tooltip__card {
  background: var(--color-card, #1e293b);
  border: 1px solid var(--color-accent, #6366f1);
  border-radius: 10px;
  padding: 14px 16px;
  max-width: 300px;
  min-width: 200px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
}

.tutorial-tooltip__header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.tutorial-tooltip__icon {
  font-size: 1.1rem;
  flex-shrink: 0;
}

.tutorial-tooltip__title {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--color-text-primary, #f1f5f9);
  line-height: 1.3;
}

.tutorial-tooltip__body {
  font-size: 0.82rem;
  color: var(--color-text-secondary, #94a3b8);
  line-height: 1.5;
  margin: 0 0 12px;
}

.tutorial-tooltip__footer {
  display: flex;
  justify-content: flex-end;
}

.tutorial-tooltip__dismiss-btn {
  padding: 5px 14px;
  font-size: 0.82rem;
  font-weight: 600;
  background: var(--color-accent, #6366f1);
  color: #fff;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: opacity 0.15s;
}

.tutorial-tooltip__dismiss-btn:hover {
  opacity: 0.85;
}

/* Fade animation */
.tt-fade-enter-active,
.tt-fade-leave-active {
  transition: opacity 0.25s ease, transform 0.25s ease;
}

.tt-fade-enter-from,
.tt-fade-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>
