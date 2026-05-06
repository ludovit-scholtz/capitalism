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
     * - sm  16 × 12 px   (inline / language picker)
     * - md  24 × 18 px   (navbar / city cards)  [default]
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
  <!-- Inline SVG flag rendered from the country-flag-icons library. -->
  <span
    v-if="svgContent"
    class="country-flag"
    :class="sizeClass"
    role="img"
    :aria-label="label"
    :title="label"
    v-html="svgContent"
  />
  <!-- Fallback: show a text badge when no flag SVG is available. -->
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
  /* Subtle border so flags are visible on light and dark backgrounds */
  box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.14);
  line-height: 1;
}

/* Size presets */
.country-flag--sm {
  width: 1rem; /* 16 px */
  height: 0.75rem; /* 12 px */
}

.country-flag--md {
  width: 1.5rem; /* 24 px */
  height: 1.125rem; /* 18 px */
}

.country-flag--lg {
  width: 2rem; /* 32 px */
  height: 1.5rem; /* 24 px */
}

/* Make the injected <svg> fill its container. */
.country-flag :deep(svg) {
  display: block;
  width: 100%;
  height: 100%;
}

/* Fallback text badge */
.country-flag--fallback {
  background: var(--color-surface-raised, rgba(255, 255, 255, 0.08));
  border: 1px solid var(--color-border);
  font-size: 0.5rem;
  font-weight: 800;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}
</style>
