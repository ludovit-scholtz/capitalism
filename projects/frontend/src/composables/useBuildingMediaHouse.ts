import { ref, watch, type Ref } from 'vue'
import { gqlRequest } from '@/lib/graphql'
import type { Building, MediaHouseAnalyticsResult, MediaHouseStatsResult } from '@/types'

/**
 * Content budget, upgrade, analytics and unit configuration for MEDIA_HOUSE buildings.
 * Extracted from useBuildingDetail (#596).
 */
export function useBuildingMediaHouse(deps: {
  building: Ref<Building | null>
  t: (key: string) => string
  loadCityMediaHouses: () => Promise<void>
  /** When given, per-building state is reset synchronously when the id changes. */
  buildingId?: () => string | null | undefined
}) {
  const { building, t, loadCityMediaHouses } = deps
  const contentBudgetInput = ref<number | null>(null)
  const savingContentBudget = ref(false)
  const contentBudgetError = ref<string | null>(null)
  const contentBudgetSuccess = ref(false)

  // Media house upgrade (MEDIA_HOUSE)
  const upgradingMediaHouse = ref(false)
  const mediaHouseUpgradeError = ref<string | null>(null)
  const mediaHouseUpgradeSuccess = ref(false)

  // Media house analytics (MEDIA_HOUSE)
  const mediaHouseAnalytics = ref<MediaHouseAnalyticsResult | null>(null)
  const mediaHouseAnalyticsLoading = ref(false)
  const mediaHouseStats = ref<MediaHouseStatsResult | null>(null)
  const mediaHouseStatsLoading = ref(false)
  const mediaHouseUnitId = ref<string | null>(null)
  const mediaHouseTargetCompanyId = ref<string | null>(null)
  const mediaHouseUnitMediaType = ref<'NEWSPAPER' | 'RADIO' | 'TV'>('NEWSPAPER')
  const mediaHouseCampaignBudgetPerTick = ref<number>(0)
  const mediaHouseUnitActive = ref(true)
  const savingMediaHouseUnit = ref(false)
  const mediaHouseUnitError = ref<string | null>(null)
  const mediaHouseUnitSuccess = ref(false)

  function initContentBudgetInput() {
    contentBudgetInput.value = building.value?.contentBudgetPerTick ?? null
    contentBudgetError.value = null
    contentBudgetSuccess.value = false
  }

  async function saveContentBudget() {
    if (!building.value || savingContentBudget.value) return
    savingContentBudget.value = true
    contentBudgetError.value = null
    contentBudgetSuccess.value = false
    try {
      const result = await gqlRequest<{
        setMediaHouseContentBudget: { id: string; contentBudgetPerTick: number | null; contentValue: number }
      }>(
        `mutation SetMediaHouseContentBudget($input: SetMediaHouseContentBudgetInput!) {
          setMediaHouseContentBudget(input: $input) {
            id contentBudgetPerTick contentValue
          }
        }`,
        {
          input: {
            buildingId: building.value.id,
            contentBudgetPerTick: contentBudgetInput.value ?? 0,
          },
        },
      )
      if (building.value) {
        building.value.contentBudgetPerTick = result.setMediaHouseContentBudget.contentBudgetPerTick
        building.value.contentValue = result.setMediaHouseContentBudget.contentValue
      }
      contentBudgetSuccess.value = true
      // Also refresh the city media houses list to reflect updated content budget.
      await loadCityMediaHouses().catch(() => {})
      setTimeout(() => {
        contentBudgetSuccess.value = false
      }, 3000)
    } catch (reason: unknown) {
      contentBudgetError.value = reason instanceof Error ? reason.message : t('mediaHouse.saveFailed')
    } finally {
      savingContentBudget.value = false
    }
  }

  /** Upgrades the current MEDIA_HOUSE building to the next level. */
  async function upgradeMediaHouse() {
    if (!building.value || upgradingMediaHouse.value) return
    upgradingMediaHouse.value = true
    mediaHouseUpgradeError.value = null
    mediaHouseUpgradeSuccess.value = false
    try {
      const result = await gqlRequest<{
        upgradeMediaHouse: { id: string; level: number }
      }>(
        `mutation UpgradeMediaHouse($input: UpgradeMediaHouseInput!) {
          upgradeMediaHouse(input: $input) {
            id level
          }
        }`,
        { input: { buildingId: building.value.id } },
      )
      if (building.value) {
        building.value.level = result.upgradeMediaHouse.level
      }
      mediaHouseUpgradeSuccess.value = true
      // Refresh analytics after upgrade.
      await loadMediaHouseAnalytics()
      setTimeout(() => {
        mediaHouseUpgradeSuccess.value = false
      }, 4000)
    } catch (reason: unknown) {
      mediaHouseUpgradeError.value = reason instanceof Error ? reason.message : t('mediaHouse.upgradeFailed')
    } finally {
      upgradingMediaHouse.value = false
    }
  }

  /** Loads brand-impact analytics for the current MEDIA_HOUSE building. */
  async function loadMediaHouseAnalytics() {
    if (!building.value || building.value.type !== 'MEDIA_HOUSE') return
    mediaHouseAnalyticsLoading.value = true
    try {
      const data = await gqlRequest<{ mediaHouseAnalytics: MediaHouseAnalyticsResult | null }>(
        `query MediaHouseAnalytics($buildingId: UUID!) {
          mediaHouseAnalytics(buildingId: $buildingId) {
            buildingId buildingName mediaType level contentValue contentRankingPct
            channelMultiplier effectiveMultiplier currentEfficiencyPct nextLevelEfficiencyPct
            isMaxLevel upgradeCostEur upgradeTimeTicks maxLevel
            totalIncomeLast100Ticks avgIncomePerTick
            advertiserCount strategyRating strategyTip
            incomeHistory { tick amount description }
            brandEffects {
              companyId companyName brandScope productName
              brandAwareness marketingQuality effectivenessMultiplierApplied
            }
          }
        }`,
        { buildingId: building.value.id },
      )
      mediaHouseAnalytics.value = data.mediaHouseAnalytics ?? null
    } catch {
      mediaHouseAnalytics.value = null
    } finally {
      mediaHouseAnalyticsLoading.value = false
    }
  }

  function initMediaHouseUnitFromStats() {
    const firstUnit = mediaHouseStats.value?.units?.[0]
    if (firstUnit) {
      mediaHouseUnitId.value = firstUnit.id
      mediaHouseTargetCompanyId.value = firstUnit.targetCompanyId
      mediaHouseUnitMediaType.value = (firstUnit.mediaType as 'NEWSPAPER' | 'RADIO' | 'TV') ?? 'NEWSPAPER'
      mediaHouseCampaignBudgetPerTick.value = firstUnit.campaignBudgetPerTick ?? 0
      mediaHouseUnitActive.value = firstUnit.isActive
      return
    }

    mediaHouseUnitId.value = null
    mediaHouseTargetCompanyId.value = building.value?.companyId ?? null
    mediaHouseUnitMediaType.value = (building.value?.mediaType as 'NEWSPAPER' | 'RADIO' | 'TV') ?? 'NEWSPAPER'
    mediaHouseCampaignBudgetPerTick.value = 0
    mediaHouseUnitActive.value = true
  }

  async function loadMediaHouseStats() {
    if (!building.value || building.value.type !== 'MEDIA_HOUSE') return
    mediaHouseStatsLoading.value = true
    try {
      const data = await gqlRequest<{ mediaHouseStats: MediaHouseStatsResult | null }>(
        `query MediaHouseStats($buildingId: UUID!) {
          mediaHouseStats(buildingId: $buildingId) {
            buildingId
            currentBoostDelivered
            campaignCostThisTaxCycle
            estimatedSalesImpact
            boostHistory { tick boost }
            units {
              id
              targetCompanyId
              targetCompanyName
              mediaType
              campaignBudgetPerTick
              brandQualityBoostPerTick
              isActive
              laborCostPerTick
              energyCostPerTick
            }
          }
        }`,
        { buildingId: building.value.id },
      )
      mediaHouseStats.value = data.mediaHouseStats ?? null
      initMediaHouseUnitFromStats()
    } catch {
      mediaHouseStats.value = null
    } finally {
      mediaHouseStatsLoading.value = false
    }
  }

  async function saveMediaHouseUnitConfig() {
    if (!building.value || building.value.type !== 'MEDIA_HOUSE' || savingMediaHouseUnit.value) return
    if (!mediaHouseTargetCompanyId.value) {
      mediaHouseUnitError.value = t('mediaHouse.targetCompanyRequired')
      return
    }
    if (mediaHouseCampaignBudgetPerTick.value < 0) {
      mediaHouseUnitError.value = t('mediaHouse.budgetMustBeNonNegative')
      return
    }

    savingMediaHouseUnit.value = true
    mediaHouseUnitError.value = null
    mediaHouseUnitSuccess.value = false

    try {
      await gqlRequest<{
        configureMediaHouseUnit: { id: string }
      }>(
        `mutation ConfigureMediaHouseUnit($input: ConfigureMediaHouseUnitInput!) {
          configureMediaHouseUnit(input: $input) { id }
        }`,
        {
          input: {
            buildingId: building.value.id,
            unitId: mediaHouseUnitId.value,
            targetCompanyId: mediaHouseTargetCompanyId.value,
            mediaType: mediaHouseUnitMediaType.value,
            campaignBudgetPerTick: mediaHouseCampaignBudgetPerTick.value,
            isActive: mediaHouseUnitActive.value,
          },
        },
      )
      mediaHouseUnitSuccess.value = true
      await loadMediaHouseStats()
      setTimeout(() => {
        mediaHouseUnitSuccess.value = false
      }, 3000)
    } catch (reason: unknown) {
      mediaHouseUnitError.value = reason instanceof Error ? reason.message : t('mediaHouse.saveFailed')
    } finally {
      savingMediaHouseUnit.value = false
    }
  }

  /** Clears everything tied to one media house; runs when the viewed building changes. */
  function resetState() {
    contentBudgetInput.value = null
    savingContentBudget.value = false
    contentBudgetError.value = null
    contentBudgetSuccess.value = false
    upgradingMediaHouse.value = false
    mediaHouseUpgradeError.value = null
    mediaHouseUpgradeSuccess.value = false
    mediaHouseAnalytics.value = null
    mediaHouseAnalyticsLoading.value = false
    mediaHouseStats.value = null
    mediaHouseStatsLoading.value = false
    mediaHouseUnitId.value = null
    mediaHouseTargetCompanyId.value = null
    mediaHouseUnitMediaType.value = 'NEWSPAPER'
    mediaHouseCampaignBudgetPerTick.value = 0
    mediaHouseUnitActive.value = true
    savingMediaHouseUnit.value = false
    mediaHouseUnitError.value = null
    mediaHouseUnitSuccess.value = false
  }

  if (deps.buildingId) watch(deps.buildingId, () => resetState(), { flush: 'sync' })

  return {
    contentBudgetInput,
    savingContentBudget,
    contentBudgetError,
    contentBudgetSuccess,
    upgradingMediaHouse,
    mediaHouseUpgradeError,
    mediaHouseUpgradeSuccess,
    mediaHouseAnalytics,
    mediaHouseAnalyticsLoading,
    mediaHouseStats,
    mediaHouseStatsLoading,
    mediaHouseUnitId,
    mediaHouseTargetCompanyId,
    mediaHouseUnitMediaType,
    mediaHouseCampaignBudgetPerTick,
    mediaHouseUnitActive,
    savingMediaHouseUnit,
    mediaHouseUnitError,
    mediaHouseUnitSuccess,
    initContentBudgetInput,
    saveContentBudget,
    upgradeMediaHouse,
    loadMediaHouseAnalytics,
    loadMediaHouseStats,
    saveMediaHouseUnitConfig,
  }
}
