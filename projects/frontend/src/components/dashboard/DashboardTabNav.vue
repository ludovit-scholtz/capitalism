<script setup lang="ts">
import { useTemplateRef } from 'vue'

export interface DashboardTab {
  key: string
  label: string
  badge?: number
}

const props = defineProps<{
  modelValue: string
  tabs: DashboardTab[]
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const tabListRef = useTemplateRef<HTMLElement>('tabList')

function selectTab(key: string) {
  emit('update:modelValue', key)
}

function onKeyDown(event: KeyboardEvent, index: number) {
  const total = props.tabs.length
  if (event.key === 'ArrowRight') {
    event.preventDefault()
    const next = (index + 1) % total
    const tab = props.tabs[next]
    if (tab) {
      selectTab(tab.key)
      focusTab(next)
    }
  } else if (event.key === 'ArrowLeft') {
    event.preventDefault()
    const prev = (index - 1 + total) % total
    const tab = props.tabs[prev]
    if (tab) {
      selectTab(tab.key)
      focusTab(prev)
    }
  } else if (event.key === 'Home') {
    event.preventDefault()
    const first = props.tabs[0]
    if (first) {
      selectTab(first.key)
      focusTab(0)
    }
  } else if (event.key === 'End') {
    event.preventDefault()
    const last = props.tabs[total - 1]
    if (last) {
      selectTab(last.key)
      focusTab(total - 1)
    }
  }
}

function focusTab(index: number) {
  const el = tabListRef.value
  if (!el) return
  const buttons = el.querySelectorAll<HTMLButtonElement>('[role="tab"]')
  const btn = buttons[index]
  if (btn) btn.focus()
}
</script>

<template>
  <nav class="dashboard-tab-nav" aria-label="Dashboard sections">
    <div ref="tabList" class="tab-list" role="tablist">
      <button
        v-for="(tab, index) in tabs"
        :key="tab.key"
        role="tab"
        :aria-selected="modelValue === tab.key"
        :tabindex="modelValue === tab.key ? 0 : -1"
        :class="['tab-btn', { 'tab-btn--active': modelValue === tab.key }]"
        @click="selectTab(tab.key)"
        @keydown="onKeyDown($event, index)"
      >
        <span class="tab-label">{{ tab.label }}</span>
        <span v-if="tab.badge !== undefined && tab.badge > 0" class="tab-badge" aria-label="count">
          {{ tab.badge }}
        </span>
      </button>
    </div>
  </nav>
</template>

<style scoped>
.dashboard-tab-nav {
  margin: 1rem 0 0;
  border-bottom: 1px solid var(--color-border);
}

.tab-list {
  display: flex;
  gap: 0;
  overflow-x: auto;
  scrollbar-width: none;
}

.tab-list::-webkit-scrollbar {
  display: none;
}

.tab-btn {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.625rem 1rem;
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  color: var(--color-text-secondary);
  font-size: 0.875rem;
  font-weight: 500;
  cursor: pointer;
  white-space: nowrap;
  transition:
    color 0.15s,
    border-color 0.15s;
  margin-bottom: -1px;
}

.tab-btn:hover {
  color: var(--color-text);
}

.tab-btn:focus-visible {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
  border-radius: 2px 2px 0 0;
}

.tab-btn--active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
  font-weight: 600;
}

.tab-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 1.25rem;
  height: 1.25rem;
  padding: 0 0.3rem;
  background: var(--color-primary);
  color: #fff;
  font-size: 0.6875rem;
  font-weight: 700;
  border-radius: 999px;
}

@media (max-width: 480px) {
  .tab-btn {
    padding: 0.5rem 0.75rem;
    font-size: 0.8125rem;
  }
}
</style>
