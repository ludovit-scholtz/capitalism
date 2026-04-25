<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  chainDisplayUnits,
  chainStatus,
  showProductionChainPanel,
  shopChainDisplayUnits,
  shopChainStatus,
  showSalesChainPanel,
  getResourceName,
  getProductName,
  formatCurrency,
  dismissProductionChainPanel,
  dismissSalesChainPanel,
} = bd
</script>

<template>
  <!-- Production chain status panel: shown for factories with the starter layout saved -->

  <!-- Sales chain status panel: shown for sales shops with units saved -->
  <div v-if="showSalesChainPanel" class="production-chain-panel" role="region" aria-label="sales chain status">
    <div class="chain-panel-header">
      <h3 class="chain-panel-title">🏪 {{ t('buildingDetail.salesChain.title') }}</h3>
      <span v-if="shopChainStatus.isChainComplete" class="chain-status-badge chain-status-badge--complete">✅ {{ t('buildingDetail.salesChain.chainComplete') }}</span>
      <span v-else class="chain-status-badge chain-status-badge--incomplete">⚠️ {{ t('buildingDetail.salesChain.chainIncomplete') }}</span>
      <button class="chain-panel-dismiss" :aria-label="t('buildingDetail.salesChain.dismissAriaLabel')" @click="dismissSalesChainPanel">{{ t('buildingDetail.salesChain.dismiss') }}</button>
    </div>

    <div class="chain-flow" role="list" aria-label="sales chain steps">
      <!-- PURCHASE step -->
      <div class="chain-step" :class="shopChainStatus.isPurchaseConfigured ? 'chain-step--configured' : 'chain-step--missing'" role="listitem">
        <div class="chain-step-icon">🛒</div>
        <div class="chain-step-type">{{ t('buildingDetail.unitTypes.PURCHASE') }}</div>
        <div v-if="shopChainStatus.isPurchaseConfigured" class="chain-step-value">
          {{ shopChainDisplayUnits.purchase?.resourceTypeId ? getResourceName(shopChainDisplayUnits.purchase.resourceTypeId) : getProductName(shopChainDisplayUnits.purchase?.productTypeId ?? null) }}
        </div>
        <div v-else class="chain-step-missing-label">
          {{ t('buildingDetail.salesChain.notConfigured') }}
        </div>
      </div>

      <div class="chain-arrow" aria-hidden="true">→</div>

      <!-- PUBLIC_SALES step -->
      <div class="chain-step" :class="shopChainStatus.isPublicSalesConfigured ? 'chain-step--configured' : 'chain-step--missing'" role="listitem">
        <div class="chain-step-icon">💲</div>
        <div class="chain-step-type">{{ t('buildingDetail.unitTypes.PUBLIC_SALES') }}</div>
        <div v-if="shopChainStatus.isPublicSalesConfigured" class="chain-step-value">
          {{ getProductName(shopChainDisplayUnits.publicSales?.productTypeId ?? null) }}
          · {{ formatCurrency(shopChainDisplayUnits.publicSales?.minPrice) }}
        </div>
        <div v-else class="chain-step-missing-label">
          {{ t('buildingDetail.salesChain.notConfigured') }}
        </div>
      </div>
    </div>

    <!-- Guidance when chain is incomplete -->
    <div v-if="!shopChainStatus.isChainComplete" class="chain-guidance">
      <h4 class="chain-guidance-title">{{ t('buildingDetail.salesChain.whatRemains') }}</h4>
      <ul class="chain-todo">
        <li v-if="!shopChainStatus.isPurchaseConfigured">
          {{ t('buildingDetail.salesChain.todoPurchaseProduct') }}
        </li>
        <li v-if="!shopChainStatus.isPublicSalesConfigured">
          {{ t('buildingDetail.salesChain.todoPublicSalesPrice') }}
        </li>
      </ul>
      <p class="chain-action-hint">{{ t('buildingDetail.salesChain.editHint') }}</p>
    </div>

    <!-- Chain complete celebration -->
    <div v-else class="chain-complete-message">
      <p>
        {{
          t('buildingDetail.salesChain.chainCompleteDesc', {
            product: getProductName(shopChainDisplayUnits.publicSales?.productTypeId ?? null),
            price: formatCurrency(shopChainDisplayUnits.publicSales?.minPrice ?? 0),
          })
        }}
      </p>
      <p class="chain-next-step">{{ t('buildingDetail.salesChain.nextStep') }}</p>
    </div>
  </div>
</template>
