import { ref, type Ref } from 'vue'
import { gqlRequest } from '@/lib/graphql'
import { createBuildingScope } from '@/composables/buildingScope'
import type { Building } from '@/types'

/** Rent dialog for APARTMENT / COMMERCIAL buildings. Extracted from useBuildingDetail (#596). */
export function useBuildingRent(deps: {
  building: Ref<Building | null>
  t: (key: string) => string
  /** When given, the dialog state is reset synchronously when the id changes. */
  buildingId?: () => string | null | undefined
}) {
  const { building, t } = deps
  // Property management (APARTMENT / COMMERCIAL)
  const showRentDialog = ref(false)
  const newRentPerSqm = ref<number | null>(null)
  const savingRent = ref(false)
  const rentSaveError = ref<string | null>(null)
  const scope = createBuildingScope(deps.buildingId, () => {
    showRentDialog.value = false
    newRentPerSqm.value = null
    savingRent.value = false
    rentSaveError.value = null
  })

  function openRentDialog() {
    newRentPerSqm.value = building.value?.pendingPricePerSqm ?? building.value?.pricePerSqm ?? null
    rentSaveError.value = null
    showRentDialog.value = true
  }

  function closeRentDialog() {
    showRentDialog.value = false
    rentSaveError.value = null
  }

  async function saveRentPerSqm() {
    if (!building.value || !scope.matches(building.value.id) || savingRent.value || newRentPerSqm.value === null) return
    const gen = scope.token()
    savingRent.value = true
    rentSaveError.value = null
    try {
      const result = await scope.settle(
        gen,
        gqlRequest<{
          setRentPerSqm: { id: string; pricePerSqm: number | null; pendingPricePerSqm: number | null; pendingPriceActivationTick: number | null }
        }>(
          `mutation SetRentPerSqm($input: SetRentPerSqmInput!) {
          setRentPerSqm(input: $input) {
            id pricePerSqm pendingPricePerSqm pendingPriceActivationTick
          }
        }`,
          {
            input: {
              buildingId: building.value.id,
              rentPerSqm: newRentPerSqm.value,
            },
          },
        ),
      )
      // Update local building state with returned values.
      if (building.value) {
        building.value.pendingPricePerSqm = result.setRentPerSqm.pendingPricePerSqm
        building.value.pendingPriceActivationTick = result.setRentPerSqm.pendingPriceActivationTick
      }
      showRentDialog.value = false
    } catch (reason: unknown) {
      if (scope.isStaleError(reason)) return
      rentSaveError.value = reason instanceof Error ? reason.message : t('property.saveFailed')
    } finally {
      if (!scope.isStale(gen)) savingRent.value = false
    }
  }

  return {
    showRentDialog,
    newRentPerSqm,
    savingRent,
    rentSaveError,
    openRentDialog,
    closeRentDialog,
    saveRentPerSqm,
  }
}
