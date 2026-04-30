<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import UiStateLoading from '@/components/ui/UiStateLoading.vue'
import UiStateError from '@/components/ui/UiStateError.vue'
import UiStateEmpty from '@/components/ui/UiStateEmpty.vue'
import type { GlobalExchangeOffer } from '@/types'

interface City {
  id: string
  name: string
}

interface ExchangeRow {
  resourceId: string
  resourceName: string
  resourceSlug: string
  unitSymbol: string
  category: string
  offers: GlobalExchangeOffer[]
  bestCityId: string
}

defineProps<{
  cities: City[]
  selectedCityId: string | null
  search: string
  selectedCategory: string
  categories: string[]
  loading: boolean
  error: string | null
  exchangeRows: ExchangeRow[]
  formatPrice: (value: number) => string
  formatPercent: (value: number) => string
  localizedCategory: (value: string) => string
}>()

const emit = defineEmits<{
  'update:search': [value: string]
  'update:selectedCategory': [value: string]
  'switch-city': [cityId: string]
  retry: []
}>()

const { t } = useI18n()
</script>

<template>
  <div>
    <div v-if="cities.length > 0" class="exchange-city-tabs city-tabs mb-6 flex flex-wrap gap-2" role="tablist" :aria-label="t('common.city')">
      <button
        v-for="city in cities"
        :key="city.id"
        role="tab"
        :aria-selected="selectedCityId === city.id"
        :class="[
          'exchange-city-tab city-tab rounded-md border px-3 py-1.5 text-sm font-medium transition-colors',
          selectedCityId === city.id ? 'active border-brand bg-brand/10 text-brand' : 'border-divider bg-card text-muted hover:border-brand hover:text-body',
        ]"
        @click="emit('switch-city', city.id)"
      >
        {{ city.name }}
      </button>
    </div>

    <div class="exchange-filters mb-6 flex flex-wrap items-end gap-4">
      <div class="search-wrapper min-w-[180px] flex-1">
        <input
          :value="search"
          type="search"
          class="search-input w-full rounded-md border border-divider bg-card px-3 py-2 text-sm text-body"
          :placeholder="t('globalExchange.searchPlaceholder')"
          :aria-label="t('globalExchange.searchPlaceholder')"
          @input="emit('update:search', ($event.target as HTMLInputElement).value)"
        />
      </div>
      <div class="category-filter flex flex-col gap-1">
        <label for="category-select" class="filter-label text-xs font-semibold uppercase tracking-[0.04em] text-muted">{{ t('globalExchange.filterCategory') }}</label>
        <select
          id="category-select"
          :value="selectedCategory"
          class="filter-select min-w-[140px] rounded-md border border-divider bg-card px-3 py-2 text-sm text-body"
          @change="emit('update:selectedCategory', ($event.target as HTMLSelectElement).value)"
        >
          <option v-for="cat in categories" :key="cat" :value="cat">
            {{ cat === 'ALL' ? t('globalExchange.allCategories') : localizedCategory(cat) }}
          </option>
        </select>
      </div>
    </div>

    <UiStateLoading v-if="loading" class="exchange-loading" :label="t('common.loading')" />
    <UiStateError v-else-if="error" class="exchange-error" :message="error" :retry-label="t('common.retry')" @retry="emit('retry')" />
    <UiStateEmpty v-else-if="exchangeRows.length === 0" class="exchange-empty">
      {{ t('globalExchange.noResults') }}
    </UiStateEmpty>

    <template v-else>
      <div v-for="row in exchangeRows" :key="row.resourceId" class="resource-row mb-8 overflow-hidden rounded-xl border border-divider bg-card" :data-slug="row.resourceSlug">
        <div class="resource-row-header flex items-center gap-3 border-b border-divider bg-brand/5 px-4 py-3">
          <span class="resource-name text-base font-bold text-body">{{ row.resourceName }}</span>
          <span class="resource-category-badge rounded-full bg-brand/15 px-2 py-0.5 text-[0.7rem] font-semibold uppercase tracking-[0.05em] text-brand">
            {{ localizedCategory(row.category) }}
          </span>
          <RouterLink
            :to="`/encyclopedia/resources/${row.resourceSlug}`"
            class="production-chain-link ml-auto whitespace-nowrap rounded border border-brand/40 px-2 py-0.5 text-xs font-semibold text-brand"
            :aria-label="`${t('globalExchange.viewProductionChain')}: ${row.resourceName}`"
          >
            {{ t('globalExchange.viewProductionChain') }}
          </RouterLink>
        </div>

        <div class="city-offers-grid grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-4 p-4">
          <div
            v-for="offer in row.offers"
            :key="offer.cityId"
            :class="['city-offer-card rounded-md border border-divider bg-page p-3.5', offer.cityId === row.bestCityId ? 'best-offer border-brand bg-brand/5' : '']"
          >
            <div class="offer-card-header mb-2.5 flex items-center justify-between gap-2">
              <strong class="offer-city-name text-sm font-bold text-body">{{ offer.cityName }}</strong>
              <span v-if="offer.cityId === row.bestCityId" class="best-badge whitespace-nowrap rounded-full bg-brand px-1.5 py-0.5 text-[0.65rem] font-bold uppercase tracking-[0.05em] text-white">
                {{ t('globalExchange.bestDelivered') }}
              </span>
            </div>

            <div class="offer-metrics flex flex-col gap-1.5">
              <div class="offer-metric flex items-baseline justify-between gap-2 text-[0.78rem]">
                <span class="metric-label whitespace-nowrap text-muted">{{ t('globalExchange.exchangePrice') }}</span>
                <span class="metric-value exchange-price text-right font-medium text-body"> {{ formatPrice(offer.exchangePricePerUnit) }}/{{ offer.unitSymbol }} </span>
              </div>
              <div class="offer-metric flex items-baseline justify-between gap-2 text-[0.78rem]">
                <span class="metric-label whitespace-nowrap text-muted">{{ t('globalExchange.transitCost') }}</span>
                <span class="metric-value transit-cost flex flex-wrap items-center justify-end gap-1.5 text-right font-medium text-body">
                  +{{ formatPrice(offer.transitCostPerUnit) }} · {{ offer.distanceKm }} km
                  <span
                    v-if="offer.fuelPriceIndex && offer.fuelPriceIndex !== 1"
                    class="fuel-badge whitespace-nowrap rounded px-1 py-0.5 text-[0.7rem] font-semibold"
                    :class="offer.fuelPriceIndex > 1 ? 'fuel-high bg-amber-500/20 text-amber-500' : 'fuel-low bg-emerald-500/20 text-emerald-500'"
                    :title="t('globalExchange.fuelPriceHint')"
                    >⛽ ×{{ offer.fuelPriceIndex.toFixed(2) }}</span
                  >
                </span>
              </div>
              <div class="offer-metric delivered-metric mt-1 border-t border-divider pt-1.5">
                <span class="metric-label whitespace-nowrap text-muted">{{ t('globalExchange.deliveredPrice') }}</span>
                <span class="metric-value delivered-price text-right text-sm font-bold text-brand"> {{ formatPrice(offer.deliveredPricePerUnit) }}/{{ offer.unitSymbol }} </span>
              </div>
              <div class="offer-metric flex items-baseline justify-between gap-2 text-[0.78rem]">
                <span class="metric-label whitespace-nowrap text-muted">{{ t('globalExchange.quality') }}</span>
                <span class="metric-value quality-value flex flex-col items-end gap-1 text-right font-medium text-body">
                  <span class="quality-range whitespace-nowrap text-[0.78rem] font-semibold"> {{ formatPercent(offer.qualityMin) }}&nbsp;–&nbsp;{{ formatPercent(offer.qualityMax) }} </span>
                  <span class="quality-band-bar" :title="t('globalExchange.qualityBandHint')">
                    <span
                      class="quality-band-fill"
                      :style="{
                        left: `${offer.qualityMin * 100}%`,
                        width: `${(offer.qualityMax - offer.qualityMin) * 100}%`,
                      }"
                    ></span>
                    <span class="quality-band-center" :style="{ left: `${offer.estimatedQuality * 100}%` }"></span>
                  </span>
                </span>
              </div>
              <div class="offer-metric flex items-baseline justify-between gap-2 text-[0.78rem]">
                <span class="metric-label whitespace-nowrap text-muted">{{ t('globalExchange.abundance') }}</span>
                <span class="metric-value text-right font-medium text-body">{{ formatPercent(offer.localAbundance) }}</span>
              </div>
            </div>
          </div>

          <p v-if="row.offers.length === 0" class="no-offers-hint py-2 text-sm text-muted">
            {{ t('globalExchange.noOffersForResource') }}
          </p>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.quality-band-bar {
  position: relative;
  width: 80px;
  height: 5px;
  border-radius: 999px;
  overflow: visible;
  background: color-mix(in srgb, var(--color-border) 60%, transparent);
}

.quality-band-fill {
  position: absolute;
  top: 0;
  height: 100%;
  min-width: 3px;
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-primary) 70%, transparent);
}

.quality-band-center {
  position: absolute;
  top: -2px;
  transform: translateX(-50%);
  width: 3px;
  height: 9px;
  border-radius: 1px;
  background: var(--color-primary);
}
</style>
