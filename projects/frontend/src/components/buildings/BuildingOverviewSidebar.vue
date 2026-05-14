<script setup lang="ts">
import { computed, inject, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import BuildingEditBasicDataTab from '@/components/buildings/BuildingEditBasicDataTab.vue'
import BuildingLayoutsTab from '@/components/buildings/BuildingLayoutsTab.vue'
import BuildingOverviewTab from '@/components/buildings/BuildingOverviewTab.vue'
import BuildingBankAccountTab from '@/components/buildings/BuildingBankAccountTab.vue'
import BuildingEnergyPanel from '@/components/buildings/BuildingEnergyPanel.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const bd = inject(BUILDING_DETAIL_KEY)!
const { isEditing } = bd

type BuildingEditTab = 'basic-data' | 'energy' | 'bank-account' | 'layouts'

const overviewTabs = [
  { key: 'overview', label: t('buildingDetail.overviewTab') },
  { key: 'bankAccount', label: t('buildingDetail.bankAccountTab') },
]

const editTabs = computed(() => [
  { key: 'basic-data', label: t('buildingDetail.editBuildingTabBasicData') },
  { key: 'energy', label: t('buildingDetail.editBuildingTabEnergy') },
  { key: 'bank-account', label: t('buildingDetail.editBuildingTabBankAccount') },
  { key: 'layouts', label: t('buildingDetail.editBuildingTabLayouts') },
])

function normalizeBuildingEditTab(value: unknown): BuildingEditTab {
  if (value === 'energy') return 'energy'
  if (value === 'bank-account' || value === 'bankAccount') return 'bank-account'
  if (value === 'layouts') return 'layouts'
  return 'basic-data'
}

const selectedOverviewTab = computed(() => {
  const tab = route.query.tab as string | undefined
  return tab === 'bankAccount' ? 'bankAccount' : 'overview'
})

const selectedEditTab = computed(() => normalizeBuildingEditTab(route.params.tab))

watch(
  () => route.params.tab,
  (tab) => {
    if (route.name !== 'building-detail-edit') return
    const normalized = normalizeBuildingEditTab(tab)
    if (tab === normalized) return

    void router.replace({
      name: 'building-detail-edit',
      params: { id: route.params.id as string, tab: normalized },
      query: route.query,
    })
  },
  { immediate: true },
)

function selectOverviewTab(key: string) {
  router.replace({ query: { ...route.query, tab: key === 'overview' ? undefined : key } })
}

function selectEditTab(key: BuildingEditTab) {
  void router.replace({
    name: 'building-detail-edit',
    params: { id: route.params.id as string, tab: key },
    query: route.query,
  })
}
</script>

<template>
  <div class="sidebar sidebar-placeholder">
    <div class="unit-config">
      <div class="unit-config-header">
        <h3>{{ isEditing ? t('buildingDetail.buildingEditTabs') : t('buildingDetail.overview.title') }}</h3>
      </div>
      <div v-if="isEditing" class="unit-detail placeholder-detail">
        <nav class="unit-detail-tabs" role="tablist" :aria-label="t('buildingDetail.buildingEditTabs')">
          <button
            v-for="tab in editTabs"
            :key="tab.key"
            class="unit-tab-btn"
            :class="selectedEditTab === tab.key ? 'unit-tab-btn--active' : ''"
            role="tab"
            :aria-selected="selectedEditTab === tab.key"
            @click="selectEditTab(tab.key as BuildingEditTab)"
          >
            {{ tab.label }}
          </button>
        </nav>

        <BuildingEditBasicDataTab v-if="selectedEditTab === 'basic-data'" />
        <BuildingEnergyPanel v-else-if="selectedEditTab === 'energy'" />
        <BuildingBankAccountTab v-else-if="selectedEditTab === 'bank-account'" />
        <BuildingLayoutsTab v-else />
      </div>
      <div v-else class="unit-detail building-overview-detail">
        <!-- Overview tabs nav -->
        <nav
          class="unit-detail-tabs flex flex-nowrap items-center gap-1 overflow-x-auto border-b border-divider bg-bg px-4 py-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
          :aria-label="t('buildingDetail.overview.title')"
        >
          <button
            v-for="tab in overviewTabs"
            :key="tab.key"
            class="unit-tab-btn inline-flex shrink-0 items-center rounded-md border border-transparent px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted transition-colors hover:text-foreground"
            :class="selectedOverviewTab === tab.key ? 'unit-tab-btn--active border-primary/40 bg-primary/10 text-primary' : 'hover:border-divider hover:bg-surface'"
            @click="selectOverviewTab(tab.key)"
          >
            {{ tab.label }}
          </button>
        </nav>

        <!-- Tab: P&L & Statistics -->
        <template v-if="selectedOverviewTab === 'overview'">
          <BuildingOverviewTab />
        </template>

        <!-- Tab: Bank Account -->
        <template v-else-if="selectedOverviewTab === 'bankAccount'">
          <BuildingBankAccountTab />
        </template>
      </div>
    </div>
  </div>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>
<style scoped src="./BuildingSidebar.analytics.css"></style>
<style scoped src="./BuildingSidebar.exchange.css"></style>
