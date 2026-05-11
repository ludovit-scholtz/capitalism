<script setup lang="ts">
import { inject } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const router = useRouter()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  building,
  isBuildingUsedAsCollateral,
  collateralLoanCount,
  isLoanDefaulted,
  ticksUntilDestruction,
  formatBuildingType,
} = bd
</script>

<template>
  <div class="building-header mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5">
    <div class="building-title mb-3 flex flex-wrap items-center gap-3">
      <h1 class="m-0 text-2xl font-semibold leading-tight text-foreground">{{ building?.name }}</h1>
      <span class="building-type-badge inline-flex items-center rounded-full bg-primary px-3 py-1 text-xs font-semibold uppercase tracking-wide text-primary-foreground">{{
        formatBuildingType(building?.type ?? '')
      }}</span>
      <!-- Destroyed badge -->
      <span
        v-if="building?.destroyedAtUtc"
        class="destroyed-badge inline-flex items-center gap-1 rounded-full border border-red-300/60 bg-red-500/15 px-3 py-1 text-xs font-semibold text-red-700 dark:text-red-300"
        :title="t('buildingDetail.destroyedHint')"
      >
        <font-awesome-icon icon="skull" class="text-[10px]" />
        {{ t('buildingDetail.destroyedBadge') }}
      </span>
      <!-- Loan default badge -->
      <span
        v-if="isLoanDefaulted && !building?.destroyedAtUtc"
        class="loan-default-badge inline-flex items-center gap-1 rounded-full border border-red-400/70 bg-red-500/20 px-3 py-1 text-xs font-semibold text-red-700 dark:text-red-300"
        :title="t('buildingDetail.loanDefaultHint')"
      >
        <font-awesome-icon icon="triangle-exclamation" class="text-[10px]" />
        {{ t('buildingDetail.loanDefaultBadge') }}
      </span>
    </div>
    <div class="building-meta flex flex-wrap items-center gap-2">
      <span class="meta-pill inline-flex items-center gap-1.5 rounded-full border border-divider bg-surface px-3 py-1.5 text-sm">
        <span class="meta-label text-xs text-muted">{{ t('common.level') }}</span>
        <span class="meta-value font-semibold text-foreground">{{ building?.level }}</span>
      </span>
      <span class="meta-pill inline-flex items-center gap-1.5 rounded-full border border-divider bg-surface px-3 py-1.5 text-sm">
        <span class="meta-label text-xs text-muted">{{ t('buildings.power') }}</span>
        <span class="meta-value font-semibold text-foreground">{{ building?.powerConsumption }} {{ t('buildings.powerUnit') }}</span>
      </span>
      <span
        v-if="building?.powerStatus"
        class="meta-pill power-status-pill inline-flex items-center gap-1.5 rounded-full border border-divider bg-surface px-3 py-1.5 text-sm"
        :class="{
          'power-status-powered border-green-300/60 bg-green-500/10 text-green-700 dark:text-green-300': building?.powerStatus === 'POWERED',
          'power-status-constrained border-amber-300/60 bg-amber-500/15 text-amber-700 dark:text-amber-300': building?.powerStatus === 'CONSTRAINED',
          'power-status-offline border-red-300/60 bg-red-500/10 text-red-700 dark:text-red-300': building?.powerStatus === 'OFFLINE',
        }"
        :title="t(`powerGrid.buildingStatusHint.${building?.powerStatus}`, { percent: 50 })"
        role="status"
      >
        <span class="meta-label text-xs text-current/80">{{ t('powerGrid.powerCardTitle') }}</span>
        <span class="meta-value font-semibold">{{ t(`powerGrid.buildingStatus.${building?.powerStatus}`) }}</span>
      </span>
      <span
        v-if="!building?.destroyedAtUtc"
        class="meta-pill inline-flex items-center rounded-full border border-divider bg-surface px-3 py-1.5 text-sm font-medium text-foreground"
        :class="building?.isForSale ? 'for-sale border-green-300/60 bg-green-500/10 text-green-700 dark:text-green-300' : ''"
      >
        {{ building?.isForSale ? t('buildingDetail.forSale') : t('buildingDetail.notForSale') }}
      </span>
      <span
        v-if="building?.isCollateralized && !building?.destroyedAtUtc"
        class="meta-pill inline-flex items-center gap-1 rounded-full border border-amber-300/60 bg-amber-500/10 px-3 py-1.5 text-sm font-medium text-amber-700 dark:text-amber-300"
        :title="t('buildingDetail.collateralLockedTooltip')"
      >
        🔒 {{ t('buildingDetail.collateralLockedBadge') }}
      </span>
      <!-- Sell button — hidden for destroyed buildings -->
      <button
        v-if="!building?.destroyedAtUtc"
        class="btn btn-secondary btn-sm ml-auto"
        :disabled="isBuildingUsedAsCollateral || building?.isCollateralized"
        :title="isBuildingUsedAsCollateral || building?.isCollateralized ? t('buildingDetail.collateralLockedTooltip') : ''"
        @click="router.push(`/building/${building?.id}/sell`)"
      >
        {{ building?.isForSale ? t('buildingDetail.editSale') : t('buildingDetail.sellBuilding') }}
      </button>
    </div>

    <!-- Loan default alert banner -->
    <div
      v-if="isLoanDefaulted && !building?.destroyedAtUtc"
      class="loan-default-alert mt-3 rounded-lg border border-red-400/60 bg-red-500/10 px-4 py-3 text-sm text-red-700 dark:text-red-300"
      role="alert"
    >
      <p class="font-semibold">
        <font-awesome-icon icon="triangle-exclamation" class="mr-1.5" />
        {{ t('buildingDetail.loanDefaultTitle') }}
      </p>
      <p class="mt-1 text-xs">
        {{ t('buildingDetail.loanDefaultBody') }}
      </p>
      <p v-if="ticksUntilDestruction !== null" class="mt-1 text-xs font-medium">
        {{ t('buildingDetail.loanDefaultDestruction', { ticks: ticksUntilDestruction }) }}
      </p>
    </div>

    <!-- Collateral warning with loan count -->
    <p v-if="isBuildingUsedAsCollateral && !building?.destroyedAtUtc" class="collateral-warning mt-2 rounded-lg border border-amber-300/60 bg-amber-500/10 px-3 py-2 text-xs text-amber-700 dark:text-amber-300">
      <font-awesome-icon icon="lock" class="mr-1" />
      {{ t('buildingDetail.collateralBlockedByLoans', { count: collateralLoanCount }) }}
      <span v-if="building?.foreclosureTicksRemaining !== null && building?.foreclosureTicksRemaining !== undefined" class="ml-1">
        {{ t('buildingDetail.loanDefaultDestruction', { ticks: building.foreclosureTicksRemaining }) }}
      </span>
    </p>

    <!-- Destroyed notice -->
    <p v-if="building?.destroyedAtUtc" class="mt-2 rounded-lg border border-red-300/60 bg-red-500/10 px-3 py-2 text-xs text-red-700 dark:text-red-300">
      <font-awesome-icon icon="skull" class="mr-1" />
      {{ t('buildingDetail.destroyedHint') }}
    </p>
  </div>
</template>
