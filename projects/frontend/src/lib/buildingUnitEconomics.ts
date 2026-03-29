import type { BuildingUnit, ProductType, ResourceType } from '@/types'

export type UnitIdentity = Pick<BuildingUnit, 'unitType' | 'gridX' | 'gridY'>

const unitConstructionCosts: Record<string, number> = {
  MINING: 9000,
  STORAGE: 3500,
  B2B_SALES: 5000,
  PURCHASE: 4500,
  MANUFACTURING: 12000,
  BRANDING: 7000,
  MARKETING: 5500,
  PUBLIC_SALES: 6000,
  PRODUCT_QUALITY: 8500,
  BRAND_QUALITY: 8500,
}

export function getUnitConstructionCost(unitType: string): number {
  return unitConstructionCosts[unitType] ?? 0
}

export function getPlannedUnitConstructionCost(
  activeUnit: UnitIdentity | null | undefined,
  plannedUnit: UnitIdentity | null | undefined,
): number {
  if (!plannedUnit) return 0
  if (!activeUnit) return getUnitConstructionCost(plannedUnit.unitType)
  if (activeUnit.unitType !== plannedUnit.unitType) return getUnitConstructionCost(plannedUnit.unitType)
  return 0
}

export function sumPlannedConfigurationCost(
  activeUnits: UnitIdentity[],
  plannedUnits: UnitIdentity[],
): number {
  const activeByPosition = new Map(activeUnits.map((unit) => [`${unit.gridX},${unit.gridY}`, unit]))

  return plannedUnits.reduce((total, unit) => {
    const activeUnit = activeByPosition.get(`${unit.gridX},${unit.gridY}`)
    return total + getPlannedUnitConstructionCost(activeUnit, unit)
  }, 0)
}

export function getConfiguredItemBasePrice(
  resourceTypeId: string | null | undefined,
  productTypeId: string | null | undefined,
  resourceTypes: ResourceType[],
  productTypes: ProductType[],
): number | null {
  if (productTypeId) {
    return productTypes.find((product) => product.id === productTypeId)?.basePrice ?? null
  }

  if (resourceTypeId) {
    return resourceTypes.find((resource) => resource.id === resourceTypeId)?.basePrice ?? null
  }

  return null
}

export function getInventoryEstimatedValue(
  quantity: number | null | undefined,
  resourceTypeId: string | null | undefined,
  productTypeId: string | null | undefined,
  resourceTypes: ResourceType[],
  productTypes: ProductType[],
): number | null {
  if (quantity == null || quantity <= 0) return null

  const basePrice = getConfiguredItemBasePrice(
    resourceTypeId,
    productTypeId,
    resourceTypes,
    productTypes,
  )

  if (basePrice == null) return null
  return Number((quantity * basePrice).toFixed(2))
}