<script setup lang="ts">
import { computed, inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import AdvancedItemSelector from '@/components/buildings/AdvancedItemSelector.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  building,
  showPurchaseSelector,
  selectedDraftPurchaseUnit,
  purchaseSelectorItems,
  selectedPurchaseSelection,
  purchaseVendorOptions,
  sourcingCandidates,
  sourcingCandidatesLoading,
  closePurchaseSelector,
  applyPurchaseSelection,
  selectPurchaseVendor,
  lockSourcingCandidate,
  getPurchaseVendorTransitLabel,
  formatCurrency,
} = bd

const sourcingComparisonRows = computed(() =>
  sourcingCandidates.value.filter((candidate) => candidate.deliveredPricePerUnit != null),
)

type TransitEfficiency = 'good' | 'warning' | 'bad' | 'neutral'

const bestEligibleDeliveredPrice = computed(() => {
  const eligible = sourcingComparisonRows.value.filter((candidate) => candidate.isEligible)
  if (eligible.length === 0) return null
  return Math.min(...eligible.map((candidate) => candidate.deliveredPricePerUnit ?? Number.POSITIVE_INFINITY))
})

function getTransitEfficiency(pricePerUnit: number | null | undefined, transitCostPerUnit: number | null | undefined): TransitEfficiency {
  if (pricePerUnit == null || pricePerUnit <= 0 || transitCostPerUnit == null) {
    return 'neutral'
  }

  const ratio = transitCostPerUnit / pricePerUnit
  if (ratio < 0.05) return 'good'
  if (ratio <= 0.2) return 'warning'
  return 'bad'
}

function getTransitEfficiencyLabel(pricePerUnit: number | null | undefined, transitCostPerUnit: number | null | undefined): string {
  const efficiency = getTransitEfficiency(pricePerUnit, transitCostPerUnit)
  if (efficiency === 'good') return t('buildingDetail.purchaseSelector.transitEfficiencyGood')
  if (efficiency === 'warning') return t('buildingDetail.purchaseSelector.transitEfficiencyWarning')
  if (efficiency === 'bad') return t('buildingDetail.purchaseSelector.transitEfficiencyBad')
  return t('buildingDetail.purchaseSelector.transitEfficiencyUnknown')
}
</script>

<template>
  <div
    v-if="showPurchaseSelector"
    class="purchase-selector-page fixed inset-0 z-50 overflow-y-auto bg-black/45 p-4 backdrop-blur-sm sm:p-6"
    role="dialog"
    :aria-label="t('buildingDetail.purchaseSelector.title')"
  >
    <div class="purchase-selector-shell mx-auto mt-2 w-full max-w-6xl rounded-2xl border border-divider bg-card p-4 shadow-2xl sm:mt-6 sm:p-6">
      <div class="purchase-selector-header mb-4 flex flex-wrap items-start justify-between gap-3 border-b border-divider pb-4">
        <div>
          <p class="purchase-selector-eyebrow text-xs font-semibold uppercase tracking-[0.08em] text-muted">{{ t('buildingDetail.purchaseSelector.eyebrow') }}</p>
          <h2 class="mt-1 text-xl font-semibold text-foreground">{{ t('buildingDetail.purchaseSelector.title') }}</h2>
        </div>
        <button class="btn btn-ghost" @click="closePurchaseSelector">{{ t('common.close') }}</button>
      </div>

      <div class="purchase-selector-grid grid grid-cols-1 gap-4 lg:grid-cols-2 lg:gap-5">
        <section class="purchase-selector-card rounded-xl border border-divider bg-surface p-4">
          <AdvancedItemSelector
            :model-value="selectedPurchaseSelection"
            :items="purchaseSelectorItems"
            :label="t('buildingDetail.config.inputItem')"
            :placeholder="t('buildingDetail.selector.searchPlaceholder')"
            :empty-text="t('buildingDetail.selector.noItems')"
            @update:model-value="applyPurchaseSelection"
          />
        </section>

        <section class="purchase-selector-card rounded-xl border border-divider bg-surface p-4">
          <h3 class="text-lg font-semibold text-foreground">{{ t('buildingDetail.purchaseSelector.vendorTitle') }}</h3>
          <p class="config-help mt-1 text-sm text-muted">{{ t('buildingDetail.purchaseSelector.vendorHelp') }}</p>

          <button
            type="button"
            class="purchase-vendor-card mt-3 grid w-full gap-0.5 rounded-xl border border-divider bg-card p-3 text-left transition-colors hover:border-primary/40 hover:bg-primary/5"
            :class="{ selected: selectedDraftPurchaseUnit?.vendorLockCompanyId == null, 'border-primary bg-primary/10': selectedDraftPurchaseUnit?.vendorLockCompanyId == null }"
            @click="selectPurchaseVendor(null)"
          >
            <strong>{{ t('buildingDetail.purchaseSelector.vendorAutoTitle') }}</strong>
            <span class="text-sm text-muted">{{ t('buildingDetail.purchaseSelector.vendorAuto') }}</span>
          </button>

          <button
            type="button"
            class="purchase-vendor-card mt-2 grid w-full gap-0.5 rounded-xl border border-divider bg-card p-3 text-left transition-colors hover:border-primary/40 hover:bg-primary/5"
            :class="{
              selected: selectedDraftPurchaseUnit?.vendorLockCompanyId === building?.companyId,
              'border-primary bg-primary/10': selectedDraftPurchaseUnit?.vendorLockCompanyId === building?.companyId,
            }"
            @click="selectPurchaseVendor(building!.companyId)"
          >
            <strong>{{ t('buildingDetail.purchaseSelector.vendorOwnCompany') }}</strong>
            <span class="text-sm text-muted">{{ t('buildingDetail.purchaseSelector.vendorOwnCompanyHelp') }}</span>
          </button>

          <div v-if="purchaseVendorOptions.length > 0" class="purchase-vendor-list mt-3 grid gap-2">
            <button
              v-for="option in purchaseVendorOptions"
              :key="`${option.companyId}-${option.buildingId}`"
              type="button"
              class="purchase-vendor-card grid w-full gap-0.5 rounded-xl border border-divider bg-card p-3 text-left transition-colors hover:border-primary/40 hover:bg-primary/5"
              :class="{
                selected: selectedDraftPurchaseUnit?.vendorLockCompanyId === option.companyId,
                'border-primary bg-primary/10': selectedDraftPurchaseUnit?.vendorLockCompanyId === option.companyId,
              }"
              @click="selectPurchaseVendor(option.companyId)"
            >
              <strong>{{ option.companyName }}</strong>
              <span class="text-sm text-muted">{{ option.buildingName }}</span>
              <span v-if="option.pricePerUnit != null" class="purchase-vendor-pricing text-xs font-medium text-muted">
                {{ t('buildingDetail.purchaseSelector.vendorPrice', { price: formatCurrency(option.pricePerUnit) }) }}
              </span>
              <span class="purchase-vendor-pricing purchase-vendor-transit-line text-xs font-medium text-muted">
                <span aria-hidden="true">🚚</span>
                {{ getPurchaseVendorTransitLabel(option.transitCostPerUnit) }}
                <span
                  class="purchase-vendor-transit-badge"
                  :class="`is-${getTransitEfficiency(option.pricePerUnit, option.transitCostPerUnit)}`"
                >
                  {{ getTransitEfficiencyLabel(option.pricePerUnit, option.transitCostPerUnit) }}
                </span>
              </span>
            </button>
          </div>
          <p v-else class="config-help mt-3 rounded-lg border border-dashed border-divider bg-surface-muted px-3 py-2 text-sm text-muted">{{ t('buildingDetail.purchaseSelector.vendorEmpty') }}</p>

          <div class="mt-4">
            <h4 class="text-sm font-semibold text-foreground">{{ t('buildingDetail.purchaseSelector.comparisonTitle') }}</h4>
            <p class="config-help mt-1 text-sm text-muted">{{ t('buildingDetail.purchaseSelector.comparisonHelp') }}</p>
            <p v-if="sourcingCandidatesLoading" class="config-help mt-2 text-sm text-muted">{{ t('common.loading') }}</p>
            <div v-else-if="sourcingComparisonRows.length > 0" class="mt-2 overflow-x-auto">
              <table class="w-full min-w-[540px] text-sm">
                <thead>
                  <tr class="text-left text-xs uppercase tracking-wide text-muted">
                    <th class="pb-2 pr-2">{{ t('buildingDetail.purchaseSelector.comparisonSource') }}</th>
                    <th class="pb-2 pr-2">{{ t('buildingDetail.purchaseSelector.comparisonLocalPrice') }}</th>
                    <th class="pb-2 pr-2">{{ t('buildingDetail.purchaseSelector.comparisonLogistics') }}</th>
                    <th class="pb-2 pr-2">{{ t('buildingDetail.purchaseSelector.comparisonLanded') }}</th>
                    <th class="pb-2"></th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="candidate in sourcingComparisonRows"
                    :key="`${candidate.sourceType}-${candidate.rank}-${candidate.sourceCityId ?? 'none'}`"
                    class="border-t border-divider/70"
                  >
                    <td class="py-2 pr-2">
                      <div class="flex flex-col gap-0.5">
                        <span>{{ candidate.sourceCityName ?? t('common.notAvailable') }}</span>
                        <span class="text-xs text-muted">
                          {{
                            candidate.sourceCityId && candidate.sourceCityId === building?.cityId
                              ? t('buildingDetail.purchaseSelector.sourceLocal')
                              : t('buildingDetail.purchaseSelector.sourceCrossCity')
                          }}
                        </span>
                        <span v-if="candidate.isRecommended || ((candidate.deliveredPricePerUnit ?? 0) === bestEligibleDeliveredPrice && candidate.isEligible)" class="text-xs font-semibold text-success">
                          {{ t('buildingDetail.purchaseSelector.bestValue') }}
                        </span>
                      </div>
                    </td>
                    <td class="py-2 pr-2">{{ candidate.exchangePricePerUnit != null ? formatCurrency(candidate.exchangePricePerUnit) : '—' }}</td>
                    <td class="py-2 pr-2">{{ candidate.transitCostPerUnit != null ? formatCurrency(candidate.transitCostPerUnit) : '—' }}</td>
                    <td class="py-2 pr-2 font-semibold">{{ candidate.deliveredPricePerUnit != null ? formatCurrency(candidate.deliveredPricePerUnit) : '—' }}</td>
                    <td class="py-2 text-right">
                      <button
                        type="button"
                        class="btn btn-ghost btn-sm"
                        :disabled="!candidate.isEligible"
                        @click="lockSourcingCandidate(candidate)"
                      >
                        {{ t('buildingDetail.purchaseSelector.useSource') }}
                      </button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p v-else class="config-help mt-2 text-sm text-muted">{{ t('buildingDetail.purchaseSelector.comparisonEmpty') }}</p>
          </div>
        </section>
      </div>

      <div class="purchase-selector-actions mt-5 flex justify-end border-t border-divider pt-4">
        <button class="btn btn-primary" @click="closePurchaseSelector">{{ t('buildingDetail.purchaseSelector.done') }}</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.purchase-vendor-transit-line {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}

.purchase-vendor-transit-badge {
  border-radius: 9999px;
  border: 1px solid var(--color-border);
  padding: 0.05rem 0.45rem;
  font-size: 0.65rem;
  font-weight: 700;
  letter-spacing: 0.01em;
  text-transform: uppercase;
}

.purchase-vendor-transit-badge.is-good {
  border-color: color-mix(in srgb, var(--color-success) 50%, var(--color-border));
  background: color-mix(in srgb, var(--color-success) 18%, transparent);
  color: var(--color-success);
}

.purchase-vendor-transit-badge.is-warning {
  border-color: color-mix(in srgb, var(--color-warning) 50%, var(--color-border));
  background: color-mix(in srgb, var(--color-warning) 18%, transparent);
  color: var(--color-warning);
}

.purchase-vendor-transit-badge.is-bad {
  border-color: color-mix(in srgb, var(--color-danger) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-danger) 18%, transparent);
  color: var(--color-danger);
}

.purchase-vendor-transit-badge.is-neutral {
  color: var(--color-text-secondary);
}
</style>
