<script setup lang="ts">
import { computed } from 'vue'
import type { HorizontalLinkState, VerticalLinkState } from '@/lib/linkHelpers'

const props = defineProps<{
  state: HorizontalLinkState | VerticalLinkState
  orientation: 'horizontal' | 'vertical'
  active: boolean
}>()

const isHorizontal = computed(() => props.orientation === 'horizontal')
const showStartArrow = computed(() => props.state === 'backward' || props.state === 'both')
const showEndArrow = computed(() => props.state === 'forward' || props.state === 'both')
const lineProps = computed(() =>
  isHorizontal.value
    ? { x1: 2, y1: 8, x2: 14, y2: 8 }
    : { x1: 8, y1: 2, x2: 8, y2: 14 },
)
const startArrowPoints = computed(() =>
  isHorizontal.value ? '2,8 6,5 6,11' : '8,2 5,6 11,6',
)
const endArrowPoints = computed(() =>
  isHorizontal.value ? '14,8 10,5 10,11' : '8,14 5,10 11,10',
)
const strokeColor = computed(() => (props.active ? '#3b82f6' : '#9ca3af'))
</script>

<template>
  <svg class="link-arrow-svg" viewBox="0 0 16 16" aria-hidden="true">
    <line
      class="link-arrow-line"
      :class="{ 'link-arrow-line-active': active, 'link-arrow-line-inactive': !active }"
      v-bind="lineProps"
      :stroke="strokeColor"
    />
    <polygon v-if="showStartArrow" class="link-arrow-head" :class="{ inactive: !active }" :points="startArrowPoints" :fill="strokeColor" />
    <polygon v-if="showEndArrow" class="link-arrow-head" :class="{ inactive: !active }" :points="endArrowPoints" :fill="strokeColor" />
  </svg>
</template>

<style scoped>
.link-arrow-svg {
  width: 100%;
  height: 100%;
}

.link-arrow-line {
  fill: none;
  stroke-width: 2;
}

.link-arrow-line-inactive {
  stroke-dasharray: 2 2;
}

.link-arrow-head.inactive {
  opacity: 0.85;
}
</style>
