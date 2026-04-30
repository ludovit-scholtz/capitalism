<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import UiStateLoading from '@/components/ui/UiStateLoading.vue'
import UiStateError from '@/components/ui/UiStateError.vue'
import UiStateEmpty from '@/components/ui/UiStateEmpty.vue'
import type { GlobalExchangeProductListing, GlobalExchangeProductQuote } from '@/types'

interface ProductRow {
  productId: string
  productName: string
  productSlug: string
  productIndustry: string
  unitSymbol: string
  basePrice: number
  marketQuote: GlobalExchangeProductQuote
  listings: GlobalExchangeProductListing[]
}

defineProps<{
  search: string
  selectedIndustry: string
  industries: string[]
  loading: boolean
  error: string | null
  productRows: ProductRow[]
  formatPrice: (value: number) => string
  localizedIndustry: (value: string) => string
  priceVsBase: (pricePerUnit: number, basePrice: number) => string
  priceVsBaseClass: (pricePerUnit: number, basePrice: number) => string
}>()

const emit = defineEmits<{
  'update:search': [value: string]
  'update:selectedIndustry': [value: string]
  retry: []
}>()

const { t } = useI18n()
</script>

<template>
  <div>
    <div class="exchange-filters mb-4 flex flex-wrap items-end gap-4">
      <div class="search-wrapper min-w-[180px] flex-1">
        <input
          :value="search"
          type="search"
          class="search-input w-full rounded-md border border-divider bg-card px-3 py-2 text-sm text-body"
          :placeholder="t('globalExchange.productSearchPlaceholder')"
          :aria-label="t('globalExchange.productSearchPlaceholder')"
          @input="emit('update:search', ($event.target as HTMLInputElement).value)"
        />
      </div>
      <div class="category-filter flex flex-col gap-1">
        <label for="industry-select" class="filter-label text-xs font-semibold uppercase tracking-[0.04em] text-muted">{{ t('globalExchange.filterIndustry') }}</label>
        <select
          id="industry-select"
          :value="selectedIndustry"
          class="filter-select min-w-[140px] rounded-md border border-divider bg-card px-3 py-2 text-sm text-body"
          @change="emit('update:selectedIndustry', ($event.target as HTMLSelectElement).value)"
        >
          <option v-for="ind in industries" :key="ind" :value="ind">
            {{ ind === 'ALL' ? t('globalExchange.allIndustries') : localizedIndustry(ind) }}
          </option>
        </select>
      </div>
    </div>

    <p class="products-mode-hint mb-4 rounded-r-md border-l-4 border-[var(--color-accent,#a855f7)] bg-[color-mix(in_srgb,var(--color-accent,#a855f7)_6%,transparent)] px-3 py-2 text-sm text-muted">
      {{ t('globalExchange.modeProductsHint') }}
    </p>

    <UiStateLoading v-if="loading" class="exchange-loading" :label="t('common.loading')" />
    <UiStateError v-else-if="error" class="exchange-error" :message="error" :retry-label="t('common.retry')" @retry="emit('retry')" />
    <UiStateEmpty v-else-if="productRows.length === 0" class="exchange-empty">
      {{ t('globalExchange.noProductResults') }}
    </UiStateEmpty>

    <template v-else>
      <div v-for="row in productRows" :key="row.productId" class="product-row mb-8 overflow-hidden rounded-xl border border-divider bg-card" :data-slug="row.productSlug">
        <div class="product-row-header flex items-center gap-3 border-b border-divider bg-[color-mix(in_srgb,var(--color-surface)_92%,var(--color-accent,#a855f7)_8%)] px-4 py-3">
          <span class="product-name text-base font-bold text-body">{{ row.productName }}</span>
          <span
            class="product-industry-badge rounded-full bg-[color-mix(in_srgb,var(--color-accent,#a855f7)_15%,transparent)] px-2 py-0.5 text-[0.7rem] font-semibold uppercase tracking-[0.05em] text-[var(--color-accent,#a855f7)]"
          >
            {{ localizedIndustry(row.productIndustry) }}
          </span>
          <span class="product-listing-count text-xs font-medium text-muted">{{ t('globalExchange.productListingsCount', { count: row.listings.length }) }}</span>
          <RouterLink
            :to="`/encyclopedia/products/${row.productSlug}`"
            class="production-chain-link ml-auto whitespace-nowrap rounded border border-brand/40 px-2 py-0.5 text-xs font-semibold text-brand"
            :aria-label="`${t('globalExchange.viewProductDetail')}: ${row.productName}`"
          >
            {{ t('globalExchange.viewProductDetail') }}
          </RouterLink>
        </div>

        <div class="product-market-quote-grid grid grid-cols-[repeat(auto-fit,minmax(160px,1fr))] gap-3 px-4 pb-0 pt-3">
          <div class="product-market-quote flex flex-col gap-1 rounded-md border border-divider bg-page p-3">
            <span class="metric-label text-xs text-muted">{{ t('globalExchange.productBasePrice') }}</span>
            <strong class="text-sm text-body">{{ formatPrice(row.marketQuote.basePrice) }}/{{ row.unitSymbol }}</strong>
          </div>
          <div class="product-market-quote flex flex-col gap-1 rounded-md border border-divider bg-page p-3">
            <span class="metric-label text-xs text-muted">{{ t('globalExchange.productBidPrice') }}</span>
            <strong class="listing-price text-sm text-body">{{ formatPrice(row.marketQuote.bidPricePerUnit) }}/{{ row.unitSymbol }}</strong>
          </div>
          <div class="product-market-quote flex flex-col gap-1 rounded-md border border-divider bg-page p-3">
            <span class="metric-label text-xs text-muted">{{ t('globalExchange.productAskPrice') }}</span>
            <strong class="listing-price text-sm text-body">{{ formatPrice(row.marketQuote.offerPricePerUnit) }}/{{ row.unitSymbol }}</strong>
          </div>
          <div class="product-market-quote flex flex-col gap-1 rounded-md border border-divider bg-page p-3">
            <span class="metric-label text-xs text-muted">{{ t('globalExchange.quality') }}</span>
            <strong class="text-sm text-body">{{ Math.round(row.marketQuote.estimatedQuality * 100) }}%</strong>
          </div>
        </div>

        <div v-if="row.listings.length > 0" class="product-listings-table px-4 py-3">
          <div class="listings-header mb-1 grid grid-cols-[1.5fr_0.75fr_1fr_1.5fr_1fr] gap-2 border-b border-divider px-2 py-1.5 text-[0.7rem] font-bold uppercase tracking-[0.05em] text-muted">
            <span>{{ t('globalExchange.productPlayerAskPrice') }}</span>
            <span>{{ t('globalExchange.productPriceVsBase') }}</span>
            <span>{{ t('globalExchange.productAvailable') }}</span>
            <span>{{ t('globalExchange.productSeller') }}</span>
            <span>{{ t('globalExchange.productCity') }}</span>
          </div>
          <div
            v-for="listing in row.listings"
            :key="listing.orderId"
            class="listing-row grid grid-cols-[1.5fr_0.75fr_1fr_1.5fr_1fr] items-center gap-2 rounded px-2 py-2 text-[0.8125rem] hover:bg-[color-mix(in_srgb,var(--color-surface)_80%,var(--color-accent,#a855f7)_20%)]"
          >
            <span class="listing-price font-bold text-body"> {{ formatPrice(listing.pricePerUnit) }}/{{ listing.unitSymbol }} </span>
            <span :class="['listing-vs-base rounded px-1 py-0.5 text-[0.7rem] font-bold', priceVsBaseClass(listing.pricePerUnit, row.basePrice)]">
              {{ priceVsBase(listing.pricePerUnit, row.basePrice) }}
            </span>
            <span class="listing-quantity text-body"> {{ listing.remainingQuantity.toFixed(0) }} {{ listing.unitSymbol }} </span>
            <span class="listing-seller overflow-hidden text-ellipsis whitespace-nowrap font-medium text-body">{{ listing.sellerCompanyName }}</span>
            <span class="listing-city text-muted">{{ listing.sellerCityName }}</span>
          </div>
        </div>
        <p v-else class="no-product-listings-hint px-4 pb-4 text-sm text-muted">{{ t('globalExchange.noProductListingsHint') }}</p>
      </div>
    </template>
  </div>
</template>

<style scoped>
.price-above-base {
  color: var(--color-error, #ef4444);
  background: color-mix(in srgb, var(--color-error, #ef4444) 10%, transparent);
}

.price-below-base {
  color: var(--color-success, #22c55e);
  background: color-mix(in srgb, var(--color-success, #22c55e) 10%, transparent);
}

.price-at-base {
  color: var(--color-text-secondary);
}

@media (max-width: 480px) {
  .listings-header,
  .listing-row {
    grid-template-columns: 1.2fr 0.6fr 0.8fr 1fr 0.8fr;
    font-size: 0.7rem;
  }
}
</style>
