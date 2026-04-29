<script setup lang="ts">
/* oxlint-disable no-unused-vars */
 
/* eslint-disable @typescript-eslint/no-unused-vars */
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
</script>`r`n`r`n<template src="./ProductPickerPanel.template.html"></template>`r`n`r`n<style scoped src="./ProductPickerPanel.styles.css"></style>`r`n

<template src="./ProductPickerPanel.template.html"></template>

<style scoped src="./ProductPickerPanel.styles.css"></style>