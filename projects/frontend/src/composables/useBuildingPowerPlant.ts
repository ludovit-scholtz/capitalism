import { ref, type Ref } from 'vue'
import { gqlRequest } from '@/lib/graphql'
import type { Building, CityPowerBalance, PowerPlantAnalytics } from '@/types'

/**
 * Power plant analytics, dispatch/priority controls, energy spot-market listings and the
 * city power balance for the building detail page. Extracted from useBuildingDetail (#596);
 * it only needs the shared building ref and the current auth token.
 */
export function useBuildingPowerPlant(deps: { building: Ref<Building | null>; getToken: () => string | null | undefined }) {
  const { building, getToken } = deps
  const powerPlantAnalytics = ref<PowerPlantAnalytics | null>(null)
  const powerPlantAnalyticsLoading = ref(false)
  const cityPowerBalance = ref<CityPowerBalance | null>(null)
  const cityPowerBalanceLoading = ref(false)
  let activePowerPlantAnalyticsRequest = 0
  let activeCityPowerBalanceRequest = 0

  async function loadPowerPlantAnalytics(buildingId: string, isRefresh = false) {
    if (!getToken()) {
      powerPlantAnalytics.value = null
      powerPlantAnalyticsLoading.value = false
      return
    }

    const requestId = ++activePowerPlantAnalyticsRequest
    if (!isRefresh || powerPlantAnalytics.value == null) {
      powerPlantAnalyticsLoading.value = true
    }

    try {
      const data = await gqlRequest<{ powerPlantAnalytics: PowerPlantAnalytics }>(
        `query PowerPlantAnalytics($buildingId: UUID!, $limit: Int) {
          powerPlantAnalytics(buildingId: $buildingId, limit: $limit) {
            buildingId
            buildingName
            plantType
            currentOutputMw
            dispatchTargetPercent
            fuelReserveMwh
            maxFuelReserveMwh
            fuelReservePercent
            fuelPurchaseCapacityMwhPerTick
            energyProducingCapacityMw
            fuelConstrainedOutputMw
            fuelTypeLabel
            fuelCostPerMwhEur
            dataFromTick
            dataToTick
            totalSurplusIncome
            totalGridFines
            totalOperatingCosts
            totalFuelCosts
            totalSpotMarketRevenue
            totalNetProfit
            activeListing {
              listingId
              buildingId
              buildingName
              companyId
              companyName
              cityId
              plantType
              pricePerKwhLocal
              capacityKw
              availableKw
              createdAtTick
              createdAtUtc
            }
            timeline {
              tick
              surplusIncome
              gridFine
              operatingCosts
              fuelCosts
              spotMarketRevenue
              netProfit
            }
          }
        }`,
        { buildingId, limit: 100 },
      )
      if (requestId !== activePowerPlantAnalyticsRequest) return
      powerPlantAnalytics.value = data.powerPlantAnalytics
    } catch {
      if (requestId !== activePowerPlantAnalyticsRequest) return
      if (!isRefresh) powerPlantAnalytics.value = null
    } finally {
      if (requestId === activePowerPlantAnalyticsRequest) {
        powerPlantAnalyticsLoading.value = false
      }
    }
  }

  const dispatchSaving = ref(false)
  const dispatchError = ref<string | null>(null)
  const dispatchSuccess = ref(false)

  async function setPlantDispatch(buildingId: string, dispatchTargetPercent: number) {
    dispatchSaving.value = true
    dispatchError.value = null
    dispatchSuccess.value = false
    try {
      const data = await gqlRequest<{ setPlantDispatch: { id: string; dispatchTargetPercent: number } }>(
        `mutation SetPlantDispatch($input: SetPlantDispatchInput!) {
          setPlantDispatch(input: $input) {
            id
            dispatchTargetPercent
          }
        }`,
        { input: { buildingId, dispatchTargetPercent } },
      )
      // Update the local building state immediately.
      if (building.value && data.setPlantDispatch) {
        building.value = { ...building.value, dispatchTargetPercent: data.setPlantDispatch.dispatchTargetPercent }
      }
      dispatchSuccess.value = true
      // Reload analytics to reflect the new dispatch target.
      void loadPowerPlantAnalytics(buildingId, true)
    } catch (e) {
      dispatchError.value = e instanceof Error ? e.message : 'Failed to update dispatch target.'
    } finally {
      dispatchSaving.value = false
    }
  }

  // ─── Power priority ───────────────────────────────────────────────────────
  const prioritySaving = ref(false)
  const priorityError = ref<string | null>(null)
  const prioritySuccess = ref(false)

  async function setPowerPriority(buildingId: string, priority: number) {
    prioritySaving.value = true
    priorityError.value = null
    prioritySuccess.value = false
    try {
      await gqlRequest<{ setPowerPriority: { id: string; powerPriority: number } }>(
        `mutation SetPowerPriority($input: SetPowerPriorityInput!) {
          setPowerPriority(input: $input) {
            id
            powerPriority
          }
        }`,
        { input: { buildingId, priority } },
      )
      if (building.value) {
        building.value = { ...building.value, powerPriority: priority }
      }
      prioritySuccess.value = true
    } catch (e) {
      priorityError.value = e instanceof Error ? e.message : 'Failed to update power priority.'
    } finally {
      prioritySaving.value = false
    }
  }

  // ─── Energy spot-market listing (power plant buildings) ───────────────────
  const energyListingSaving = ref(false)
  const energyListingError = ref<string | null>(null)
  const energyListingSuccess = ref(false)

  async function listEnergyForSale(buildingId: string, pricePerKwhLocal: number, capacityKw: number) {
    energyListingSaving.value = true
    energyListingError.value = null
    energyListingSuccess.value = false
    try {
      await gqlRequest<{ listEnergyForSale: { listingId: string } }>(
        `mutation ListEnergyForSale($input: ListEnergyForSaleInput!) {
          listEnergyForSale(input: $input) {
            listingId
          }
        }`,
        { input: { buildingId, pricePerKwhLocal, capacityKw } },
      )
      energyListingSuccess.value = true
      void loadPowerPlantAnalytics(buildingId, true)
    } catch (e) {
      energyListingError.value = e instanceof Error ? e.message : 'Failed to create energy listing.'
    } finally {
      energyListingSaving.value = false
    }
  }

  const energyCancelSaving = ref(false)
  const energyCancelError = ref<string | null>(null)

  async function cancelEnergyListing(listingId: string) {
    energyCancelSaving.value = true
    energyCancelError.value = null
    const bid = building.value?.id ?? ''
    try {
      await gqlRequest<{ cancelEnergyListing: boolean }>(
        `mutation CancelEnergyListing($input: CancelEnergyListingInput!) {
          cancelEnergyListing(input: $input)
        }`,
        { input: { listingId } },
      )
      if (bid) void loadPowerPlantAnalytics(bid, true)
    } catch (e) {
      energyCancelError.value = e instanceof Error ? e.message : 'Failed to cancel energy listing.'
    } finally {
      energyCancelSaving.value = false
    }
  }

  // ─── Max energy bid price (consumer buildings) ────────────────────────────
  const maxBidSaving = ref(false)
  const maxBidError = ref<string | null>(null)
  const maxBidSuccess = ref(false)

  async function setMaxEnergyBidPrice(buildingId: string, maxBidPricePerKwh: number | null) {
    maxBidSaving.value = true
    maxBidError.value = null
    maxBidSuccess.value = false
    try {
      await gqlRequest<{ setMaxEnergyBidPrice: { id: string; maxEnergyBidPrice: number | null } }>(
        `mutation SetMaxEnergyBidPrice($input: SetMaxEnergyBidPriceInput!) {
          setMaxEnergyBidPrice(input: $input) {
            id
            maxEnergyBidPrice
          }
        }`,
        { input: { buildingId, maxBidPricePerKwh } },
      )
      if (building.value) {
        building.value = { ...building.value, maxEnergyBidPrice: maxBidPricePerKwh }
      }
      maxBidSuccess.value = true
    } catch (e) {
      maxBidError.value = e instanceof Error ? e.message : 'Failed to update max bid price.'
    } finally {
      maxBidSaving.value = false
    }
  }

  async function loadCityPowerBalance(cityId: string, isRefresh = false) {
    if (!cityId) return
    const requestId = ++activeCityPowerBalanceRequest
    if (!isRefresh || cityPowerBalance.value == null) {
      cityPowerBalanceLoading.value = true
    }
    try {
      const data = await gqlRequest<{ cityPowerBalance: CityPowerBalance }>(
        `query CityPowerBalance($cityId: UUID!) {
          cityPowerBalance(cityId: $cityId) {
            cityId
            totalSupplyMw
            totalDemandMw
            reserveMw
            reservePercent
            status
            powerPlantCount
            consumerBuildingCount
          }
        }`,
        { cityId },
      )
      if (requestId !== activeCityPowerBalanceRequest) return
      cityPowerBalance.value = data.cityPowerBalance
    } catch {
      if (requestId !== activeCityPowerBalanceRequest) return
      if (!isRefresh) cityPowerBalance.value = null
    } finally {
      if (requestId === activeCityPowerBalanceRequest) cityPowerBalanceLoading.value = false
    }
  }

  /** Clears everything tied to one building; call when the route moves to another building. */
  function resetState() {
    activePowerPlantAnalyticsRequest++
    activeCityPowerBalanceRequest++
    powerPlantAnalytics.value = null
    powerPlantAnalyticsLoading.value = false
    cityPowerBalance.value = null
    cityPowerBalanceLoading.value = false
    dispatchSaving.value = false
    dispatchError.value = null
    dispatchSuccess.value = false
    prioritySaving.value = false
    priorityError.value = null
    prioritySuccess.value = false
    energyListingSaving.value = false
    energyListingError.value = null
    energyListingSuccess.value = false
    energyCancelSaving.value = false
    energyCancelError.value = null
    maxBidSaving.value = false
    maxBidError.value = null
    maxBidSuccess.value = false
  }

  return {
    resetState,
    powerPlantAnalytics,
    powerPlantAnalyticsLoading,
    cityPowerBalance,
    cityPowerBalanceLoading,
    loadPowerPlantAnalytics,
    loadCityPowerBalance,
    setPlantDispatch,
    dispatchSaving,
    dispatchError,
    dispatchSuccess,
    setPowerPriority,
    prioritySaving,
    priorityError,
    prioritySuccess,
    listEnergyForSale,
    energyListingSaving,
    energyListingError,
    energyListingSuccess,
    cancelEnergyListing,
    energyCancelSaving,
    energyCancelError,
    setMaxEnergyBidPrice,
    maxBidSaving,
    maxBidError,
    maxBidSuccess,
  }
}
