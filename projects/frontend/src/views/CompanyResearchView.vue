<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'

const { t } = useI18n()
const route = useRoute()
const companyId = computed(() => route.params.companyId as string)

const loading = ref(true)
const error = ref<string | null>(null)

interface BrandState {
  id: string
  name: string
  scope: string
  productTypeId: string | null
  productName: string | null
  industryCategory: string | null
  quality: number
  marketingQuality: number
  combinedBrandQuality: number
  accumulatedResearchBudget: number | null
  baseResearchBudget: number | null
  marketingEfficiencyMultiplier: number
}

interface BrandQualityOverview {
  companyId: string
  totalResearchBudgetUsd: number
  brands: BrandState[]
}

const overview = ref<BrandQualityOverview | null>(null)

const BRAND_QUALITY_OVERVIEW_QUERY = `
  query BrandQualityOverview($companyId: UUID!) {
    brandQualityOverview(companyId: $companyId) {
      companyId
      totalResearchBudgetUsd
      brands {
        id
        name
        scope
        productTypeId
        productName
        industryCategory
        quality
        marketingQuality
        combinedBrandQuality
        accumulatedResearchBudget
        baseResearchBudget
        marketingEfficiencyMultiplier
      }
    }
  }
`

async function loadData(isRefresh = false) {
  if (!isRefresh) loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ brandQualityOverview: BrandQualityOverview | null }>(
      BRAND_QUALITY_OVERVIEW_QUERY,
      { companyId: companyId.value },
    )
    overview.value = data.brandQualityOverview
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

onMounted(() => loadData())
useTickRefresh(() => loadData(true))

function formatUsd(amount: number | null | undefined): string {
  if (amount == null) return '—'
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: 'USD',
    notation: 'compact',
    maximumFractionDigits: 1,
  }).format(amount)
}

/** Combined quality level on 0–10 scale. */
function qualityLevel(brand: BrandState): number {
  return Math.min(10, Math.round(brand.combinedBrandQuality * 100) / 10)
}

/** Price premium percentage (0–50%) from combined quality. */
function pricePremiumPct(brand: BrandState): string {
  return (brand.combinedBrandQuality * 50).toFixed(1)
}

/** CSS colour class for a quality level. */
function qualityLevelColorClass(level: number): string {
  if (level >= 8) return 'quality-tier-gold'
  if (level >= 5) return 'quality-tier-green'
  if (level >= 2) return 'quality-tier-blue'
  return 'quality-tier-dim'
}

const productBrands = computed(() =>
  (overview.value?.brands ?? []).filter((b) => b.scope === 'PRODUCT'),
)

const categoryBrands = computed(() =>
  (overview.value?.brands ?? []).filter((b) => b.scope === 'CATEGORY'),
)

const companyBrands = computed(() =>
  (overview.value?.brands ?? []).filter((b) => b.scope === 'COMPANY'),
)
</script>

<template>
  <div class="research-dashboard pt-6 pb-16 lg:pt-8 lg:pb-20">
    <div class="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8">
      <!-- Header -->
      <div class="research-header mb-8">
        <h1 class="research-title mb-1 text-2xl font-bold text-foreground">
          🔬 {{ t('research.dashboard.title') }}
        </h1>
        <p class="research-subtitle text-sm text-muted">{{ t('research.dashboard.subtitle') }}</p>
        <RouterLink
          :to="{ name: 'ledger', params: { companyId } }"
          class="mt-2 inline-flex items-center gap-1 text-xs text-primary hover:underline"
        >
          ← {{ t('nav.ledger') }}
        </RouterLink>
      </div>

      <!-- Loading -->
      <div v-if="loading" class="research-loading py-12 text-center text-muted">
        {{ t('common.loading') }}
      </div>

      <!-- Error -->
      <div v-else-if="error" class="research-error rounded-lg border border-error/30 bg-error/10 p-4 text-sm text-error">
        {{ error }}
      </div>

      <!-- No data (foreign company or no brands yet) -->
      <div
        v-else-if="!overview || overview.brands.length === 0"
        class="research-empty-state rounded-xl border border-divider bg-card p-10 text-center"
      >
        <div class="mb-4 text-5xl">🔬</div>
        <h2 class="mb-2 text-lg font-semibold text-foreground">{{ t('research.dashboard.noRdBuildings') }}</h2>
        <p class="text-sm text-muted">{{ t('research.dashboard.noRdBuildingsCta') }}</p>
      </div>

      <!-- Dashboard content -->
      <template v-else>
        <!-- Summary card -->
        <div class="research-summary-card mb-8 flex flex-wrap gap-6 rounded-xl border border-divider bg-card p-6">
          <div class="research-stat flex flex-col">
            <span class="text-xs uppercase tracking-wide text-muted">{{ t('research.dashboard.totalBudget') }}</span>
            <span class="text-2xl font-bold text-foreground">{{ formatUsd(overview.totalResearchBudgetUsd) }}</span>
          </div>
          <div class="research-stat flex flex-col">
            <span class="text-xs uppercase tracking-wide text-muted">{{ t('research.dashboard.productCol') }}s</span>
            <span class="text-2xl font-bold text-foreground">{{ productBrands.length }}</span>
          </div>
        </div>

        <!-- Product quality table -->
        <section v-if="productBrands.length > 0" class="research-section mb-8">
          <h2 class="research-section-title mb-4 text-lg font-semibold text-foreground">
            🧪 {{ t('research.qualityLabel') }}
          </h2>
          <div class="overflow-x-auto rounded-xl border border-divider bg-card">
            <table class="research-table w-full text-sm">
              <thead>
                <tr class="border-b border-divider text-left text-xs uppercase tracking-wide text-muted">
                  <th class="px-5 py-4">{{ t('research.dashboard.productCol') }}</th>
                  <th class="px-5 py-4 text-center">{{ t('research.dashboard.qualityLevelCol') }}</th>
                  <th class="px-5 py-4 text-center">{{ t('research.dashboard.pricePremiumCol') }}</th>
                  <th class="px-5 py-4 text-right">{{ t('research.dashboard.rdQualityCol') }}</th>
                  <th class="px-5 py-4 text-right">{{ t('research.dashboard.marketingQualityCol') }}</th>
                  <th class="px-5 py-4 text-right">{{ t('research.dashboard.budgetCol') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="brand in productBrands"
                  :key="brand.id"
                  class="research-row border-b border-divider/50 last:border-0 hover:bg-surface/50"
                >
                  <td class="px-5 py-4 font-medium text-foreground">{{ brand.productName || brand.name }}</td>
                  <td class="px-5 py-4 text-center">
                    <span
                      class="quality-level-badge inline-flex items-center rounded-full px-2.5 py-1 text-xs font-bold"
                      :class="qualityLevelColorClass(qualityLevel(brand))"
                    >
                      Lv {{ qualityLevel(brand) }}
                    </span>
                  </td>
                  <td class="px-5 py-4 text-center">
                    <span class="font-semibold text-emerald-400">
                      +{{ pricePremiumPct(brand) }}%
                    </span>
                  </td>
                  <td class="px-5 py-4 text-right">
                    <div class="flex flex-col items-end gap-1">
                      <span class="font-medium text-foreground">{{ (brand.quality * 100).toFixed(1) }}%</span>
                      <div class="quality-bar-bg h-1.5 w-24 overflow-hidden rounded-full bg-border">
                        <div
                          class="quality-bar-fill h-full rounded-full bg-primary transition-[width] duration-300"
                          :style="{ width: `${(brand.quality * 100).toFixed(1)}%` }"
                        ></div>
                      </div>
                    </div>
                  </td>
                  <td class="px-5 py-4 text-right">
                    <div class="flex flex-col items-end gap-1">
                      <span class="font-medium text-foreground">{{ (brand.marketingQuality * 100).toFixed(1) }}%</span>
                      <div class="quality-bar-bg h-1.5 w-24 overflow-hidden rounded-full bg-border">
                        <div
                          class="quality-bar-fill h-full rounded-full bg-violet-500 transition-[width] duration-300"
                          :style="{ width: `${(brand.marketingQuality * 100).toFixed(1)}%` }"
                        ></div>
                      </div>
                    </div>
                  </td>
                  <td class="px-5 py-4 text-right font-mono text-foreground">
                    {{ formatUsd(brand.accumulatedResearchBudget) }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Brand quality section -->
        <section v-if="categoryBrands.length > 0 || companyBrands.length > 0" class="research-section mb-8">
          <h2 class="research-section-title mb-4 text-lg font-semibold text-foreground">
            ✨ {{ t('research.marketingEfficiencyLabel') }}
          </h2>
          <div class="flex flex-wrap gap-4">
            <div
              v-for="brand in [...categoryBrands, ...companyBrands]"
              :key="brand.id"
              class="brand-quality-card flex-1 basis-64 rounded-xl border border-divider bg-card p-5"
            >
              <div class="mb-2 flex items-center gap-2">
                <span class="font-semibold text-foreground">{{ brand.productName || brand.name }}</span>
                <span class="rounded-full bg-primary/10 px-1.5 py-0.5 text-[0.6875rem] font-semibold uppercase text-primary">
                  {{ brand.scope }}
                </span>
              </div>
              <div class="mt-2 flex items-center justify-between text-sm">
                <span class="text-muted">{{ t('research.marketingEfficiencyLabel') }}</span>
                <span class="font-bold text-emerald-400">{{ brand.marketingEfficiencyMultiplier.toFixed(2) }}×</span>
              </div>
              <div v-if="brand.quality > 0" class="mt-1 flex items-center justify-between text-sm">
                <span class="text-muted">{{ t('research.qualityLabel') }}</span>
                <span class="font-medium text-foreground">{{ (brand.quality * 100).toFixed(1) }}%</span>
              </div>
            </div>
          </div>
        </section>
      </template>
    </div>
  </div>
</template>

<style scoped>
.quality-tier-gold {
  background-color: color-mix(in srgb, #f59e0b 20%, transparent);
  color: #fbbf24;
}
.quality-tier-green {
  background-color: color-mix(in srgb, #10b981 20%, transparent);
  color: #34d399;
}
.quality-tier-blue {
  background-color: color-mix(in srgb, var(--color-primary) 15%, transparent);
  color: var(--color-primary);
}
.quality-tier-dim {
  background-color: color-mix(in srgb, var(--color-muted) 15%, transparent);
  color: var(--color-muted);
}
</style>
