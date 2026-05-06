/**
 * Unit configuration clipboard serialization and deserialization.
 *
 * Schema version v1 captures all user-configurable properties of a grid unit
 * without position-specific data (grid coordinates, links, level).
 * Future schema changes must increment the version for graceful migration.
 */

export const UNIT_CLIPBOARD_SCHEMA_VERSION = 'unit-config-v1' as const

/**
 * The JSON schema for a copied unit configuration.
 * Only configurable (non-structural) fields are included.
 * Position-specific fields (gridX, gridY, level, links) are excluded.
 */
export type UnitClipboardConfig = {
  __schema: typeof UNIT_CLIPBOARD_SCHEMA_VERSION
  unitType: string
  resourceTypeId: string | null
  productTypeId: string | null
  minPrice: number | null
  maxPrice: number | null
  purchaseSource: string | null
  saleVisibility: string | null
  budget: number | null
  mediaHouseBuildingId: string | null
  minQuality: number | null
  brandScope: string | null
  vendorLockCompanyId: string | null
  lockedCityId: string | null
  industryCategory: string | null
  lowInventoryAlertThreshold: number | null
}

/** Error codes returned when clipboard text cannot be parsed as a unit config. */
export type ClipboardParseError = 'EMPTY' | 'INVALID_JSON' | 'SCHEMA_MISMATCH' | 'INCOMPATIBLE_TYPE'

export type ClipboardParseResult =
  | { ok: true; config: UnitClipboardConfig }
  | { ok: false; error: ClipboardParseError }

/**
 * Serialize a unit's configurable properties into JSON for clipboard storage.
 *
 * Position-specific fields (gridX, gridY, level, links) are intentionally
 * excluded so the config can be applied to any unit of the same type.
 */
export function serializeUnitConfig(unit: {
  unitType: string
  resourceTypeId?: string | null
  productTypeId?: string | null
  minPrice?: number | null
  maxPrice?: number | null
  purchaseSource?: string | null
  saleVisibility?: string | null
  budget?: number | null
  mediaHouseBuildingId?: string | null
  minQuality?: number | null
  brandScope?: string | null
  vendorLockCompanyId?: string | null
  lockedCityId?: string | null
  industryCategory?: string | null
  lowInventoryAlertThreshold?: number | null
}): string {
  const config: UnitClipboardConfig = {
    __schema: UNIT_CLIPBOARD_SCHEMA_VERSION,
    unitType: unit.unitType,
    resourceTypeId: unit.resourceTypeId ?? null,
    productTypeId: unit.productTypeId ?? null,
    minPrice: unit.minPrice ?? null,
    maxPrice: unit.maxPrice ?? null,
    purchaseSource: unit.purchaseSource ?? null,
    saleVisibility: unit.saleVisibility ?? null,
    budget: unit.budget ?? null,
    mediaHouseBuildingId: unit.mediaHouseBuildingId ?? null,
    minQuality: unit.minQuality ?? null,
    brandScope: unit.brandScope ?? null,
    vendorLockCompanyId: unit.vendorLockCompanyId ?? null,
    lockedCityId: unit.lockedCityId ?? null,
    industryCategory: unit.industryCategory ?? null,
    lowInventoryAlertThreshold: unit.lowInventoryAlertThreshold ?? null,
  }
  return JSON.stringify(config)
}

/**
 * Parse and validate clipboard text as a unit configuration.
 *
 * @param text - Raw clipboard text to parse.
 * @param targetUnitType - When provided, the parsed config's unitType must match
 *   (otherwise INCOMPATIBLE_TYPE is returned).
 * @returns A parse result with the config on success, or a typed error code.
 */
export function deserializeUnitConfig(text: string, targetUnitType?: string): ClipboardParseResult {
  if (!text || !text.trim()) {
    return { ok: false, error: 'EMPTY' }
  }

  let parsed: unknown
  try {
    parsed = JSON.parse(text)
  } catch {
    return { ok: false, error: 'INVALID_JSON' }
  }

  if (
    typeof parsed !== 'object' ||
    parsed === null ||
    (parsed as Record<string, unknown>)['__schema'] !== UNIT_CLIPBOARD_SCHEMA_VERSION
  ) {
    return { ok: false, error: 'SCHEMA_MISMATCH' }
  }

  const config = parsed as UnitClipboardConfig

  if (targetUnitType !== undefined && config.unitType !== targetUnitType) {
    return { ok: false, error: 'INCOMPATIBLE_TYPE' }
  }

  return { ok: true, config }
}
