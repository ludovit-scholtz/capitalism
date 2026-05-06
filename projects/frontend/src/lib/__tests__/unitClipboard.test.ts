import { describe, it, expect } from 'vitest'
import {
  UNIT_CLIPBOARD_SCHEMA_VERSION,
  serializeUnitConfig,
  deserializeUnitConfig,
  type UnitClipboardConfig,
} from '../unitClipboard'

// Minimal unit fixture covering all config fields
const minimalUnit = {
  unitType: 'MANUFACTURING',
  resourceTypeId: null,
  productTypeId: 'product-abc',
  minPrice: 5.5,
  maxPrice: 10.0,
  purchaseSource: null,
  saleVisibility: null,
  budget: null,
  mediaHouseBuildingId: null,
  minQuality: null,
  brandScope: null,
  vendorLockCompanyId: null,
  lockedCityId: null,
  industryCategory: null,
  lowInventoryAlertThreshold: null,
}

describe('serializeUnitConfig', () => {
  it('includes __schema version', () => {
    const json = serializeUnitConfig(minimalUnit)
    const parsed = JSON.parse(json) as UnitClipboardConfig
    expect(parsed.__schema).toBe(UNIT_CLIPBOARD_SCHEMA_VERSION)
  })

  it('includes unitType', () => {
    const json = serializeUnitConfig(minimalUnit)
    const parsed = JSON.parse(json) as UnitClipboardConfig
    expect(parsed.unitType).toBe('MANUFACTURING')
  })

  it('serializes all config fields', () => {
    const unit = {
      unitType: 'PURCHASE',
      resourceTypeId: 'res-1',
      productTypeId: null,
      minPrice: 1.0,
      maxPrice: 99.0,
      purchaseSource: 'EXCHANGE',
      saleVisibility: 'PUBLIC',
      budget: 1000,
      mediaHouseBuildingId: 'building-1',
      minQuality: 0.5,
      brandScope: 'PRODUCT',
      vendorLockCompanyId: 'company-1',
      lockedCityId: 'city-1',
      industryCategory: 'FURNITURE',
      lowInventoryAlertThreshold: 50,
    }
    const json = serializeUnitConfig(unit)
    const parsed = JSON.parse(json) as UnitClipboardConfig
    expect(parsed.resourceTypeId).toBe('res-1')
    expect(parsed.productTypeId).toBeNull()
    expect(parsed.minPrice).toBe(1.0)
    expect(parsed.maxPrice).toBe(99.0)
    expect(parsed.purchaseSource).toBe('EXCHANGE')
    expect(parsed.saleVisibility).toBe('PUBLIC')
    expect(parsed.budget).toBe(1000)
    expect(parsed.mediaHouseBuildingId).toBe('building-1')
    expect(parsed.minQuality).toBe(0.5)
    expect(parsed.brandScope).toBe('PRODUCT')
    expect(parsed.vendorLockCompanyId).toBe('company-1')
    expect(parsed.lockedCityId).toBe('city-1')
    expect(parsed.industryCategory).toBe('FURNITURE')
    expect(parsed.lowInventoryAlertThreshold).toBe(50)
  })

  it('maps undefined optional fields to null', () => {
    const unit = { unitType: 'STORAGE' }
    const json = serializeUnitConfig(unit)
    const parsed = JSON.parse(json) as UnitClipboardConfig
    expect(parsed.resourceTypeId).toBeNull()
    expect(parsed.productTypeId).toBeNull()
    expect(parsed.minPrice).toBeNull()
    expect(parsed.maxPrice).toBeNull()
    expect(parsed.purchaseSource).toBeNull()
    expect(parsed.saleVisibility).toBeNull()
    expect(parsed.budget).toBeNull()
    expect(parsed.mediaHouseBuildingId).toBeNull()
    expect(parsed.minQuality).toBeNull()
    expect(parsed.brandScope).toBeNull()
    expect(parsed.vendorLockCompanyId).toBeNull()
    expect(parsed.lockedCityId).toBeNull()
    expect(parsed.industryCategory).toBeNull()
    expect(parsed.lowInventoryAlertThreshold).toBeNull()
  })

  it('round-trips: serialize → deserialize → serialize yields identical JSON', () => {
    const json1 = serializeUnitConfig(minimalUnit)
    const result = deserializeUnitConfig(json1)
    expect(result.ok).toBe(true)
    if (!result.ok) return
    const json2 = serializeUnitConfig(result.config)
    expect(json2).toBe(json1)
  })

  it('does NOT include position-specific fields', () => {
    const unit = {
      unitType: 'MINING',
      gridX: 2,
      gridY: 3,
      level: 5,
      linkRight: true,
      linkDown: false,
      id: 'unit-id-123',
    }
    const json = serializeUnitConfig(unit)
    const parsed = JSON.parse(json) as Record<string, unknown>
    expect(parsed['gridX']).toBeUndefined()
    expect(parsed['gridY']).toBeUndefined()
    expect(parsed['level']).toBeUndefined()
    expect(parsed['linkRight']).toBeUndefined()
    expect(parsed['linkDown']).toBeUndefined()
    expect(parsed['id']).toBeUndefined()
  })
})

describe('deserializeUnitConfig', () => {
  it('parses a valid unit config JSON successfully', () => {
    const json = serializeUnitConfig(minimalUnit)
    const result = deserializeUnitConfig(json)
    expect(result.ok).toBe(true)
    if (!result.ok) return
    expect(result.config.unitType).toBe('MANUFACTURING')
    expect(result.config.productTypeId).toBe('product-abc')
    expect(result.config.minPrice).toBe(5.5)
  })

  it('returns EMPTY for empty string', () => {
    const result = deserializeUnitConfig('')
    expect(result.ok).toBe(false)
    if (result.ok) return
    expect(result.error).toBe('EMPTY')
  })

  it('returns EMPTY for whitespace-only string', () => {
    const result = deserializeUnitConfig('   ')
    expect(result.ok).toBe(false)
    if (result.ok) return
    expect(result.error).toBe('EMPTY')
  })

  it('returns INVALID_JSON for non-JSON text', () => {
    const result = deserializeUnitConfig('not valid json {{{}')
    expect(result.ok).toBe(false)
    if (result.ok) return
    expect(result.error).toBe('INVALID_JSON')
  })

  it('returns SCHEMA_MISMATCH for JSON without __schema field', () => {
    const result = deserializeUnitConfig('{"unitType":"MINING"}')
    expect(result.ok).toBe(false)
    if (result.ok) return
    expect(result.error).toBe('SCHEMA_MISMATCH')
  })

  it('returns SCHEMA_MISMATCH for JSON with wrong __schema version', () => {
    const result = deserializeUnitConfig('{"__schema":"unit-config-v0","unitType":"MINING"}')
    expect(result.ok).toBe(false)
    if (result.ok) return
    expect(result.error).toBe('SCHEMA_MISMATCH')
  })

  it('returns SCHEMA_MISMATCH for a plain string (not JSON object)', () => {
    const result = deserializeUnitConfig('"just a string"')
    expect(result.ok).toBe(false)
    if (result.ok) return
    expect(result.error).toBe('SCHEMA_MISMATCH')
  })

  it('accepts config when targetUnitType matches', () => {
    const json = serializeUnitConfig({ unitType: 'MINING' })
    const result = deserializeUnitConfig(json, 'MINING')
    expect(result.ok).toBe(true)
  })

  it('returns INCOMPATIBLE_TYPE when unitType does not match target', () => {
    const json = serializeUnitConfig({ unitType: 'MINING' })
    const result = deserializeUnitConfig(json, 'MANUFACTURING')
    expect(result.ok).toBe(false)
    if (result.ok) return
    expect(result.error).toBe('INCOMPATIBLE_TYPE')
  })

  it('accepts config when no targetUnitType is given (no type check)', () => {
    const json = serializeUnitConfig({ unitType: 'PUBLIC_SALES' })
    const result = deserializeUnitConfig(json)
    expect(result.ok).toBe(true)
    if (!result.ok) return
    expect(result.config.unitType).toBe('PUBLIC_SALES')
  })

  it('compatibility matrix: same types succeed', () => {
    const types = ['MINING', 'STORAGE', 'B2B_SALES', 'PURCHASE', 'MANUFACTURING', 'PUBLIC_SALES']
    for (const unitType of types) {
      const json = serializeUnitConfig({ unitType })
      const result = deserializeUnitConfig(json, unitType)
      expect(result.ok).toBe(true)
    }
  })

  it('compatibility matrix: different types fail with INCOMPATIBLE_TYPE', () => {
    const json = serializeUnitConfig({ unitType: 'MINING' })
    const incompatibleTargets = ['MANUFACTURING', 'STORAGE', 'B2B_SALES', 'PURCHASE', 'PUBLIC_SALES']
    for (const target of incompatibleTargets) {
      const result = deserializeUnitConfig(json, target)
      expect(result.ok).toBe(false)
      if (result.ok) continue
      expect(result.error).toBe('INCOMPATIBLE_TYPE')
    }
  })
})
