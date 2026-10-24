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
})
