import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import type { Building } from '@/types'

const gqlRequest = vi.fn()
vi.mock('@/lib/graphql', () => ({ gqlRequest: (...args: unknown[]) => gqlRequest(...args) }))

async function setup(building: Record<string, unknown> = {}) {
  const { useBuildingMediaHouse } = await import('../useBuildingMediaHouse')
  const b = ref({ id: 'b1', type: 'MEDIA_HOUSE', companyId: 'c1', level: 1, ...building } as unknown as Building)
  const loadCityMediaHouses = vi.fn(async () => {})
  const api = useBuildingMediaHouse({ building: b, t: (key: string) => key, loadCityMediaHouses })
  return { api, b, loadCityMediaHouses }
}

describe('useBuildingMediaHouse', () => {
  beforeEach(() => gqlRequest.mockReset())

  it('initContentBudgetInput copies the budget and clears messages', async () => {
    const { api } = await setup({ contentBudgetPerTick: 120 })
    api.contentBudgetError.value = 'x'
    api.initContentBudgetInput()
    expect(api.contentBudgetInput.value).toBe(120)
    expect(api.contentBudgetError.value).toBeNull()
  })

  it('saveContentBudget stores the returned values and refreshes the city media houses', async () => {
    gqlRequest.mockResolvedValue({ setMediaHouseContentBudget: { id: 'b1', contentBudgetPerTick: 50, contentValue: 9 } })
    const { api, b, loadCityMediaHouses } = await setup()
    api.contentBudgetInput.value = 50
    await api.saveContentBudget()
    const rec = b.value as unknown as { contentBudgetPerTick: number; contentValue: number }
    expect(rec.contentBudgetPerTick).toBe(50)
    expect(rec.contentValue).toBe(9)
    expect(api.contentBudgetSuccess.value).toBe(true)
    expect(api.savingContentBudget.value).toBe(false)
    expect(loadCityMediaHouses).toHaveBeenCalledOnce()
    expect(gqlRequest.mock.calls[0][1]).toEqual({ input: { buildingId: 'b1', contentBudgetPerTick: 50 } })
  })

  it('saveContentBudget reports the translated fallback for non-Error failures', async () => {
    gqlRequest.mockRejectedValueOnce('nope')
    const { api } = await setup()
    await api.saveContentBudget()
    expect(api.contentBudgetError.value).toBe('mediaHouse.saveFailed')
  })

  it('upgradeMediaHouse sets the new level and reloads analytics', async () => {
    gqlRequest.mockResolvedValueOnce({ upgradeMediaHouse: { id: 'b1', level: 2 } })
    gqlRequest.mockResolvedValueOnce({ mediaHouseAnalytics: { buildingId: 'b1' } })
    const { api, b } = await setup()
    await api.upgradeMediaHouse()
    expect((b.value as unknown as { level: number }).level).toBe(2)
    expect(api.mediaHouseUpgradeSuccess.value).toBe(true)
    expect(api.mediaHouseAnalytics.value).toMatchObject({ buildingId: 'b1' })
  })

  it('loadMediaHouseAnalytics ignores non media house buildings', async () => {
    const { api } = await setup({ type: 'FACTORY' })
    await api.loadMediaHouseAnalytics()
    expect(gqlRequest).not.toHaveBeenCalled()
  })

  it('loadMediaHouseStats fills the unit form from the first unit', async () => {
    gqlRequest.mockResolvedValue({
      mediaHouseStats: {
        buildingId: 'b1',
        units: [{ id: 'u1', targetCompanyId: 'c9', mediaType: 'TV', campaignBudgetPerTick: 7, isActive: false }],
      },
    })
    const { api } = await setup()
    await api.loadMediaHouseStats()
    expect(api.mediaHouseUnitId.value).toBe('u1')
    expect(api.mediaHouseTargetCompanyId.value).toBe('c9')
    expect(api.mediaHouseUnitMediaType.value).toBe('TV')
    expect(api.mediaHouseCampaignBudgetPerTick.value).toBe(7)
    expect(api.mediaHouseUnitActive.value).toBe(false)
  })

  it('loadMediaHouseStats defaults the form when there are no units, and nulls on failure', async () => {
    gqlRequest.mockResolvedValueOnce({ mediaHouseStats: { buildingId: 'b1', units: [] } })
    const { api } = await setup({ mediaType: 'RADIO' })
    await api.loadMediaHouseStats()
    expect(api.mediaHouseUnitId.value).toBeNull()
    expect(api.mediaHouseTargetCompanyId.value).toBe('c1')
    expect(api.mediaHouseUnitMediaType.value).toBe('RADIO')
    gqlRequest.mockRejectedValueOnce(new Error('x'))
    await api.loadMediaHouseStats()
    expect(api.mediaHouseStats.value).toBeNull()
    expect(api.mediaHouseStatsLoading.value).toBe(false)
  })

  it('saveMediaHouseUnitConfig validates before calling the API', async () => {
    const { api } = await setup()
    await api.saveMediaHouseUnitConfig()
    expect(api.mediaHouseUnitError.value).toBe('mediaHouse.targetCompanyRequired')
    api.mediaHouseTargetCompanyId.value = 'c1'
    api.mediaHouseCampaignBudgetPerTick.value = -1
    await api.saveMediaHouseUnitConfig()
    expect(api.mediaHouseUnitError.value).toBe('mediaHouse.budgetMustBeNonNegative')
    expect(gqlRequest).not.toHaveBeenCalled()
  })

  it('saveMediaHouseUnitConfig sends the unit configuration and reloads stats', async () => {
    gqlRequest.mockResolvedValueOnce({ configureMediaHouseUnit: { id: 'u1' } })
    gqlRequest.mockResolvedValueOnce({ mediaHouseStats: { buildingId: 'b1', units: [] } })
    const { api } = await setup()
    api.mediaHouseTargetCompanyId.value = 'c1'
    api.mediaHouseCampaignBudgetPerTick.value = 5
    await api.saveMediaHouseUnitConfig()
    expect(gqlRequest.mock.calls[0][1]).toEqual({
      input: { buildingId: 'b1', unitId: null, targetCompanyId: 'c1', mediaType: 'NEWSPAPER', campaignBudgetPerTick: 5, isActive: true },
    })
    expect(api.mediaHouseUnitSuccess.value).toBe(true)
    expect(gqlRequest).toHaveBeenCalledTimes(2)
  })
})
