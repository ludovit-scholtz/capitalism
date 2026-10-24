import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import type { Building } from '@/types'
import type { EditableGridUnit } from '../useBuildingDetail'

const api = vi.hoisted(() => ({
  isMasterConnected: vi.fn(() => false),
  fetchMasterLayouts: vi.fn(),
  saveMasterLayout: vi.fn(),
  deleteMasterLayout: vi.fn(),
  saveLocalLayout: vi.fn(),
  deleteLocalLayout: vi.fn(),
  getLocalLayoutsForType: vi.fn(() => []),
}))
vi.mock('@/lib/masterLayoutApi', () => api)

function unit(overrides: Partial<EditableGridUnit> = {}): EditableGridUnit {
  return {
    id: 'u1',
    unitType: 'STORAGE',
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
    productTypeId: null,
    minPrice: null,
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

async function setup(units: EditableGridUnit[] = [unit()]) {
  const { useBuildingLayouts } = await import('../useBuildingLayouts')
  const building = ref({ id: 'b1', type: 'FACTORY' } as unknown as Building)
  const buildingId = ref('b1')
  const draftUnits = ref<EditableGridUnit[]>(units)
  const layouts = useBuildingLayouts({
    building,
    draftUnits,
    t: (key: string) => key,
    buildingId: () => buildingId.value,
  })
  return { layouts, building, buildingId, draftUnits }
}

describe('useBuildingLayouts', () => {
  beforeEach(() => {
    Object.values(api).forEach((fn) => fn.mockReset())
    api.isMasterConnected.mockReturnValue(false)
    api.getLocalLayoutsForType.mockReturnValue([])
  })

  it('getDraftLayoutUnits serialises only the layout fields', async () => {
    const { layouts } = await setup([unit({ gridX: 2, unitType: 'MANUFACTURING' })])
    const [serialised] = layouts.getDraftLayoutUnits()
    expect(serialised).toMatchObject({ unitType: 'MANUFACTURING', gridX: 2 })
    expect(serialised).not.toHaveProperty('id')
    expect(serialised).not.toHaveProperty('level')
  })

  it('refreshLocalLayouts reads the layouts of the building type', async () => {
    api.getLocalLayoutsForType.mockReturnValue([{ name: 'a' }])
    const { layouts } = await setup()
    layouts.refreshLocalLayouts()
    expect(api.getLocalLayoutsForType).toHaveBeenCalledWith('FACTORY')
    expect(layouts.localLayouts.value).toEqual([{ name: 'a' }])
  })

  it('refreshMasterLayouts is a no-op that clears the list when not connected', async () => {
    const { layouts } = await setup()
    layouts.masterLayouts.value = [{ name: 'old' } as never]
    await layouts.refreshMasterLayouts()
    expect(layouts.masterLayouts.value).toEqual([])
    expect(api.fetchMasterLayouts).not.toHaveBeenCalled()
  })

  it('refreshMasterLayouts keeps only layouts of the building type and records errors', async () => {
    api.isMasterConnected.mockReturnValue(true)
    api.fetchMasterLayouts.mockResolvedValueOnce([
      { id: '1', buildingType: 'FACTORY' },
      { id: '2', buildingType: 'SHOP' },
    ])
    const { layouts } = await setup()
    await layouts.refreshMasterLayouts()
    expect(layouts.masterLayouts.value.map((l) => l.id)).toEqual(['1'])
    api.fetchMasterLayouts.mockRejectedValueOnce(new Error('offline'))
    await layouts.refreshMasterLayouts()
    expect(layouts.masterLayoutsError.value).toBe('offline')
    expect(layouts.masterLayoutsLoading.value).toBe(false)
  })

  it('saveLayout needs a name and units', async () => {
    const { layouts, draftUnits } = await setup()
    await layouts.saveLayout()
    expect(api.saveLocalLayout).not.toHaveBeenCalled()
    layouts.layoutName.value = 'My layout'
    draftUnits.value = []
    await layouts.saveLayout()
    expect(layouts.layoutSaveError.value).toBe('buildingDetail.layouts.noUnits')
  })

  it('saveLayout stores locally when not connected and clears the form', async () => {
    const { layouts } = await setup()
    layouts.layoutName.value = '  My layout '
    layouts.layoutDescription.value = ' '
    await layouts.saveLayout()
    expect(api.saveLocalLayout).toHaveBeenCalledWith('My layout', null, 'FACTORY', expect.any(Array))
    expect(layouts.layoutName.value).toBe('')
    expect(layouts.layoutSaveSuccess.value).toBe(true)
    expect(layouts.layoutSaving.value).toBe(false)
  })

  it('saveLayout updates an existing cloud layout with the same name', async () => {
    api.isMasterConnected.mockReturnValue(true)
    api.saveMasterLayout.mockResolvedValueOnce({ id: 'cloud-1', name: 'L', buildingType: 'FACTORY', units: [] })
    const { layouts } = await setup()
    layouts.masterLayouts.value = [{ id: 'cloud-1', name: 'L', buildingType: 'FACTORY', units: [] } as never]
    layouts.layoutName.value = 'L'
    await layouts.saveLayout()
    expect(api.saveMasterLayout).toHaveBeenCalledWith('L', null, 'FACTORY', expect.any(Array), 'cloud-1')
    expect(layouts.masterLayouts.value).toHaveLength(1)
  })

  it('saveLayout falls back to local storage with a warning when the cloud save fails', async () => {
    api.isMasterConnected.mockReturnValue(true)
    api.saveMasterLayout.mockRejectedValueOnce(new Error('down'))
    const { layouts } = await setup()
    layouts.layoutName.value = 'L'
    await layouts.saveLayout()
    expect(api.saveLocalLayout).toHaveBeenCalledOnce()
    expect(layouts.layoutSaveError.value).toBe('buildingDetail.layouts.masterError')
    expect(layouts.layoutSaveSuccess.value).toBe(true)
  })

  it('requestLoadLayout rejects a layout for another building type', async () => {
    const { layouts, draftUnits } = await setup()
    layouts.requestLoadLayout({ name: 'x', buildingType: 'SHOP', units: [] } as never)
    expect(layouts.layoutSaveError.value).toBe('buildingDetail.layouts.incompatible')
    expect(draftUnits.value).toHaveLength(1)
  })

  it('requestLoadLayout asks for confirmation when the draft is not empty, then applies on confirm', async () => {
    const layout = { name: 'x', buildingType: 'FACTORY', units: [{ ...unit({ unitType: 'SALES', gridX: 3 }) }] } as never
    const { layouts, draftUnits } = await setup()
    layouts.requestLoadLayout(layout)
    expect(layouts.overwriteConfirmPending.value).toEqual(layout)
    layouts.cancelOverwrite()
    expect(layouts.overwriteConfirmPending.value).toBeNull()
    layouts.requestLoadLayout(layout)
    layouts.confirmOverwrite()
    expect(draftUnits.value).toHaveLength(1)
    expect(draftUnits.value[0]).toMatchObject({ unitType: 'SALES', gridX: 3, level: 1 })
    expect(layouts.overwriteConfirmPending.value).toBeNull()
  })

  it('requestLoadLayout applies immediately when the draft is empty', async () => {
    const layout = { name: 'x', buildingType: 'FACTORY', units: [unit({ gridX: 1 })] } as never
    const { layouts, draftUnits } = await setup([])
    layouts.requestLoadLayout(layout)
    expect(draftUnits.value).toHaveLength(1)
  })

  it('deleteLayout removes cloud layouts remotely and local ones from storage', async () => {
    const { layouts } = await setup()
    layouts.masterLayouts.value = [{ id: 'c1' } as never, { id: 'c2' } as never]
    await layouts.deleteLayout({ id: 'c1', name: 'a', buildingType: 'FACTORY', isLocal: false } as never)
    expect(api.deleteMasterLayout).toHaveBeenCalledWith('c1')
    expect(layouts.masterLayouts.value.map((l) => l.id)).toEqual(['c2'])
    await layouts.deleteLayout({ name: 'b', buildingType: 'FACTORY', isLocal: true } as never)
    expect(api.deleteLocalLayout).toHaveBeenCalledWith('b', 'FACTORY')
  })

  it('deleteLayout records an error', async () => {
    api.deleteMasterLayout.mockRejectedValueOnce(new Error('nope'))
    const { layouts } = await setup()
    await layouts.deleteLayout({ id: 'c1', name: 'a', buildingType: 'FACTORY', isLocal: false } as never)
    expect(layouts.layoutDeleteError.value).toBe('nope')
  })

  it('resets messages, pending confirmation and lists when the building changes', async () => {
    const { layouts, buildingId } = await setup()
    layouts.layoutSaveError.value = 'x'
    layouts.layoutSaveSuccess.value = true
    layouts.layoutDeleteError.value = 'y'
    layouts.overwriteConfirmPending.value = { name: 'p', buildingType: 'FACTORY', units: [] } as never
    layouts.masterLayouts.value = [{ id: '1' } as never]
    layouts.localLayouts.value = [{ name: 'l' } as never]
    buildingId.value = 'b2'
    expect(layouts.layoutSaveError.value).toBeNull()
    expect(layouts.layoutSaveSuccess.value).toBe(false)
    expect(layouts.layoutDeleteError.value).toBeNull()
    expect(layouts.overwriteConfirmPending.value).toBeNull()
    expect(layouts.masterLayouts.value).toEqual([])
    expect(layouts.localLayouts.value).toEqual([])
  })

  it('a slow master layout fetch for the previous building is ignored', async () => {
    api.isMasterConnected.mockReturnValue(true)
    let resolveFetch!: (v: unknown) => void
    api.fetchMasterLayouts.mockImplementationOnce(() => new Promise((r) => (resolveFetch = r)))
    const { layouts, buildingId } = await setup()
    const pending = layouts.refreshMasterLayouts()
    buildingId.value = 'b2'
    resolveFetch([{ id: '1', buildingType: 'FACTORY' }])
    await pending
    expect(layouts.masterLayouts.value).toEqual([])
  })

  it('the success banner clears itself after 3 seconds, but not for a different building', async () => {
    vi.useFakeTimers()
    try {
      const { layouts, buildingId } = await setup()
      layouts.layoutName.value = 'L'
      await layouts.saveLayout()
      expect(layouts.layoutSaveSuccess.value).toBe(true)
      vi.advanceTimersByTime(3000)
      expect(layouts.layoutSaveSuccess.value).toBe(false)
      layouts.layoutName.value = 'L2'
      await layouts.saveLayout()
      buildingId.value = 'b2'
      layouts.layoutSaveSuccess.value = true // a new banner on the next building
      vi.advanceTimersByTime(3000)
      expect(layouts.layoutSaveSuccess.value).toBe(true)
    } finally {
      vi.useRealTimers()
    }
  })

  it('reports the original cloud error when the local fallback also fails', async () => {
    api.isMasterConnected.mockReturnValue(true)
    api.saveMasterLayout.mockRejectedValueOnce(new Error('cloud down'))
    api.saveLocalLayout.mockImplementationOnce(() => {
      throw new Error('disk full')
    })
    const { layouts } = await setup()
    layouts.layoutName.value = 'L'
    await layouts.saveLayout()
    expect(layouts.layoutSaveError.value).toBe('cloud down')
    expect(layouts.layoutSaveSuccess.value).toBe(false)
  })
})
