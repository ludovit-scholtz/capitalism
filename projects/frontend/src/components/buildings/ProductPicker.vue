<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import ProductPickerPanel from '@/components/buildings/ProductPickerPanel.vue'
import type { RankedProductResult } from '@/types'
import { getProductImageUrl, getLocalizedProductName, getLocalizedIndustry } from '@/lib/catalogPresentation'

const { t, locale } = useI18n()

const props = defineProps<{
  /** Currently selected product type ID (or null/undefined for no selection). */
  modelValue: string | null | undefined
  /** Ranked products returned by the rankedProductTypes query. */
  rankedProducts: RankedProductResult[]
  /** Whether the list is loading. */
  loading?: boolean
  /** Whether to show the "none" option. */
  allowNone?: boolean
  /** Label for the none option. */
  noneLabelKey?: string
  /** Optional help text override for context-aware pickers. */
  helpTextKey?: string
  /** Optional empty-state override for context-aware pickers. */
  emptyStateKey?: string
  /**
   * When true, the picker is used in an R&D context (PRODUCT_QUALITY or BRAND_QUALITY unit).
   * This changes the "used by company" section header label and styling to emphasise
   * "Currently Producing" so players can focus research on their active production lines.
   */
  rdContext?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
}>()

type ProductPickerPanelExpose = {
  focusSearchInput: () => void
}

const isOpen = ref(false)
const searchQuery = ref('')
const triggerRef = ref<HTMLElement | null>(null)

/** Position of the dropdown panel calculated from the trigger's bounding rect. */
const panelStyle = ref<{ top: string; left: string; width: string } | null>(null)

/** Layout constants for the dropdown panel. */
const PANEL_MAX_HEIGHT = 340
const MIN_SPACE_BELOW = 200
const VIEWPORT_HEIGHT_FRACTION = 0.5
const PANEL_GAP = 4

function localProductName(r: RankedProductResult): string {
  return getLocalizedProductName(r.productType, locale.value)
}

function localIndustry(r: RankedProductResult): string {
  return getLocalizedIndustry(r.productType.industry, locale.value)
}

/** The currently selected product entry, if any. */
const selectedProduct = computed(() => {
  if (!props.modelValue) return null
  return props.rankedProducts.find((r) => r.productType.id === props.modelValue) ?? null
})

/**
 * True when a saved selection exists but the product is not in the ranked list.
 * This signals a stale/invalid selection that the player should replace.
 */
const hasStaleSelection = computed(() => {
  if (!props.modelValue || props.loading) return false
  return !selectedProduct.value && props.rankedProducts.length > 0
})

const selectedId = computed({
  get: () => props.modelValue ?? null,
  set: (v) => emit('update:modelValue', v),
})

function computePanelPosition() {
  if (!triggerRef.value) return
  const rect = triggerRef.value.getBoundingClientRect()
  const spaceBelow = window.innerHeight - rect.bottom
  panelStyle.value = {
    top: `${rect.bottom + PANEL_GAP}px`,
    left: `${rect.left}px`,
    width: `${rect.width}px`,
  }
  // If not enough space below, position above the trigger
  if (spaceBelow < MIN_SPACE_BELOW) {
    const maxHeight = Math.min(PANEL_MAX_HEIGHT, window.innerHeight * VIEWPORT_HEIGHT_FRACTION)
    panelStyle.value.top = `${rect.top - maxHeight - PANEL_GAP}px`
  }
}

const searchInputRef = ref<ProductPickerPanelExpose | null>(null)

async function open() {
  isOpen.value = true
  searchQuery.value = ''
  await nextTick()
  computePanelPosition()
  // Move focus to search input so keyboard users can type immediately
  searchInputRef.value?.focusSearchInput()
}

function close() {
  isOpen.value = false
}

function toggle() {
  if (isOpen.value) {
    close()
  } else {
    void open()
  }
}

function select(id: string | null) {
  selectedId.value = id
  close()
}

function productImage(r: RankedProductResult): string {
  return getProductImageUrl(r.productType)
}

/** Close panel when clicking outside. */
function onDocumentClick(e: MouseEvent) {
  if (!triggerRef.value) return
  const target = e.target as Node
  if (triggerRef.value.contains(target)) return
  const panel = document.querySelector('.product-picker-panel')
  if (panel && panel.contains(target)) return
  close()
}

onMounted(() => document.addEventListener('mousedown', onDocumentClick))
onUnmounted(() => document.removeEventListener('mousedown', onDocumentClick))

// Reset search when products list changes significantly
watch(
  () => props.rankedProducts.length,
  () => {
    searchQuery.value = ''
  },
)
</script>

<template>
  <div class="product-picker" ref="triggerRef">
    <!-- Stale/invalid selection warning -->
    <div v-if="hasStaleSelection" class="picker-stale-warning" role="alert">
      <span>⚠</span> {{ t('productPicker.invalidSelectionWarning') }}
    </div>

    <!-- Trigger button showing current selection -->
    <button
      type="button"
      class="picker-trigger"
      :class="{ 'picker-trigger-open': isOpen, 'picker-trigger-stale': hasStaleSelection }"
      :aria-expanded="isOpen"
      :aria-haspopup="'listbox'"
      @click="toggle"
    >
      <template v-if="loading">
        <span class="picker-trigger-label picker-trigger-loading">{{ t('productPicker.loading') }}</span>
      </template>
      <template v-else-if="selectedProduct">
        <img
          :src="productImage(selectedProduct)"
          :alt="localProductName(selectedProduct)"
          class="picker-trigger-img"
          aria-hidden="true"
        />
        <span class="picker-trigger-label picker-trigger-selected-name">{{ localProductName(selectedProduct) }}</span>
        <span class="picker-trigger-industry">{{ localIndustry(selectedProduct) }}</span>
      </template>
      <template v-else-if="selectedId === null && allowNone">
        <span class="picker-trigger-label picker-trigger-none">{{ noneLabelKey ? t(noneLabelKey) : t('productPicker.noneLabel') }}</span>
      </template>
      <template v-else>
        <span class="picker-trigger-label picker-trigger-placeholder">{{ t('productPicker.triggerPlaceholder') }}</span>
      </template>
      <span class="picker-trigger-arrow" aria-hidden="true">{{ isOpen ? '▲' : '▼' }}</span>
    </button>

    <!-- Help text -->
    <p class="picker-help-text">{{ t(props.helpTextKey ?? 'productPicker.helpText') }}</p>

    <!-- Dropdown panel teleported to body to escape overflow:hidden containers -->
    <Teleport to="body">
      <ProductPickerPanel
        v-if="isOpen"
        ref="searchInputRef"
        :ranked-products="rankedProducts"
        :loading="loading"
        :allow-none="allowNone"
        :none-label-key="noneLabelKey"
        :empty-state-key="emptyStateKey"
        :rd-context="rdContext"
        :selected-id="selectedId"
        :search-query="searchQuery"
        :panel-style="panelStyle"
        @update:search-query="searchQuery = $event"
        @select="select"
      />
    </Teleport>
  </div>
</template>

<style scoped>
.product-picker {
  position: relative;
}

/* === Trigger button === */
.picker-trigger {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 8px;
  background: var(--color-surface, #fff);
  color: var(--color-text, #111);
  cursor: pointer;
  text-align: left;
  font-size: 0.875rem;
  transition: border-color 0.15s, box-shadow 0.15s;
  min-height: 42px;
}

.picker-trigger:hover {
  border-color: var(--color-primary, #4f46e5);
}

.picker-trigger:focus {
  outline: 2px solid var(--color-primary, #4f46e5);
  outline-offset: 1px;
}

.picker-trigger-open {
  border-color: var(--color-primary, #4f46e5);
  box-shadow: 0 0 0 2px rgba(79, 70, 229, 0.15);
}

.picker-trigger-stale {
  border-color: #f59e0b;
}

.picker-trigger-img {
  width: 28px;
  height: 28px;
  border-radius: 5px;
  object-fit: cover;
  flex-shrink: 0;
  background: var(--color-background, #f3f4f6);
}

.picker-trigger-label {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.picker-trigger-selected-name {
  font-weight: 500;
}

.picker-trigger-placeholder,
.picker-trigger-loading {
  color: var(--color-text-muted, #6b7280);
  font-style: italic;
}

.picker-trigger-none {
  color: var(--color-text-muted, #6b7280);
  font-style: italic;
}

.picker-trigger-industry {
  font-size: 0.72rem;
  color: var(--color-text-muted, #6b7280);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  flex-shrink: 0;
}

.picker-trigger-arrow {
  font-size: 0.7rem;
  color: var(--color-text-muted, #6b7280);
  flex-shrink: 0;
}

/* === Help text === */
.picker-help-text {
  margin: 4px 0 0;
  font-size: 0.75rem;
  color: var(--color-text-muted, #6b7280);
  font-style: italic;
}

/* === Stale warning === */
.picker-stale-warning {
  margin-bottom: 4px;
  padding: 6px 10px;
  background: #fef3c7;
  border: 1px solid #f59e0b;
  border-radius: 6px;
  font-size: 0.8rem;
  color: #92400e;
}

</style>
