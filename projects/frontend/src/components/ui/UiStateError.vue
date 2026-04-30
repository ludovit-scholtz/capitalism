<script setup lang="ts">
defineProps<{
  message: string
  retryLabel: string
}>()

defineEmits<{
  retry: []
}>()
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-4 rounded-2xl border border-danger/30 bg-danger/10 px-5 py-8 text-center">
    <p class="text-sm font-medium text-danger sm:text-base">{{ message }}</p>
    <button class="btn btn-secondary" type="button" @click="$emit('retry')">
      {{ retryLabel }}
    </button>
  </div>
</template>
