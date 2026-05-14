<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import BuildingBankAccountPanel from '@/components/buildings/BuildingBankAccountPanel.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { building, loading, cityCurrencyCode } = bd
</script>

<template>
  <div class="unit-insight-card building-bank-account-card">
    <h5>{{ t('buildingBankAccount.panelTitle') }}</h5>
    <BuildingBankAccountPanel
      :building-id="building?.id ?? ''"
      :company-id="building?.companyId ?? ''"
      :currency-code="cityCurrencyCode"
      :loading="loading"
      @updated="bd.loadBuilding()"
    />
  </div>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>
