<script setup lang="ts">
interface SubnavItem {
  label: string
  to: string
}

defineProps<{
  items: SubnavItem[]
  ariaLabel?: string
}>()
</script>

<template>
  <nav class="container py-4" :aria-label="ariaLabel || 'Section navigation'">
    <div class="subnav-wrap">
      <RouterLink
        v-for="item in items"
        :key="`${item.to}:${item.label}`"
        :to="item.to"
        class="subnav-link"
      >
        {{ item.label }}
      </RouterLink>
    </div>
  </nav>
</template>

<style scoped>
.subnav-wrap {
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
  border: 1px solid var(--color-border);
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-surface) 88%, transparent);
  padding: 0.55rem;
}

.subnav-link {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid transparent;
  border-radius: 10px;
  padding: 0.45rem 0.85rem;
  font-size: 0.82rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  transition: all 0.14s ease;
}

.subnav-link:hover,
.subnav-link.router-link-active {
  border-color: color-mix(in srgb, var(--color-primary) 62%, var(--color-border));
  background: var(--color-primary-light);
  color: var(--color-text);
}
</style>
