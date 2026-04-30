<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

defineProps<{
  step: number
}>()

const { t } = useI18n()

const steps = computed(() => [
  { number: 1, labelKey: 'onboarding.step1Title' },
  { number: 2, labelKey: 'onboarding.step2Title' },
  { number: 3, labelKey: 'onboarding.step3Title' },
  { number: 4, labelKey: 'onboarding.step4Title' },
  { number: 5, labelKey: 'onboarding.step5Title' },
  { number: 6, labelKey: 'onboarding.step6Title' },
])
</script>

<template>
  <div class="flex items-start mb-10 px-4 overflow-x-auto">
    <template v-for="(entry, index) in steps" :key="entry.number">
      <div class="progress-segment flex min-w-20 flex-col items-center gap-2" :class="{ active: step >= entry.number, done: step > entry.number }">
        <div
          class="progress-step flex h-10 w-10 items-center justify-center rounded-full border-2 border-divider bg-surface text-[0.9375rem] font-bold text-muted transition-all duration-300"
          :class="{
            'border-brand bg-brand text-white shadow-[0_0_16px_rgba(0,71,255,0.3)]': step >= entry.number,
            'border-[var(--color-success)] bg-[var(--color-success)] text-white shadow-[0_0_12px_rgba(0,200,83,0.3)]': step > entry.number,
          }"
        >
          <span v-if="step > entry.number" class="check-icon text-lg">✓</span>
          <span v-else>{{ entry.number }}</span>
        </div>
        <span class="progress-label max-w-[100px] text-center text-[0.6875rem] leading-[1.2] text-muted max-[640px]:hidden" :class="{ 'font-medium text-body': step >= entry.number }">
          {{ t(entry.labelKey) }}
        </span>
      </div>

      <div
        v-if="index < steps.length - 1"
        class="progress-line mt-5 h-0.5 flex-1 bg-divider transition-colors duration-300"
        :class="{ 'bg-gradient-to-r from-[var(--color-success)] to-brand': step > entry.number }"
      ></div>
    </template>
  </div>
</template>
