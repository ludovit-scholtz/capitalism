<script setup lang="ts">
export type PlayerGender = 'MALE' | 'FEMALE' | 'UNSPECIFIED'

const props = defineProps<{
  modelValue: PlayerGender
  femaleLabel: string
  maleLabel: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: PlayerGender): void
}>()

function selectGender(gender: 'FEMALE' | 'MALE') {
  emit('update:modelValue', gender)
}
</script>

<template>
  <div class="gender-picker" role="radiogroup">
    <button
      type="button"
      class="gender-option gender-option-female"
      :class="{ 'is-active': props.modelValue === 'FEMALE' }"
      role="radio"
      :aria-checked="props.modelValue === 'FEMALE'"
      :aria-label="femaleLabel"
      @click="selectGender('FEMALE')"
    >
      <span aria-hidden="true">♀</span>
      <span>{{ femaleLabel }}</span>
    </button>
    <button
      type="button"
      class="gender-option gender-option-male"
      :class="{ 'is-active': props.modelValue === 'MALE' }"
      role="radio"
      :aria-checked="props.modelValue === 'MALE'"
      :aria-label="maleLabel"
      @click="selectGender('MALE')"
    >
      <span aria-hidden="true">♂</span>
      <span>{{ maleLabel }}</span>
    </button>
  </div>
</template>

<style scoped>
.gender-picker {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.5rem;
}

.gender-option {
  border: 1px solid var(--color-divider);
  background: var(--color-surface);
  color: var(--color-text-primary);
  border-radius: 0.75rem;
  padding: 0.625rem 0.75rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  font-size: 0.875rem;
  font-weight: 600;
  transition: transform 0.18s ease, border-color 0.18s ease, background-color 0.18s ease,
    box-shadow 0.18s ease;
}

.gender-option:hover {
  transform: translateY(-1px) scale(1.01);
}

.gender-option.is-active {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 14%, var(--color-surface) 86%);
  box-shadow: 0 0 0 1px var(--color-primary), 0 0 0 6px color-mix(in srgb, var(--color-primary) 18%, transparent 82%);
  animation: pick-pulse 220ms ease-out;
}

@keyframes pick-pulse {
  0% {
    transform: scale(1);
  }
  45% {
    transform: scale(1.06);
  }
  100% {
    transform: scale(1);
  }
}
</style>
