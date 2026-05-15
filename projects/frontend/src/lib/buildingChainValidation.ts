export interface ChainValidationUnit {
  id: string
  unitType: string
  gridX: number
  gridY: number
  linkUp: boolean
  linkDown: boolean
  linkLeft: boolean
  linkRight: boolean
  linkUpLeft: boolean
  linkUpRight: boolean
  linkDownLeft: boolean
  linkDownRight: boolean
  resourceTypeId: string | null
  productTypeId: string | null
}

export interface ProductionChainDisplayUnits {
  purchase: ChainValidationUnit | null
  manufacturing: ChainValidationUnit | null
  storage: ChainValidationUnit | null
}

export interface ProductionChainStatus {
  isPurchaseConfigured: boolean
  isManufacturingConfigured: boolean
  isStoragePresent: boolean
  isChainComplete: boolean
}

export function getLinkedUnits(unit: ChainValidationUnit, units: readonly ChainValidationUnit[]): ChainValidationUnit[] {
  const linked: ChainValidationUnit[] = []
  const byPos = new Map(units.map((candidate) => [`${candidate.gridX},${candidate.gridY}`, candidate]))

  if (unit.linkUp) {
    const candidate = byPos.get(`${unit.gridX},${unit.gridY - 1}`)
    if (candidate) linked.push(candidate)
  }
  if (unit.linkDown) {
    const candidate = byPos.get(`${unit.gridX},${unit.gridY + 1}`)
    if (candidate) linked.push(candidate)
  }
  if (unit.linkLeft) {
    const candidate = byPos.get(`${unit.gridX - 1},${unit.gridY}`)
    if (candidate) linked.push(candidate)
  }
  if (unit.linkRight) {
    const candidate = byPos.get(`${unit.gridX + 1},${unit.gridY}`)
    if (candidate) linked.push(candidate)
  }
  if (unit.linkUpLeft) {
    const candidate = byPos.get(`${unit.gridX - 1},${unit.gridY - 1}`)
    if (candidate) linked.push(candidate)
  }
  if (unit.linkUpRight) {
    const candidate = byPos.get(`${unit.gridX + 1},${unit.gridY - 1}`)
    if (candidate) linked.push(candidate)
  }
  if (unit.linkDownLeft) {
    const candidate = byPos.get(`${unit.gridX - 1},${unit.gridY + 1}`)
    if (candidate) linked.push(candidate)
  }
  if (unit.linkDownRight) {
    const candidate = byPos.get(`${unit.gridX + 1},${unit.gridY + 1}`)
    if (candidate) linked.push(candidate)
  }

  return linked
}

interface OutputPathOptions {
  passthroughUnitTypes?: readonly string[]
  terminalUnitTypes: readonly string[]
}

export function hasReachableOutputPath(sourceUnit: ChainValidationUnit, units: readonly ChainValidationUnit[], options: OutputPathOptions): boolean {
  const passthroughUnitTypes = new Set(options.passthroughUnitTypes ?? [])
  const terminalUnitTypes = new Set(options.terminalUnitTypes)
  const queue: ChainValidationUnit[] = [sourceUnit]
  const visited = new Set<string>()
  let readIndex = 0

  while (readIndex < queue.length) {
    const current = queue[readIndex]
    readIndex += 1
    if (!current || visited.has(current.id)) continue
    visited.add(current.id)

    for (const next of getLinkedUnits(current, units)) {
      if (terminalUnitTypes.has(next.unitType)) {
        return true
      }

      if (!visited.has(next.id) && passthroughUnitTypes.has(next.unitType)) {
        queue.push(next)
      }
    }
  }

  return false
}

export function getProductionChainDisplayUnits(units: readonly ChainValidationUnit[]): ProductionChainDisplayUnits {
  return {
    purchase: units.find((unit) => unit.unitType === 'PURCHASE') ?? null,
    manufacturing: units.find((unit) => unit.unitType === 'MANUFACTURING') ?? null,
    storage: units.find((unit) => unit.unitType === 'STORAGE') ?? null,
  }
}

export function getProductionChainStatus(units: readonly ChainValidationUnit[]): ProductionChainStatus {
  const { purchase, manufacturing, storage } = getProductionChainDisplayUnits(units)
  const isPurchaseConfigured = !!(purchase && (purchase.resourceTypeId || purchase.productTypeId))
  const isManufacturingConfigured = !!(manufacturing && manufacturing.productTypeId)
  const isStoragePresent = !!storage

  return {
    isPurchaseConfigured,
    isManufacturingConfigured,
    isStoragePresent,
    isChainComplete: isPurchaseConfigured && isManufacturingConfigured,
  }
}
