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
  // Bumped by resetState; a mutation started before a reset must not write into the next building.
  let generation = 0
  const STALE = Symbol('stale')

  async function guardedRequest<T>(gen: number, query: string, variables: Record<string, unknown>): Promise<T> {
    try {
      const result = await gqlRequest<T>(query, variables)
      if (gen !== generation) throw STALE
      return result
    } catch (e) {
      if (gen !== generation) throw STALE
      throw e
    }
  }

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
    const gen = generation
    dispatchSaving.value = true
    dispatchError.value = null
    dispatchSuccess.value = false
    try {
      const data = await guardedRequest<{ setPlantDispatch: { id: string; dispatchTargetPercent: number } }>(
        gen,
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
      if (e === STALE) return
      dispatchError.value = e instanceof Error ? e.message : 'Failed to update dispatch target.'
    } finally {
      if (gen === generation) dispatchSaving.value = false
    }
  }

  // ─── Power priority ───────────────────────────────────────────────────────
  const prioritySaving = ref(false)
  const priorityError = ref<string | null>(null)
  const prioritySuccess = ref(false)

  async function setPowerPriority(buildingId: string, priority: number) {
    const gen = generation
    prioritySaving.value = true
    priorityError.value = null
    prioritySuccess.value = false
    try {
      await guardedRequest<{ setPowerPriority: { id: string; powerPriority: number } }>(
        gen,
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
      if (e === STALE) return
      priorityError.value = e instanceof Error ? e.message : 'Failed to update power priority.'
    } finally {
      if (gen === generation) prioritySaving.value = false
    }
  }

  // ─── Energy spot-market listing (power plant buildings) ───────────────────
  const energyListingSaving = ref(false)
  const energyListingError = ref<string | null>(null)
  const energyListingSuccess = ref(false)

  async function listEnergyForSale(buildingId: string, pricePerKwhLocal: number, capacityKw: number) {
    const gen = generation
    energyListingSaving.value = true
    energyListingError.value = null
    energyListingSuccess.value = false
    try {
      await guardedRequest<{ listEnergyForSale: { listingId: string } }>(
        gen,
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
      if (e === STALE) return
      energyListingError.value = e instanceof Error ? e.message : 'Failed to create energy listing.'
    } finally {
      if (gen === generation) energyListingSaving.value = false
    }
  }

  const energyCancelSaving = ref(false)
  const energyCancelError = ref<string | null>(null)

  async function cancelEnergyListing(listingId: string) {
    const gen = generation
    energyCancelSaving.value = true
    energyCancelError.value = null
    const bid = building.value?.id ?? ''
    try {
      await guardedRequest<{ cancelEnergyListing: boolean }>(
        gen,
        `mutation CancelEnergyListing($input: CancelEnergyListingInput!) {
          cancelEnergyListing(input: $input)
        }`,
        { input: { listingId } },
      )
      if (bid) void loadPowerPlantAnalytics(bid, true)
    } catch (e) {
      if (e === STALE) return
      energyCancelError.value = e instanceof Error ? e.message : 'Failed to cancel energy listing.'
    } finally {
      if (gen === generation) energyCancelSaving.value = false
    }
  }

  // ─── Max energy bid price (consumer buildings) ────────────────────────────
  const maxBidSaving = ref(false)
  const maxBidError = ref<string | null>(null)
  const maxBidSuccess = ref(false)

  async function setMaxEnergyBidPrice(buildingId: string, maxBidPricePerKwh: number | null) {
    const gen = generation
    maxBidSaving.value = true
    maxBidError.value = null
    maxBidSuccess.value = false
    try {
      await guardedRequest<{ setMaxEnergyBidPrice: { id: string; maxEnergyBidPrice: number | null } }>(
        gen,
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
      if (e === STALE) return
      maxBidError.value = e instanceof Error ? e.message : 'Failed to update max bid price.'
    } finally {
      if (gen === generation) maxBidSaving.value = false
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

  /**
   * Clears everything tied to one building; call when the route moves to another building.
   * The city power balance is per city (the panel refetches it when the city changes), so it stays.
   */
  function resetState() {
    generation++
    activePowerPlantAnalyticsRequest++
    powerPlantAnalytics.value = null
    powerPlantAnalyticsLoading.value = false
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
