<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { chainDisplayUnits, chainStatus, showProductionChainPanel, shopChainDisplayUnits, shopChainStatus, showSalesChainPanel, getResourceName, getProductName, formatCurrency, dismissProductionChainPanel, dismissSalesChainPanel } = bd
</script>

<template>
<!-- Production chain status panel: shown for factories with the starter layout saved -->
<div v-if="showProductionChainPanel" class="production-chain-panel" role="region" aria-label="production chain status">
  <div class="chain-panel-header">
    <h3 class="chain-panel-title">⚙️ {{ t('buildingDetail.productionChain.title') }}</h3>
    <span v-if="chainStatus.isChainComplete" class="chain-status-badge chain-status-badge--complete">✅ {{ t('buildingDetail.productionChain.chainComplete') }}</span>
    <span v-else class="chain-status-badge chain-status-badge--incomplete">⚠️ {{ t('buildingDetail.productionChain.chainIncomplete') }}</span>
    <button class="chain-panel-dismiss" :aria-label="t('buildingDetail.productionChain.dismissAriaLabel')" @click="dismissProductionChainPanel">
      {{ t('buildingDetail.productionChain.dismiss') }}
    </button>
  </div>

  <div class="chain-flow" role="list" aria-label="production chain steps">
    <!-- PURCHASE step -->
    <div class="chain-step" :class="chainStatus.isPurchaseConfigured ? 'chain-step--configured' : 'chain-step--missing'" role="listitem">
      <div class="chain-step-icon">🛒</div>
      <div class="chain-step-type">{{ t('buildingDetail.unitTypes.PURCHASE') }}</div>
      <div v-if="chainStatus.isPurchaseConfigured" class="chain-step-value">
        {{ chainDisplayUnits.purchase?.resourceTypeId ? getResourceName(chainDisplayUnits.purchase.resourceTypeId) : getProductName(chainDisplayUnits.purchase?.productTypeId ?? null) }}
      </div>
      <div v-else class="chain-step-missing-label">
        {{ t('buildingDetail.productionChain.notConfigured') }}
      </div>
    </div>

    <div class="chain-arrow" aria-hidden="true">→</div>

    <!-- MANUFACTURING step -->
    <div class="chain-step" :class="chainStatus.isManufacturingConfigured ? 'chain-step--configured' : 'chain-step--missing'" role="listitem">
      <div class="chain-step-icon">🏭</div>
      <div class="chain-step-type">{{ t('buildingDetail.unitTypes.MANUFACTURING') }}</div>
      <div v-if="chainStatus.isManufacturingConfigured" class="chain-step-value">
        {{ getProductName(chainDisplayUnits.manufacturing?.productTypeId ?? null) }}
      </div>
      <div v-else class="chain-step-missing-label">
        {{ t('buildingDetail.productionChain.notConfigured') }}
      </div>
    </div>

    <div class="chain-arrow" aria-hidden="true">→</div>

    <!-- STORAGE step -->
    <div class="chain-step" :class="chainStatus.isStoragePresent ? 'chain-step--configured' : 'chain-step--missing'" role="listitem">
      <div class="chain-step-icon">📦</div>
      <div class="chain-step-type">{{ t('buildingDetail.unitTypes.STORAGE') }}</div>
      <div class="chain-step-value">
        {{ chainStatus.isManufacturingConfigured ? getProductName(chainDisplayUnits.manufacturing?.productTypeId ?? null) : t('buildingDetail.productionChain.storageDesc') }}
      </div>
    </div>
  </div>

  <!-- Guidance when chain is incomplete -->
  <div v-if="!chainStatus.isChainComplete" class="chain-guidance">
    <h4 class="chain-guidance-title">{{ t('buildingDetail.productionChain.whatRemains') }}</h4>
    <ul class="chain-todo">
      <li v-if="!chainStatus.isPurchaseConfigured">
        {{ t('buildingDetail.productionChain.todoSelectResource') }}
      </li>
      <li v-if="!chainStatus.isManufacturingConfigured">
        {{ t('buildingDetail.productionChain.todoSelectProduct') }}
      </li>
    </ul>
    <p class="chain-action-hint">{{ t('buildingDetail.productionChain.editHint') }}</p>
  </div>

  <!-- Chain complete celebration -->
  <div v-else class="chain-complete-message">
    <p>
      {{
        t('buildingDetail.productionChain.chainCompleteDesc', {
          product: getProductName(chainDisplayUnits.manufacturing?.productTypeId ?? null),
          resource: chainDisplayUnits.purchase?.resourceTypeId ? getResourceName(chainDisplayUnits.purchase.resourceTypeId) : getProductName(chainDisplayUnits.purchase?.productTypeId ?? null),
        })
      }}
    </p>
    <p class="chain-next-step">{{ t('buildingDetail.productionChain.nextStep') }}</p>
  </div>
</div>

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
        {{
          shopChainDisplayUnits.purchase?.resourceTypeId ? getResourceName(shopChainDisplayUnits.purchase.resourceTypeId) : getProductName(shopChainDisplayUnits.purchase?.productTypeId ?? null)
        }}
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
