<script setup lang="ts">
defineProps<{
  label: string
}>()
</script>

<template>
  <div class="flex items-center justify-center gap-3 rounded-2xl border border-divider bg-card px-5 py-8 text-sm text-muted sm:text-base" role="status" aria-live="polite">
    <span class="h-5 w-5 animate-spin rounded-full border-2 border-divider border-t-brand" aria-hidden="true" />
    <span>{{ label }}</span>
  </div>
</template>
