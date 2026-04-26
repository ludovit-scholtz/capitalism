<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { shopChainDisplayUnits, shopChainStatus, showSalesChainPanel, getResourceName, getProductName, formatCurrency, dismissSalesChainPanel } = bd
</script>

<template>
  <!-- Production chain status panel: shown for factories with the starter layout saved -->

  <!-- Sales chain status panel: shown for sales shops with units saved -->
  <div v-if="showSalesChainPanel" class="production-chain-panel mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5" role="region" aria-label="sales chain status">
    <div class="chain-panel-header mb-4 flex flex-wrap items-center gap-2">
      <h3 class="chain-panel-title text-lg font-semibold text-foreground">🏪 {{ t('buildingDetail.salesChain.title') }}</h3>
      <span v-if="shopChainStatus.isChainComplete" class="chain-status-badge chain-status-badge--complete inline-flex items-center rounded-full border border-emerald-300/60 bg-emerald-500/10 px-2.5 py-1 text-xs font-semibold text-emerald-700 dark:text-emerald-300">✅ {{ t('buildingDetail.salesChain.chainComplete') }}</span>
      <span v-else class="chain-status-badge chain-status-badge--incomplete inline-flex items-center rounded-full border border-amber-300/60 bg-amber-500/10 px-2.5 py-1 text-xs font-semibold text-amber-700 dark:text-amber-300">⚠️ {{ t('buildingDetail.salesChain.chainIncomplete') }}</span>
      <button class="chain-panel-dismiss ml-auto rounded-md px-2 py-1 text-xs font-medium text-muted hover:bg-surface hover:text-foreground" :aria-label="t('buildingDetail.salesChain.dismissAriaLabel')" @click="dismissSalesChainPanel">{{ t('buildingDetail.salesChain.dismiss') }}</button>
    </div>

    <div class="chain-flow flex flex-wrap items-center gap-2 sm:gap-3" role="list" aria-label="sales chain steps">
      <!-- PURCHASE step -->
      <div class="chain-step min-w-[180px] flex-1 rounded-lg border p-3" :class="shopChainStatus.isPurchaseConfigured ? 'chain-step--configured border-emerald-300/60 bg-emerald-500/10' : 'chain-step--missing border-amber-300/60 bg-amber-500/10'" role="listitem">
        <div class="chain-step-icon text-lg">🛒</div>
        <div class="chain-step-type mt-1 text-xs font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.unitTypes.PURCHASE') }}</div>
        <div v-if="shopChainStatus.isPurchaseConfigured" class="chain-step-value mt-1 text-sm font-semibold text-foreground">
          {{ shopChainDisplayUnits.purchase?.resourceTypeId ? getResourceName(shopChainDisplayUnits.purchase.resourceTypeId) : getProductName(shopChainDisplayUnits.purchase?.productTypeId ?? null) }}
        </div>
        <div v-else class="chain-step-missing-label mt-1 text-sm font-medium text-warning">
          {{ t('buildingDetail.salesChain.notConfigured') }}
        </div>
      </div>

      <div class="chain-arrow px-1 text-xl text-muted" aria-hidden="true">→</div>

      <!-- PUBLIC_SALES step -->
      <div class="chain-step min-w-[180px] flex-1 rounded-lg border p-3" :class="shopChainStatus.isPublicSalesConfigured ? 'chain-step--configured border-emerald-300/60 bg-emerald-500/10' : 'chain-step--missing border-amber-300/60 bg-amber-500/10'" role="listitem">
        <div class="chain-step-icon text-lg">💲</div>
        <div class="chain-step-type mt-1 text-xs font-semibold uppercase tracking-wide text-muted">{{ t('buildingDetail.unitTypes.PUBLIC_SALES') }}</div>
        <div v-if="shopChainStatus.isPublicSalesConfigured" class="chain-step-value mt-1 text-sm font-semibold text-foreground">
          {{ getProductName(shopChainDisplayUnits.publicSales?.productTypeId ?? null) }}
          · {{ formatCurrency(shopChainDisplayUnits.publicSales?.minPrice) }}
        </div>
        <div v-else class="chain-step-missing-label mt-1 text-sm font-medium text-warning">
          {{ t('buildingDetail.salesChain.notConfigured') }}
        </div>
      </div>
    </div>

    <!-- Guidance when chain is incomplete -->
    <div v-if="!shopChainStatus.isChainComplete" class="chain-guidance mt-4 rounded-lg border border-amber-300/50 bg-amber-500/10 p-3">
      <h4 class="chain-guidance-title text-sm font-semibold text-foreground">{{ t('buildingDetail.salesChain.whatRemains') }}</h4>
      <ul class="chain-todo mt-2 list-inside list-disc space-y-1 text-sm text-foreground">
        <li v-if="!shopChainStatus.isPurchaseConfigured">
          {{ t('buildingDetail.salesChain.todoPurchaseProduct') }}
        </li>
        <li v-if="!shopChainStatus.isPublicSalesConfigured">
          {{ t('buildingDetail.salesChain.todoPublicSalesPrice') }}
        </li>
      </ul>
      <p class="chain-action-hint mt-2 text-xs text-muted">{{ t('buildingDetail.salesChain.editHint') }}</p>
    </div>

    <!-- Chain complete celebration -->
    <div v-else class="chain-complete-message mt-4 rounded-lg border border-emerald-300/50 bg-emerald-500/10 p-3">
      <p class="text-sm text-foreground">
        {{
          t('buildingDetail.salesChain.chainCompleteDesc', {
            product: getProductName(shopChainDisplayUnits.publicSales?.productTypeId ?? null),
            price: formatCurrency(shopChainDisplayUnits.publicSales?.minPrice ?? 0),
          })
        }}
      </p>
      <p class="chain-next-step mt-1 text-xs text-muted">{{ t('buildingDetail.salesChain.nextStep') }}</p>
    </div>
  </div>
</template>
