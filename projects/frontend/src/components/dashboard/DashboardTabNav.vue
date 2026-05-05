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
  <nav class="dashboard-tab-nav mt-4 border-b border-divider" aria-label="Dashboard sections">
    <div ref="tabList" class="tab-list flex gap-0 overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden" role="tablist">
      <button
        v-for="(tab, index) in tabs"
        :key="tab.key"
        role="tab"
        :aria-selected="modelValue === tab.key"
        :tabindex="modelValue === tab.key ? 0 : -1"
        :class="[
          'tab-btn -mb-px flex items-center whitespace-nowrap border-0 border-b-2 border-transparent bg-transparent px-4 py-2.5 text-sm font-medium text-muted transition-colors max-[480px]:px-3 max-[480px]:py-2 max-[480px]:text-[0.8125rem] hover:text-body focus-visible:rounded-t-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand focus-visible:ring-offset-2 focus-visible:ring-offset-page',
          modelValue === tab.key ? 'tab-btn--active border-b-brand font-semibold text-brand' : '',
        ]"
        @click="selectTab(tab.key)"
        @keydown="onKeyDown($event, index)"
      >
        <span class="tab-label">{{ tab.label }}</span>
        <span
          v-if="tab.badge !== undefined && tab.badge > 0"
          class="tab-badge ml-2 inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-brand px-1 text-[0.6875rem] font-bold text-white"
          aria-label="count"
        >
          {{ tab.badge }}
        </span>
      </button>
    </div>
  </nav>
</template>
