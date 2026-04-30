<template>
  <section class="lender-cta-section flex flex-col gap-6" aria-label="Lender action">
    <h2 class="text-2xl font-bold text-body">{{ t('bank.becomeALender') }}</h2>

    <!-- Unauthenticated: prompt login -->
    <div
      v-if="!isAuthenticated"
      class="lender-cta-card lender-cta-login flex flex-col gap-5 rounded-3xl border border-divider bg-card p-6 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:p-8"
    >
      <div class="lender-cta-icon text-3xl" aria-hidden="true">🏦</div>
      <div class="lender-cta-body flex-1 min-w-0">
        <h3 class="lender-cta-title mb-1 text-base font-semibold text-body">{{ t('bank.loginToLendTitle') }}</h3>
        <p class="lender-cta-description mb-0 text-sm leading-snug text-muted">{{ t('bank.loginToLendDescription') }}</p>
      </div>
      <router-link to="/login" class="btn btn-secondary lender-cta-btn shrink-0 whitespace-nowrap" aria-label="Log in to offer loans">
        {{ t('bank.loginToLend') }}
      </router-link>
    </div>

    <!-- Authenticated, no bank building: acquire CTA -->
    <div
      v-else-if="!hasBankBuilding"
      class="lender-cta-card lender-cta-acquire flex flex-col gap-5 rounded-3xl border border-divider bg-card p-6 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:p-8"
    >
      <div class="lender-cta-icon text-3xl" aria-hidden="true">🏦</div>
      <div class="lender-cta-body flex-1 min-w-0">
        <h3 class="lender-cta-title mb-1 text-base font-semibold text-body">{{ t('bank.noBankCTATitle') }}</h3>
        <p class="lender-cta-description mb-0 text-sm leading-snug text-muted">{{ t('bank.noBankCTADescription') }}</p>
      </div>
      <button class="btn btn-primary lender-cta-btn shrink-0 whitespace-nowrap" aria-label="Acquire a Bank building" @click="$emit('acquire-bank')">
        {{ t('bank.acquireBank') }}
      </button>
    </div>

    <!-- Authenticated, has bank: manage bank CTA -->
    <div v-else class="lender-cta-card lender-cta-manage flex flex-col gap-5 rounded-3xl border border-brand/40 bg-card p-6 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:p-8">
      <div class="lender-cta-icon text-3xl" aria-hidden="true">🏦</div>
      <div class="lender-cta-body flex-1 min-w-0">
        <h3 class="lender-cta-title mb-1 text-base font-semibold text-body">{{ t('bank.hasBankCTATitle') }}</h3>
        <p class="lender-cta-description mb-0 text-sm leading-snug text-muted">{{ t('bank.hasBankCTADescription') }}</p>
        <span class="lender-bank-name text-xs font-medium text-brand">{{ firstBankBuildingName }}</span>
      </div>
      <button class="btn btn-primary lender-cta-btn shrink-0 whitespace-nowrap" @click="$emit('manage-bank')">
        {{ t('bank.manageBank') }}
      </button>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

defineProps<{
  isAuthenticated: boolean
  hasBankBuilding: boolean
  firstBankBuildingName?: string
}>()

defineEmits<{
  'acquire-bank': []
  'manage-bank': []
}>()
</script>
