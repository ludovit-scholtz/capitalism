import { describe, expect, it } from 'vitest'
import {
  ADVISOR_MAX_SUGGESTIONS,
  ADVISOR_THRESHOLDS,
  deriveAdvisorSuggestions,
  parseDismissedIds,
  type AdvisorBuilding,
  type AdvisorUnitStatus,
} from '../advisorRules'

function building(id: string, type: string, unitTypes: string[], name = `${type} ${id}`): AdvisorBuilding {
  return { id, name, type, units: unitTypes.map((unitType, index) => ({ id: `${id}-u${index}`, unitType })) }
}

function status(unitId: string, overrides: Partial<AdvisorUnitStatus> = {}): AdvisorUnitStatus {
  return { buildingUnitId: unitId, status: 'BLOCKED', blockedCode: null, idleTicks: 0, ...overrides }
}

const outOfStock = (unitId: string, idleTicks: number) => status(unitId, { blockedCode: 'NO_INVENTORY', idleTicks })

describe('out-of-stock shop rule', () => {
  it('flags a shop whose sales unit has had no stock for the threshold', () => {
    const shop = building('shop-1', 'SALES_SHOP', ['PUBLIC_SALES', 'PURCHASE'])
    const result = deriveAdvisorSuggestions([shop], { 'shop-1': [outOfStock('shop-1-u0', ADVISOR_THRESHOLDS.outOfStockTicks)] })

    expect(result).toEqual([
      expect.objectContaining({ rule: 'OUT_OF_STOCK_SHOP', buildingId: 'shop-1', ticks: ADVISOR_THRESHOLDS.outOfStockTicks, linkPath: '/building/shop-1', hasPurchaseUnit: true }),
    ])
  })

  it('stays quiet below the threshold', () => {
    const shop = building('shop-1', 'SALES_SHOP', ['PUBLIC_SALES'])
    expect(deriveAdvisorSuggestions([shop], { 'shop-1': [outOfStock('shop-1-u0', ADVISOR_THRESHOLDS.outOfStockTicks - 1)] })).toEqual([])
  })

  it('tells the player to add a purchase unit when the shop has none', () => {
    const shop = building('shop-1', 'SALES_SHOP', ['PUBLIC_SALES'])
    const [suggestion] = deriveAdvisorSuggestions([shop], { 'shop-1': [outOfStock('shop-1-u0', 20)] })

    expect(suggestion?.hasPurchaseUnit).toBe(false)
  })

  it('ignores a sales unit that is blocked for another reason', () => {
    const shop = building('shop-1', 'SALES_SHOP', ['PUBLIC_SALES', 'PURCHASE'])
    const noDemand = status('shop-1-u0', { blockedCode: 'NO_DEMAND', idleTicks: 5 })
    expect(deriveAdvisorSuggestions([shop], { 'shop-1': [noDemand] })).toEqual([])
  })

  it('ignores buildings that are not shops', () => {
    const factory = building('f-1', 'FACTORY', ['PUBLIC_SALES'])
    expect(deriveAdvisorSuggestions([factory], { 'f-1': [outOfStock('f-1-u0', 50)] })).toEqual([])
  })
})

describe('other rules', () => {
  it('flags a factory with no inputs', () => {
    const factory = building('f-1', 'FACTORY', ['MANUFACTURING', 'PURCHASE'])
    const noInputs = status('f-1-u0', { blockedCode: 'NO_INPUTS', idleTicks: ADVISOR_THRESHOLDS.noInputsTicks })

    expect(deriveAdvisorSuggestions([factory], { 'f-1': [noInputs] }).map((s) => s.rule)).toEqual(['FACTORY_NO_INPUTS'])
  })

  it('flags a shop with stock but no demand only after a full in-game day', () => {
    const shop = building('shop-1', 'SALES_SHOP', ['PUBLIC_SALES'])
    const waiting = (ticks: number) => ({ 'shop-1': [status('shop-1-u0', { blockedCode: 'NO_DEMAND', idleTicks: ticks })] })

    expect(deriveAdvisorSuggestions([shop], waiting(ADVISOR_THRESHOLDS.noDemandTicks - 1))).toEqual([])
    expect(deriveAdvisorSuggestions([shop], waiting(ADVISOR_THRESHOLDS.noDemandTicks)).map((s) => s.rule)).toEqual(['SHOP_NO_DEMAND'])
  })

  it('flags an unconfigured unit', () => {
    const factory = building('f-1', 'FACTORY', ['MANUFACTURING'])
    const unconfigured = status('f-1-u0', { status: 'UNCONFIGURED', blockedCode: 'UNCONFIGURED', idleTicks: ADVISOR_THRESHOLDS.unconfiguredTicks })

    expect(deriveAdvisorSuggestions([factory], { 'f-1': [unconfigured] }).map((s) => s.rule)).toEqual(['UNCONFIGURED_UNIT'])
  })

  it('ignores healthy units and buildings without statuses', () => {
    const shop = building('shop-1', 'SALES_SHOP', ['PUBLIC_SALES'])
    expect(deriveAdvisorSuggestions([shop], { 'shop-1': [status('shop-1-u0', { status: 'ACTIVE', idleTicks: 99 })] })).toEqual([])
    expect(deriveAdvisorSuggestions([shop], {})).toEqual([])
  })

  it('ignores statuses of units the building does not have', () => {
    const shop = building('shop-1', 'SALES_SHOP', ['PUBLIC_SALES'])
    expect(deriveAdvisorSuggestions([shop], { 'shop-1': [outOfStock('someone-elses-unit', 99)] })).toEqual([])
  })
})

describe('ranking and limits', () => {
  it('puts the out-of-stock shop ahead of other problems', () => {
    const factory = building('f-1', 'FACTORY', ['MANUFACTURING'])
    const shop = building('shop-1', 'SALES_SHOP', ['PUBLIC_SALES'])
    const result = deriveAdvisorSuggestions([factory, shop], {
      'f-1': [status('f-1-u0', { blockedCode: 'NO_INPUTS', idleTicks: 99 })],
      'shop-1': [outOfStock('shop-1-u0', 12)],
    })

    expect(result.map((s) => s.rule)).toEqual(['OUT_OF_STOCK_SHOP', 'FACTORY_NO_INPUTS'])
  })

  it('orders the same rule by how long the problem has lasted', () => {
    const shops = [building('a', 'SALES_SHOP', ['PUBLIC_SALES']), building('b', 'SALES_SHOP', ['PUBLIC_SALES'])]
    const result = deriveAdvisorSuggestions(shops, { a: [outOfStock('a-u0', 13)], b: [outOfStock('b-u0', 40)] })

    expect(result.map((s) => s.buildingId)).toEqual(['b', 'a'])
  })

  it('shows at most the limit, one suggestion per building and rule', () => {
    const shops = ['a', 'b', 'c'].map((id) => building(id, 'SALES_SHOP', ['PUBLIC_SALES', 'PUBLIC_SALES']))
    const statuses = Object.fromEntries(shops.map((s) => [s.id, [outOfStock(`${s.id}-u0`, 20), outOfStock(`${s.id}-u1`, 30)]]))
    const result = deriveAdvisorSuggestions(shops, statuses)

    expect(result).toHaveLength(ADVISOR_MAX_SUGGESTIONS)
    expect(new Set(result.map((s) => s.id)).size).toBe(result.length)
    expect(result[0]?.ticks).toBe(30)
  })

  it('skips dismissed suggestions and lets the next problem take the slot', () => {
    const shops = ['a', 'b', 'c'].map((id) => building(id, 'SALES_SHOP', ['PUBLIC_SALES']))
    const statuses = { a: [outOfStock('a-u0', 30)], b: [outOfStock('b-u0', 20)], c: [outOfStock('c-u0', 13)] }

    const result = deriveAdvisorSuggestions(shops, statuses, ['OUT_OF_STOCK_SHOP:a'])

    expect(result.map((s) => s.buildingId)).toEqual(['b', 'c'])
  })

  it('uses a stable id per rule and building so a dismissal sticks', () => {
    const shop = building('shop-1', 'SALES_SHOP', ['PUBLIC_SALES'])
    const [first] = deriveAdvisorSuggestions([shop], { 'shop-1': [outOfStock('shop-1-u0', 12)] })
    const [later] = deriveAdvisorSuggestions([shop], { 'shop-1': [outOfStock('shop-1-u0', 30)] })

    expect(first?.id).toBe('OUT_OF_STOCK_SHOP:shop-1')
    expect(later?.id).toBe(first?.id)
  })
})

describe('parseDismissedIds', () => {
  it('reads a stored list of ids', () => {
    expect(parseDismissedIds('["OUT_OF_STOCK_SHOP:a","FACTORY_NO_INPUTS:b"]')).toEqual(['OUT_OF_STOCK_SHOP:a', 'FACTORY_NO_INPUTS:b'])
  })

  it('treats missing, malformed or wrongly shaped storage as nothing dismissed', () => {
    expect(parseDismissedIds(null)).toEqual([])
    expect(parseDismissedIds('not json')).toEqual([])
    expect(parseDismissedIds('{"a":1}')).toEqual([])
    expect(parseDismissedIds('[1,"ok",null]')).toEqual(['ok'])
  })
})
