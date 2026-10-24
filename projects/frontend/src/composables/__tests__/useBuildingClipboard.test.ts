import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import type { EditableGridUnit } from '../useBuildingDetail'
import { useBuildingClipboard } from '../useBuildingClipboard'

function unit(overrides: Partial<EditableGridUnit> = {}): EditableGridUnit {
  return {
    id: 'u1',
    unitType: 'PUBLIC_SALES',
    gridX: 0,
    gridY: 0,
    level: 1,
    linkUp: false,
    linkDown: false,
    linkLeft: false,
    linkRight: false,
    linkUpLeft: false,
    linkUpRight: false,
    linkDownLeft: false,
    linkDownRight: false,
    resourceTypeId: null,
    productTypeId: 'p1',
    minPrice: 5,
    maxPrice: null,
    purchaseSource: null,
    saleVisibility: null,
    budget: null,
    mediaHouseBuildingId: null,
    minQuality: null,
    brandScope: null,
    vendorLockCompanyId: null,
    lockedCityId: null,
    industryCategory: null,
    ...overrides,
  }
}

function setup(options: { units?: EditableGridUnit[]; editing?: boolean; allowed?: string[]; selected?: { x: number; y: number } | null } = {}) {
  const units = options.units ?? [unit()]
  const placeUnit = vi.fn((type: string) => {
    units.push(unit({ id: 'new', unitType: type, productTypeId: null, minPrice: null, gridX: 1, gridY: 0 }))
  })
  const api = useBuildingClipboard({
    isEditing: ref(options.editing ?? true),
    selectedCell: ref(options.selected === undefined ? { x: 0, y: 0 } : options.selected),
    getDraftUnitAt: (x, y) => units.find((u) => u.gridX === x && u.gridY === y) ?? null,
    allowedUnits: ref(options.allowed ?? ['PUBLIC_SALES']),
    placeUnit,
    t: (key: string) => key,
  })
  return { api, units, placeUnit }
}

function stubClipboard(read: () => Promise<string>, write = vi.fn(async () => {})) {
  vi.stubGlobal('navigator', { clipboard: { readText: read, writeText: write } })
  return write
}

describe('useBuildingClipboard', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('does nothing outside edit mode or without a selection', async () => {
    const write = stubClipboard(async () => '')
    await setup({ editing: false }).api.copySelectedUnit()
    await setup({ selected: null }).api.copySelectedUnit()
    expect(write).not.toHaveBeenCalled()
  })

  it('reports when there is no unit to copy', async () => {
    stubClipboard(async () => '')
    const { api } = setup({ units: [] })
    await api.copySelectedUnit()
    expect(api.clipboardMessage.value).toBe('buildingDetail.clipboard.noUnitToCopy')
    expect(api.clipboardMessageType.value).toBe('error')
  })

  it('copies the unit configuration as JSON and shows a success message that expires', async () => {
    const write = stubClipboard(async () => '')
    const { api } = setup()
    await api.copySelectedUnit()
    expect(write).toHaveBeenCalledOnce()
    expect(JSON.parse((write.mock.calls[0] as unknown as [string])[0])).toMatchObject({ unitType: 'PUBLIC_SALES', productTypeId: 'p1' })
    expect(api.clipboardMessage.value).toBe('buildingDetail.clipboard.copySuccess')
    vi.advanceTimersByTime(3000)
    expect(api.clipboardMessage.value).toBeNull()
  })

  it('reports a failed clipboard write', async () => {
    stubClipboard(
      async () => '',
      vi.fn(async () => Promise.reject(new Error('denied'))),
    )
    const { api } = setup()
    await api.copySelectedUnit()
    expect(api.clipboardMessage.value).toBe('buildingDetail.clipboard.copyFailed')
  })

  it('a newer message replaces the older one and restarts the timer', async () => {
    stubClipboard(async () => '')
    const { api } = setup()
    await api.copySelectedUnit()
    vi.advanceTimersByTime(2000)
    await api.copySelectedUnit()
    vi.advanceTimersByTime(2000)
    expect(api.clipboardMessage.value).toBe('buildingDetail.clipboard.copySuccess')
    vi.advanceTimersByTime(1000)
    expect(api.clipboardMessage.value).toBeNull()
  })

  it('reports an unreadable clipboard', async () => {
    stubClipboard(async () => Promise.reject(new Error('denied')))
    const { api } = setup()
    await api.pasteToSelectedUnit()
    expect(api.clipboardMessage.value).toBe('buildingDetail.clipboard.readFailed')
  })

  it('pastes the copied configuration onto a unit of the same type', async () => {
    const source = unit({ productTypeId: 'copied', minPrice: 42 })
    const { serializeUnitConfig } = await import('@/lib/unitClipboard')
    stubClipboard(async () => serializeUnitConfig(source))
    const { api, units } = setup({ units: [unit({ productTypeId: 'old', minPrice: 1 })] })
    await api.pasteToSelectedUnit()
    expect(units[0]).toMatchObject({ productTypeId: 'copied', minPrice: 42 })
    expect(api.clipboardMessage.value).toBe('buildingDetail.clipboard.pasteSuccess')
  })

  it('rejects empty, invalid and incompatible clipboard content', async () => {
    const { serializeUnitConfig } = await import('@/lib/unitClipboard')
    for (const [text, key] of [
      ['', 'buildingDetail.clipboard.empty'],
      ['not json', 'buildingDetail.clipboard.invalidFormat'],
      ['{"x":1}', 'buildingDetail.clipboard.schemaMismatch'],
      [serializeUnitConfig(unit({ unitType: 'MANUFACTURING' })), 'buildingDetail.clipboard.incompatibleType'],
    ] as const) {
      stubClipboard(async () => text)
      const { api, units } = setup()
      await api.pasteToSelectedUnit()
      expect(api.clipboardMessage.value).toBe(key)
      expect(units[0].productTypeId).toBe('p1')
    }
  })

  it('pasting onto an empty slot places a new unit of the copied type when it is allowed', async () => {
    const { serializeUnitConfig } = await import('@/lib/unitClipboard')
    stubClipboard(async () => serializeUnitConfig(unit({ productTypeId: 'copied', minPrice: 9 })))
    const { api, units, placeUnit } = setup({ units: [unit({ gridX: 5, gridY: 5 })], selected: { x: 1, y: 0 } })
    await api.pasteToSelectedUnit()
    expect(placeUnit).toHaveBeenCalledWith('PUBLIC_SALES')
    expect(units.find((u) => u.id === 'new')).toMatchObject({ productTypeId: 'copied', minPrice: 9 })
    expect(api.clipboardMessage.value).toBe('buildingDetail.clipboard.pasteEmptySuccess')
  })

  it('pasting onto an empty slot refuses a unit type that is not allowed in the building', async () => {
    const { serializeUnitConfig } = await import('@/lib/unitClipboard')
    stubClipboard(async () => serializeUnitConfig(unit()))
    const { api, placeUnit } = setup({ units: [], allowed: ['STORAGE'] })
    await api.pasteToSelectedUnit()
    expect(placeUnit).not.toHaveBeenCalled()
    expect(api.clipboardMessage.value).toBe('buildingDetail.clipboard.unitTypeNotAllowed')
  })

  it('pasting invalid content onto an empty slot reports the format error', async () => {
    stubClipboard(async () => 'nope')
    const { api } = setup({ units: [] })
    await api.pasteToSelectedUnit()
    expect(api.clipboardMessage.value).toBe('buildingDetail.clipboard.invalidFormat')
  })
})
