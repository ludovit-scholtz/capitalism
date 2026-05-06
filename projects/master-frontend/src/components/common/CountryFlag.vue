<script setup lang="ts">
import { computed } from 'vue'
import { getFlagSvg } from '@/lib/countryFlags'

const props = withDefaults(
  defineProps<{
    /** ISO 3166-1 alpha-2 country code (e.g. "SK", "DE"). */
    countryCode: string
    /** Accessible label / tooltip. Defaults to the country code if omitted. */
    title?: string
    /**
     * Display size preset.
     * - sm  16 × 12 px   (language picker)
     * - md  24 × 18 px   [default]
     * - lg  32 × 24 px   (large card headers)
     */
    size?: 'sm' | 'md' | 'lg'
  }>(),
  { size: 'md' },
)

const svgContent = computed(() => getFlagSvg(props.countryCode))
const label = computed(() => props.title ?? props.countryCode.toUpperCase())

const sizeClass = computed(() => {
  switch (props.size) {
    case 'sm':
      return 'country-flag--sm'
    case 'lg':
      return 'country-flag--lg'
    default:
      return 'country-flag--md'
  }
})
</script>

<template>
  <span
    v-if="svgContent"
    class="country-flag"
    :class="sizeClass"
    role="img"
    :aria-label="label"
    :title="label"
    v-html="svgContent"
  />
  <span
    v-else
    class="country-flag country-flag--fallback"
    :class="sizeClass"
    role="img"
    :aria-label="label"
    :title="label"
  >
    {{ countryCode.toUpperCase().slice(0, 2) }}
  </span>
</template>

<style scoped>
.country-flag {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  border-radius: 2px;
  overflow: hidden;
  box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.14);
  line-height: 1;
}

.country-flag--sm {
  width: 1rem;
  height: 0.75rem;
}

.country-flag--md {
  width: 1.5rem;
  height: 1.125rem;
}

.country-flag--lg {
  width: 2rem;
  height: 1.5rem;
}

.country-flag :deep(svg) {
  display: block;
  width: 100%;
  height: 100%;
}

.country-flag--fallback {
  background: var(--color-surface-raised, rgba(255, 255, 255, 0.08));
  border: 1px solid var(--color-border);
  font-size: 0.5rem;
  font-weight: 800;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}
</style>
