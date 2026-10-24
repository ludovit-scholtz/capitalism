import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import type { Building } from '@/types'

const gqlRequest = vi.fn()
vi.mock('@/lib/graphql', () => ({ gqlRequest: (...args: unknown[]) => gqlRequest(...args) }))

async function setup(token: string | null = 'jwt') {
  const { useBuildingPowerPlant } = await import('../useBuildingPowerPlant')
  const building = ref({ id: 'b1', powerPriority: 1 } as unknown as Building)
  const api = useBuildingPowerPlant({ building, getToken: () => token })
  return { building, api }
}

describe('useBuildingPowerPlant', () => {
  beforeEach(() => gqlRequest.mockReset())

  it('clears analytics without a token and does not call the API', async () => {
    const { api } = await setup(null)
    await api.loadPowerPlantAnalytics('b1')
    expect(api.powerPlantAnalytics.value).toBeNull()
    expect(gqlRequest).not.toHaveBeenCalled()
  })

  it('loads analytics and toggles the loading flag', async () => {
    gqlRequest.mockResolvedValue({ powerPlantAnalytics: { buildingId: 'b1' } })
    const { api } = await setup()
    await api.loadPowerPlantAnalytics('b1')
    expect(api.powerPlantAnalytics.value).toMatchObject({ buildingId: 'b1' })
    expect(api.powerPlantAnalyticsLoading.value).toBe(false)
  })

  it('keeps the stale analytics when a refresh fails', async () => {
    gqlRequest.mockResolvedValueOnce({ powerPlantAnalytics: { buildingId: 'b1' } })
    const { api } = await setup()
    await api.loadPowerPlantAnalytics('b1')
    gqlRequest.mockRejectedValueOnce(new Error('x'))
    await api.loadPowerPlantAnalytics('b1', true)
    expect(api.powerPlantAnalytics.value).not.toBeNull()
  })

  it('only the latest overlapping analytics request wins', async () => {
    let resolveFirst!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveFirst = r)))
    gqlRequest.mockResolvedValueOnce({ powerPlantAnalytics: { buildingId: 'second' } })
    const { api } = await setup()
    const first = api.loadPowerPlantAnalytics('b1')
    await api.loadPowerPlantAnalytics('b2')
    resolveFirst({ powerPlantAnalytics: { buildingId: 'first' } })
    await first
    expect(api.powerPlantAnalytics.value).toMatchObject({ buildingId: 'second' })
  })

  it('setPlantDispatch updates the building and reloads analytics', async () => {
    gqlRequest.mockResolvedValueOnce({ setPlantDispatch: { id: 'b1', dispatchTargetPercent: 40 } })
    gqlRequest.mockResolvedValueOnce({ powerPlantAnalytics: { buildingId: 'b1' } })
    const { api, building } = await setup()
    await api.setPlantDispatch('b1', 40)
    expect(api.dispatchSuccess.value).toBe(true)
    expect((building.value as unknown as { dispatchTargetPercent: number }).dispatchTargetPercent).toBe(40)
    expect(gqlRequest).toHaveBeenCalledTimes(2)
  })

  it('setPlantDispatch reports the error message', async () => {
    gqlRequest.mockRejectedValueOnce(new Error('nope'))
    const { api } = await setup()
    await api.setPlantDispatch('b1', 40)
    expect(api.dispatchError.value).toBe('nope')
    expect(api.dispatchSaving.value).toBe(false)
  })

  it('setPowerPriority and setMaxEnergyBidPrice update the local building', async () => {
    gqlRequest.mockResolvedValue({})
    const { api, building } = await setup()
    await api.setPowerPriority('b1', 3)
    await api.setMaxEnergyBidPrice('b1', 0.2)
    const b = building.value as unknown as { powerPriority: number; maxEnergyBidPrice: number }
    expect(b.powerPriority).toBe(3)
    expect(b.maxEnergyBidPrice).toBe(0.2)
    expect(api.prioritySuccess.value).toBe(true)
    expect(api.maxBidSuccess.value).toBe(true)
  })

  it('cancelEnergyListing reloads analytics for the current building', async () => {
    gqlRequest.mockResolvedValue({ cancelEnergyListing: true, powerPlantAnalytics: { buildingId: 'b1' } })
    const { api } = await setup()
    await api.cancelEnergyListing('l1')
    expect(gqlRequest).toHaveBeenCalledTimes(2)
    expect(api.energyCancelError.value).toBeNull()
  })

  it('loadCityPowerBalance ignores an empty city and nulls on first-load failure', async () => {
    const { api } = await setup()
    await api.loadCityPowerBalance('')
    expect(gqlRequest).not.toHaveBeenCalled()
    gqlRequest.mockRejectedValueOnce(new Error('x'))
    await api.loadCityPowerBalance('c1')
    expect(api.cityPowerBalance.value).toBeNull()
    expect(api.cityPowerBalanceLoading.value).toBe(false)
  })

  it('only the latest overlapping city power balance request wins', async () => {
    let resolveFirst!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveFirst = r)))
    gqlRequest.mockResolvedValueOnce({ cityPowerBalance: { cityId: 'second' } })
    const { api } = await setup()
    const first = api.loadCityPowerBalance('c1')
    await api.loadCityPowerBalance('c2')
    resolveFirst({ cityPowerBalance: { cityId: 'first' } })
    await first
    expect(api.cityPowerBalance.value).toMatchObject({ cityId: 'second' })
    expect(api.cityPowerBalanceLoading.value).toBe(false)
  })

  it('resetState clears per-building flags and results', async () => {
    for (let i = 0; i < 5; i++) gqlRequest.mockRejectedValueOnce(new Error('boom'))
    const { api } = await setup()
    await api.setPlantDispatch('b1', 10)
    await api.setPowerPriority('b1', 2)
    await api.listEnergyForSale('b1', 1, 1)
    await api.setMaxEnergyBidPrice('b1', 1)
    await api.cancelEnergyListing('l1')
    api.resetState()
    for (const f of [api.dispatchError, api.priorityError, api.energyListingError, api.maxBidError, api.energyCancelError]) {
      expect(f.value).toBeNull()
    }
    expect(api.powerPlantAnalytics.value).toBeNull()
    expect(api.cityPowerBalance.value).toBeNull()
  })

  it('resetState invalidates in-flight requests', async () => {
    let resolveFirst!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveFirst = r)))
    const { api } = await setup()
    const pending = api.loadPowerPlantAnalytics('b1')
    api.resetState()
    resolveFirst({ powerPlantAnalytics: { buildingId: 'old' } })
    await pending
    expect(api.powerPlantAnalytics.value).toBeNull()
  })

  it('resetState keeps the per-city power balance (the panel only refetches when the city changes)', async () => {
    gqlRequest.mockResolvedValueOnce({ cityPowerBalance: { cityId: 'c1' } })
    const { api } = await setup()
    await api.loadCityPowerBalance('c1')
    api.resetState()
    expect(api.cityPowerBalance.value).toMatchObject({ cityId: 'c1' })
  })

  it('a mutation that finishes after resetState does not touch the new building or its flags', async () => {
    let resolveMutation!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveMutation = r)))
    const { api, building } = await setup()
    const pending = api.setPlantDispatch('b1', 40)
    expect(api.dispatchSaving.value).toBe(true)
    api.resetState()
    expect(api.dispatchSaving.value).toBe(false)
    api.dispatchSaving.value = true // a new mutation started on the next building
    resolveMutation({ setPlantDispatch: { id: 'b1', dispatchTargetPercent: 40 } })
    await pending
    expect(api.dispatchSuccess.value).toBe(false)
    expect(api.dispatchSaving.value).toBe(true)
    expect((building.value as unknown as { dispatchTargetPercent?: number }).dispatchTargetPercent).toBeUndefined()
    expect(gqlRequest).toHaveBeenCalledTimes(1)
  })

  it('a failing mutation that finishes after resetState does not set an error on the new building', async () => {
    let rejectMutation!: (e: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((_, rej) => (rejectMutation = rej)))
    const { api } = await setup()
    const pending = api.setPowerPriority('b1', 3)
    api.resetState()
    rejectMutation(new Error('late'))
    await pending
    expect(api.priorityError.value).toBeNull()
  })

  it('a successful mutation still sets the success flag when no reset happened', async () => {
    gqlRequest.mockResolvedValue({})
    const { api } = await setup()
    await api.setMaxEnergyBidPrice('b1', 2)
    expect(api.maxBidSuccess.value).toBe(true)
    expect(api.maxBidSaving.value).toBe(false)
  })

  it('resets itself synchronously when the watched building id changes', async () => {
    const { useBuildingPowerPlant } = await import('../useBuildingPowerPlant')
    const buildingId = ref('b1')
    const building = ref({ id: 'b1' } as unknown as Building)
    gqlRequest.mockRejectedValueOnce(new Error('boom'))
    const api = useBuildingPowerPlant({ building, getToken: () => 'jwt', buildingId: () => buildingId.value })
    await api.setPlantDispatch('b1', 10)
    expect(api.dispatchError.value).toBe('boom')
    buildingId.value = 'b2'
    expect(api.dispatchError.value).toBeNull()
  })

  it('a token-less load invalidates an in-flight analytics request', async () => {
    const { useBuildingPowerPlant } = await import('../useBuildingPowerPlant')
    let token: string | null = 'jwt'
    let resolveFirst!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveFirst = r)))
    const api = useBuildingPowerPlant({ building: ref(null), getToken: () => token })
    const pending = api.loadPowerPlantAnalytics('b1')
    token = null
    await api.loadPowerPlantAnalytics('b1')
    resolveFirst({ powerPlantAnalytics: { buildingId: 'old' } })
    await pending
    expect(api.powerPlantAnalytics.value).toBeNull()
  })
})
