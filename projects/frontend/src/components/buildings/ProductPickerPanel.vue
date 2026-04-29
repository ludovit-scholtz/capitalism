<script setup lang="ts">
import { computed, useTemplateRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { RankedProductResult } from '@/types'
import { getProductImageUrl, getLocalizedProductName, getLocalizedIndustry } from '@/lib/catalogPresentation'

const props = defineProps<{
  rankedProducts: RankedProductResult[]
  loading?: boolean
  allowNone?: boolean
  noneLabelKey?: string
  emptyStateKey?: string
  rdContext?: boolean
  selectedId: string | null
  searchQuery: string
  panelStyle: { top: string; left: string; width: string } | null
}>()

const emit = defineEmits<{
  'update:searchQuery': [value: string]
  select: [value: string | null]
}>()

const { t, locale } = useI18n()
const searchInputRef = useTemplateRef<HTMLInputElement>('searchInputRef')

defineExpose({
  focusSearchInput: () => searchInputRef.value?.focus(),
})

function localProductName(r: RankedProductResult): string {
  return getLocalizedProductName(r.productType, locale.value)
}

function localIndustry(r: RankedProductResult): string {
  return getLocalizedIndustry(r.productType.industry, locale.value)
}

function productImage(r: RankedProductResult): string {
  return getProductImageUrl(r.productType)
}

const filteredProducts = computed(() => {
  const q = props.searchQuery.trim().toLowerCase()
  if (!q) return props.rankedProducts
  return props.rankedProducts.filter(
    (r) =>
      localProductName(r).toLowerCase().includes(q) || r.productType.name.toLowerCase().includes(q) || localIndustry(r).toLowerCase().includes(q) || r.productType.industry.toLowerCase().includes(q),
  )
})

const groupedProducts = computed(() => {
  const connected = filteredProducts.value.filter((r) => r.rankingReason === 'connected')
  const manufacturing = filteredProducts.value.filter((r) => r.rankingReason === 'manufacturing')
  const usedByCompany = filteredProducts.value.filter((r) => r.rankingReason === 'used_by_company')
  const catalog = filteredProducts.value.filter((r) => r.rankingReason === 'catalog')
  return { connected, manufacturing, usedByCompany, catalog }
})

function rankingReasonLabel(reason: string): string {
  if (reason === 'connected') return t('productPicker.reasonConnected')
  if (reason === 'manufacturing') return t('productPicker.reasonManufacturing')
  if (reason === 'used_by_company') {
    return props.rdContext ? t('productPicker.reasonActiveProduction') : t('productPicker.reasonUsedByCompany')
  }
  return ''
}

function rankingReasonClass(reason: string): string {
  if (reason === 'connected') return 'badge-connected'
  if (reason === 'manufacturing') return 'badge-active-production'
  if (reason === 'used_by_company') return 'badge-used'
  return ''
}

const availabilityReasonMeta = {
  connected_upstream: {
    labelKey: 'productPicker.reasonConnectedUpstream',
    detailKey: 'productPicker.contextConnectedUpstream',
    className: 'badge-connected',
  },
  current_stock: {
    labelKey: 'productPicker.reasonCurrentStock',
    detailKey: 'productPicker.contextCurrentStock',
    className: 'badge-stock',
  },
  connected_and_stock: {
    labelKey: 'productPicker.reasonConnectedAndStock',
    detailKey: 'productPicker.contextConnectedAndStock',
    className: 'badge-connected-stock',
  },
} as const

function getAvailabilityMeta(entry: RankedProductResult) {
  return entry.availabilityReason ? availabilityReasonMeta[entry.availabilityReason] : null
}

function availabilityReasonLabel(entry: RankedProductResult): string {
  const meta = getAvailabilityMeta(entry)
  if (meta) return t(meta.labelKey)
  return rankingReasonLabel(entry.rankingReason)
}

function availabilityReasonClass(entry: RankedProductResult): string {
  const meta = getAvailabilityMeta(entry)
  if (meta) return meta.className
  return rankingReasonClass(entry.rankingReason)
}

function availabilityReasonDetail(entry: RankedProductResult): string {
  const meta = getAvailabilityMeta(entry)
  if (meta) return t(meta.detailKey)
  if (props.rdContext) {
    if (entry.rankingReason === 'manufacturing') return t('productPicker.contextManufacturing')
    if (entry.rankingReason === 'used_by_company') return t('productPicker.contextInPortfolio')
  }
  return ''
}

function isLocked(entry: RankedProductResult): boolean {
  return entry.productType.isProOnly && !entry.productType.isUnlockedForCurrentPlayer
}

function emitSelectIfAllowed(entry: RankedProductResult) {
  if (isLocked(entry)) return
  emit('select', entry.productType.id)
}
</script>

<template>
  <div
    class="product-picker-panel"
    role="listbox"
    :aria-label="t('productPicker.ariaLabel')"
    :style="panelStyle ? { position: 'fixed', top: panelStyle.top, left: panelStyle.left, width: panelStyle.width } : {}"
  >
    <div class="picker-search">
      <input
        ref="searchInputRef"
        :value="searchQuery"
        type="text"
        class="picker-search-input"
        :placeholder="t('productPicker.searchPlaceholder')"
        :aria-label="t('productPicker.searchPlaceholder')"
        @input="emit('update:searchQuery', ($event.target as HTMLInputElement).value)"
      />
    </div>

    <div class="picker-panel-body">
      <div v-if="loading" class="picker-loading">{{ t('productPicker.loading') }}</div>

      <div v-else-if="filteredProducts.length === 0 && searchQuery" class="picker-empty">
        {{ t('productPicker.noResults') }}
      </div>

      <div v-else-if="rankedProducts.length === 0" class="picker-empty picker-empty-no-connected">
        {{ t(emptyStateKey ?? 'productPicker.noConnectedProducts') }}
      </div>

      <template v-else>
        <div
          v-if="allowNone"
          class="picker-item picker-item-none"
          :class="{ 'picker-item-selected': selectedId === null }"
          role="option"
          :aria-selected="selectedId === null"
          tabindex="0"
          @click="emit('select', null)"
          @keydown.enter.space.prevent="emit('select', null)"
        >
          {{ noneLabelKey ? t(noneLabelKey) : t('productPicker.noneLabel') }}
        </div>

        <template v-if="groupedProducts.connected.length > 0">
          <div class="picker-section-header">{{ t('productPicker.sectionConnected') }}</div>
          <div
            v-for="entry in groupedProducts.connected"
            :key="entry.productType.id"
            class="picker-item"
            :class="{
              'picker-item-selected': selectedId === entry.productType.id,
              'picker-item-locked': isLocked(entry),
            }"
            role="option"
            :aria-selected="selectedId === entry.productType.id"
            :aria-disabled="isLocked(entry)"
            tabindex="0"
            @click="emitSelectIfAllowed(entry)"
            @keydown.enter.space.prevent="emitSelectIfAllowed(entry)"
          >
            <img :src="productImage(entry)" :alt="localProductName(entry)" class="picker-item-img" aria-hidden="true" />
            <div class="picker-item-body">
              <span class="picker-item-name">{{ localProductName(entry) }}</span>
              <span class="picker-item-industry">{{ localIndustry(entry) }}</span>
              <span v-if="availabilityReasonDetail(entry)" class="picker-item-context">{{ availabilityReasonDetail(entry) }}</span>
            </div>
            <span v-if="availabilityReasonLabel(entry)" class="picker-item-badge" :class="availabilityReasonClass(entry)" :title="availabilityReasonLabel(entry)">{{
              availabilityReasonLabel(entry)
            }}</span>
            <span v-if="isLocked(entry)" class="picker-item-badge badge-pro">{{ t('catalog.proBadge') }}</span>
          </div>
        </template>

        <template v-if="groupedProducts.manufacturing.length > 0">
          <div class="picker-section-header picker-section-header--rd">
            <span class="picker-section-icon" aria-hidden="true">­čĆş</span>
            {{ t('productPicker.sectionActiveProductLines') }}
          </div>
          <div
            v-for="entry in groupedProducts.manufacturing"
            :key="entry.productType.id"
            class="picker-item"
            :class="{
              'picker-item-selected': selectedId === entry.productType.id,
              'picker-item-locked': isLocked(entry),
            }"
            role="option"
            :aria-selected="selectedId === entry.productType.id"
            :aria-disabled="isLocked(entry)"
            tabindex="0"
            @click="emitSelectIfAllowed(entry)"
            @keydown.enter.space.prevent="emitSelectIfAllowed(entry)"
          >
            <img :src="productImage(entry)" :alt="localProductName(entry)" class="picker-item-img" aria-hidden="true" />
            <div class="picker-item-body">
              <span class="picker-item-name">{{ localProductName(entry) }}</span>
              <span class="picker-item-industry">{{ localIndustry(entry) }}</span>
              <span v-if="availabilityReasonDetail(entry)" class="picker-item-context">{{ availabilityReasonDetail(entry) }}</span>
            </div>
            <span v-if="availabilityReasonLabel(entry)" class="picker-item-badge" :class="availabilityReasonClass(entry)" :title="availabilityReasonLabel(entry)">{{
              availabilityReasonLabel(entry)
            }}</span>
            <span v-if="isLocked(entry)" class="picker-item-badge badge-pro">{{ t('catalog.proBadge') }}</span>
          </div>
        </template>

        <div v-if="rdContext && groupedProducts.manufacturing.length === 0 && groupedProducts.usedByCompany.length === 0 && !searchQuery" class="picker-rd-hint">
          {{ t('productPicker.rdNoActiveProducts') }}
        </div>

        <template v-if="groupedProducts.usedByCompany.length > 0">
          <div class="picker-section-header" :class="{ 'picker-section-header--rd-secondary': rdContext }">
            {{ rdContext ? t('productPicker.sectionActivePortfolio') : t('productPicker.sectionUsedByCompany') }}
          </div>
          <div
            v-for="entry in groupedProducts.usedByCompany"
            :key="entry.productType.id"
            class="picker-item"
            :class="{
              'picker-item-selected': selectedId === entry.productType.id,
              'picker-item-locked': isLocked(entry),
            }"
            role="option"
            :aria-selected="selectedId === entry.productType.id"
            :aria-disabled="isLocked(entry)"
            tabindex="0"
            @click="emitSelectIfAllowed(entry)"
            @keydown.enter.space.prevent="emitSelectIfAllowed(entry)"
          >
            <img :src="productImage(entry)" :alt="localProductName(entry)" class="picker-item-img" aria-hidden="true" />
            <div class="picker-item-body">
              <span class="picker-item-name">{{ localProductName(entry) }}</span>
              <span class="picker-item-industry">{{ localIndustry(entry) }}</span>
              <span v-if="availabilityReasonDetail(entry)" class="picker-item-context">{{ availabilityReasonDetail(entry) }}</span>
            </div>
            <span v-if="availabilityReasonLabel(entry)" class="picker-item-badge" :class="availabilityReasonClass(entry)" :title="availabilityReasonLabel(entry)">{{
              availabilityReasonLabel(entry)
            }}</span>
            <span v-if="isLocked(entry)" class="picker-item-badge badge-pro">{{ t('catalog.proBadge') }}</span>
          </div>
        </template>

        <template v-if="groupedProducts.catalog.length > 0">
          <div v-if="groupedProducts.connected.length > 0 || groupedProducts.manufacturing.length > 0 || groupedProducts.usedByCompany.length > 0" class="picker-section-header">
            {{ t('productPicker.sectionCatalog') }}
          </div>
          <div
            v-for="entry in groupedProducts.catalog"
            :key="entry.productType.id"
            class="picker-item"
            :class="{
              'picker-item-selected': selectedId === entry.productType.id,
              'picker-item-locked': isLocked(entry),
            }"
            role="option"
            :aria-selected="selectedId === entry.productType.id"
            :aria-disabled="isLocked(entry)"
            tabindex="0"
            @click="emitSelectIfAllowed(entry)"
            @keydown.enter.space.prevent="emitSelectIfAllowed(entry)"
          >
            <img :src="productImage(entry)" :alt="localProductName(entry)" class="picker-item-img" aria-hidden="true" />
            <div class="picker-item-body">
              <span class="picker-item-name">{{ localProductName(entry) }}</span>
              <span class="picker-item-industry">{{ localIndustry(entry) }}</span>
              <span v-if="availabilityReasonDetail(entry)" class="picker-item-context">{{ availabilityReasonDetail(entry) }}</span>
            </div>
            <span v-if="availabilityReasonLabel(entry)" class="picker-item-badge" :class="availabilityReasonClass(entry)" :title="availabilityReasonLabel(entry)">{{
              availabilityReasonLabel(entry)
            }}</span>
            <span v-if="isLocked(entry)" class="picker-item-badge badge-pro">{{ t('catalog.proBadge') }}</span>
          </div>
        </template>
      </template>
    </div>
  </div>
</template>

<style scoped>
.product-picker-panel {
  z-index: 9999;
  max-height: 340px;
  background: var(--color-surface, #fff);
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 8px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.18);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.picker-search {
  padding: 8px;
  border-bottom: 1px solid var(--color-border, #e5e7eb);
  background: var(--color-surface, #fff);
  flex-shrink: 0;
}

.picker-search-input {
  width: 100%;
  padding: 7px 10px;
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 6px;
  font-size: 0.875rem;
  background: var(--color-background, #f9fafb);
  color: var(--color-text, #111);
  box-sizing: border-box;
}

.picker-search-input:focus {
  outline: 2px solid var(--color-primary, #4f46e5);
  outline-offset: -1px;
}

.picker-panel-body {
  overflow-y: auto;
  flex: 1;
  min-height: 0;
}

.picker-loading,
.picker-empty {
  padding: 16px;
  text-align: center;
  font-size: 0.875rem;
  color: var(--color-text-muted, #6b7280);
}

.picker-empty-no-connected {
  padding: 20px 16px;
  line-height: 1.5;
}

.picker-section-header {
  padding: 4px 10px 2px;
  font-size: 0.72rem;
  font-weight: 600;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-text-muted, #6b7280);
  background: var(--color-background, #f3f4f6);
  border-top: 1px solid var(--color-border, #e5e7eb);
  position: sticky;
  top: 0;
  display: flex;
  align-items: center;
  gap: 4px;
}

.picker-section-header--rd {
  color: #065f46;
  background: #ecfdf5;
  border-top-color: #a7f3d0;
}

.picker-section-header--rd-secondary {
  color: #1e40af;
  background: #eff6ff;
  border-top-color: #bfdbfe;
}

.picker-rd-hint {
  padding: 8px 12px;
  font-size: 0.78rem;
  color: var(--color-text-muted, #6b7280);
  font-style: italic;
  border-bottom: 1px solid var(--color-border-light, #f0f0f0);
}

.picker-section-icon {
  font-size: 0.9rem;
  line-height: 1;
}

.picker-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  cursor: pointer;
  transition: background 0.12s;
  border-bottom: 1px solid var(--color-border-light, #f0f0f0);
  min-height: 44px;
}

.picker-item:last-child {
  border-bottom: none;
}

.picker-item:hover:not(.picker-item-locked) {
  background: var(--color-hover, #f0f9ff);
}

.picker-item:focus {
  outline: 2px solid var(--color-primary, #4f46e5);
  outline-offset: -2px;
}

.picker-item-selected {
  background: var(--color-primary-light, #ede9fe) !important;
}

.picker-item-locked {
  opacity: 0.55;
  cursor: not-allowed;
}

.picker-item-none {
  font-style: italic;
  color: var(--color-text-muted, #6b7280);
  font-size: 0.875rem;
}

.picker-item-img {
  width: 36px;
  height: 36px;
  border-radius: 6px;
  object-fit: cover;
  flex-shrink: 0;
  background: var(--color-background, #f3f4f6);
}

.picker-item-body {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.picker-item-name {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text, #111);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.picker-item-industry {
  font-size: 0.72rem;
  color: var(--color-text-muted, #6b7280);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.picker-item-context {
  font-size: 0.72rem;
  color: var(--color-text-muted, #6b7280);
  line-height: 1.35;
}

.picker-item-badge {
  font-size: 0.7rem;
  font-weight: 600;
  padding: 2px 6px;
  border-radius: 10px;
  flex-shrink: 0;
  white-space: nowrap;
}

.badge-connected {
  background: #d1fae5;
  color: #065f46;
}

.badge-used {
  background: #dbeafe;
  color: #1e40af;
}

.badge-active-production {
  background: #d1fae5;
  color: #065f46;
}

.badge-stock {
  background: #fef3c7;
  color: #92400e;
}

.badge-connected-stock {
  background: #ede9fe;
  color: #5b21b6;
}

.badge-pro {
  background: #fef3c7;
  color: #92400e;
}
</style>
