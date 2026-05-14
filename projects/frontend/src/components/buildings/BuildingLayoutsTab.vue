<script setup lang="ts">
import { inject } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const router = useRouter()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  masterLayouts,
  masterLayoutsLoading,
  masterLayoutsError,
  localLayouts,
  layoutName,
  layoutDescription,
  layoutSaving,
  layoutSaveError,
  layoutSaveSuccess,
  layoutDeleteError,
  overwriteConfirmPending,
  masterConnected,
  masterUserEmail,
  saveLayout,
  requestLoadLayout,
  confirmOverwrite,
  cancelOverwrite,
  deleteLayout,
  layoutStructureSummary,
  getUnitColor,
  getLayoutCellType,
} = bd
</script>

<template>
  <section class="layout-section" role="tabpanel" :aria-label="t('buildingDetail.accessibility.buildingLayouts')">
    <div class="layout-header">
      <h4>{{ t('buildingDetail.layouts.title') }}</h4>
    </div>

    <div v-if="overwriteConfirmPending" class="layout-overwrite-confirm" role="alertdialog" aria-modal="true">
      <p class="layout-confirm-title">{{ overwriteConfirmPending.name }}</p>
      <p class="layout-confirm-summary">{{ layoutStructureSummary(overwriteConfirmPending) }}</p>
      <p class="layout-confirm-text">{{ t('buildingDetail.layouts.overwriteConfirm') }}</p>
      <div class="layout-confirm-actions">
        <button class="btn btn-danger btn-sm" @click="confirmOverwrite">{{ t('common.confirm') }}</button>
        <button class="btn btn-ghost btn-sm" @click="cancelOverwrite">{{ t('common.cancel') }}</button>
      </div>
    </div>

    <div v-if="!overwriteConfirmPending" class="layout-save">
      <input type="text" class="form-input" v-model="layoutName" :placeholder="t('buildingDetail.layouts.namePlaceholder')" :aria-label="t('buildingDetail.layouts.namePlaceholder')" />
      <input
        type="text"
        class="form-input layout-desc-input"
        v-model="layoutDescription"
        :placeholder="t('buildingDetail.layouts.descriptionPlaceholder')"
        :aria-label="t('buildingDetail.layouts.descriptionPlaceholder')"
      />
      <button class="btn btn-secondary btn-sm" :disabled="!layoutName.trim() || layoutSaving" @click="saveLayout">
        {{ layoutSaving ? t('buildingDetail.layouts.masterSaving') : t('buildingDetail.layouts.save') }}
      </button>
      <p v-if="layoutSaveSuccess" class="layout-save-success">✓ {{ t('buildingDetail.layouts.saveSuccess') }}</p>
      <p v-if="layoutSaveError" class="layout-save-error">{{ layoutSaveError }}</p>
      <p v-if="layoutDeleteError" class="layout-save-error">{{ layoutDeleteError }}</p>
    </div>

    <template v-if="masterConnected">
      <div class="layout-cloud-header">
        <span class="layout-cloud-badge">☁ {{ t('buildingDetail.layouts.cloudBadge') }}</span>
        <span class="layout-connected-email">{{ t('buildingDetail.layouts.masterConnected', { email: masterUserEmail }) }}</span>
      </div>
      <p v-if="masterLayoutsLoading" class="layout-empty">{{ t('common.loading') }}</p>
      <p v-else-if="masterLayoutsError" class="layout-save-error">{{ masterLayoutsError }}</p>
      <div v-else-if="masterLayouts.length > 0" class="layout-list">
        <div v-for="layout in masterLayouts" :key="layout.id ?? layout.name" class="layout-item">
          <div class="layout-mini-grid" aria-hidden="true">
            <template v-for="row in 4" :key="row">
              <template v-for="col in 4" :key="`${row}-${col}`">
                <div
                  class="layout-mini-cell"
                  :class="{ 'layout-mini-cell-occupied': getLayoutCellType(layout, col - 1, row - 1) !== null }"
                  :style="getLayoutCellType(layout, col - 1, row - 1) ? { background: getUnitColor(getLayoutCellType(layout, col - 1, row - 1)!) } : {}"
                  :title="getLayoutCellType(layout, col - 1, row - 1) ? t(`buildingDetail.unitTypes.${getLayoutCellType(layout, col - 1, row - 1)}`) : ''"
                />
              </template>
            </template>
          </div>
          <div class="layout-item-info">
            <span class="layout-name">{{ layout.name }}</span>
            <span v-if="layout.description" class="layout-desc">{{ layout.description }}</span>
            <span class="layout-meta">{{ layoutStructureSummary(layout) }}</span>
          </div>
          <div class="layout-item-actions">
            <button class="btn btn-ghost btn-sm" @click="requestLoadLayout(layout)">{{ t('buildingDetail.layouts.load') }}</button>
            <button class="btn btn-ghost btn-sm" @click="deleteLayout(layout)">{{ t('buildingDetail.layouts.delete') }}</button>
          </div>
        </div>
      </div>
      <p v-else class="layout-empty">{{ t('buildingDetail.layouts.empty') }}</p>
      <p class="layout-sync-hint">{{ t('buildingDetail.layouts.cloudSyncHint') }}</p>
    </template>

    <template v-else>
      <div class="layout-master-connect">
        <p class="layout-connect-body">{{ t('buildingDetail.layouts.masterConnectBody') }}</p>
        <button class="btn btn-secondary btn-sm" @click="router.push('/login')">
          {{ t('buildingDetail.layouts.masterConnect') }}
        </button>
      </div>

      <div class="layout-local-section">
        <div class="layout-local-header">
          <span class="layout-local-badge">{{ t('buildingDetail.layouts.localBadge') }}</span>
        </div>
        <div v-if="localLayouts.length > 0" class="layout-list">
          <div v-for="layout in localLayouts" :key="layout.name" class="layout-item">
            <div class="layout-mini-grid" aria-hidden="true">
              <template v-for="row in 4" :key="row">
                <template v-for="col in 4" :key="`${row}-${col}`">
                  <div
                    class="layout-mini-cell"
                    :class="{ 'layout-mini-cell-occupied': getLayoutCellType(layout, col - 1, row - 1) !== null }"
                    :style="getLayoutCellType(layout, col - 1, row - 1) ? { background: getUnitColor(getLayoutCellType(layout, col - 1, row - 1)!) } : {}"
                    :title="getLayoutCellType(layout, col - 1, row - 1) ? t(`buildingDetail.unitTypes.${getLayoutCellType(layout, col - 1, row - 1)}`) : ''"
                  />
                </template>
              </template>
            </div>
            <div class="layout-item-info">
              <span class="layout-name">{{ layout.name }}</span>
              <span v-if="layout.description" class="layout-desc">{{ layout.description }}</span>
              <span class="layout-meta">{{ layoutStructureSummary(layout) }}</span>
            </div>
            <div class="layout-item-actions">
              <button class="btn btn-ghost btn-sm" @click="requestLoadLayout(layout)">{{ t('buildingDetail.layouts.load') }}</button>
              <button class="btn btn-ghost btn-sm" @click="deleteLayout(layout)">{{ t('buildingDetail.layouts.delete') }}</button>
            </div>
          </div>
        </div>
        <p v-else class="layout-empty">{{ t('buildingDetail.layouts.empty') }}</p>
      </div>
    </template>
  </section>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>