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

  it('upgradeMediaHouse ignores a second call while an upgrade is running', async () => {
    let resolveUpgrade!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveUpgrade = r)))
    gqlRequest.mockResolvedValue({ mediaHouseAnalytics: null })
    const { api } = await setup()
    const first = api.upgradeMediaHouse()
    await api.upgradeMediaHouse()
    expect(gqlRequest).toHaveBeenCalledTimes(1)
    resolveUpgrade({ upgradeMediaHouse: { id: 'b1', level: 2 } })
    await first
  })

  it('saveContentBudget still reports success when only the city media house refresh fails', async () => {
    gqlRequest.mockResolvedValue({ setMediaHouseContentBudget: { id: 'b1', contentBudgetPerTick: 5, contentValue: 1 } })
    const { useBuildingMediaHouse } = await import('../useBuildingMediaHouse')
    const b = ref({ id: 'b1', type: 'MEDIA_HOUSE' } as unknown as Building)
    const api = useBuildingMediaHouse({
      building: b,
      t: (key: string) => key,
      loadCityMediaHouses: async () => {
        throw new Error('refresh failed')
      },
    })
    await api.saveContentBudget()
    expect(api.contentBudgetSuccess.value).toBe(true)
    expect(api.contentBudgetError.value).toBeNull()
  })

  it('clears messages and the unit form synchronously when the watched building id changes', async () => {
    const { useBuildingMediaHouse } = await import('../useBuildingMediaHouse')
    const buildingId = ref('b1')
    const b = ref({ id: 'b1', type: 'MEDIA_HOUSE', companyId: 'c1' } as unknown as Building)
    const api = useBuildingMediaHouse({
      building: b,
      t: (key: string) => key,
      loadCityMediaHouses: async () => {},
      buildingId: () => buildingId.value,
    })
    api.mediaHouseUnitId.value = 'u-of-b1'
    api.contentBudgetSuccess.value = true
    api.mediaHouseUnitError.value = 'x'
    buildingId.value = 'b2'
    expect(api.mediaHouseUnitId.value).toBeNull()
    expect(api.contentBudgetSuccess.value).toBe(false)
    expect(api.mediaHouseUnitError.value).toBeNull()
    expect(api.mediaHouseStats.value).toBeNull()
  })

  async function setupScoped() {
    const { useBuildingMediaHouse } = await import('../useBuildingMediaHouse')
    const buildingId = ref('b1')
    const b = ref({ id: 'b1', type: 'MEDIA_HOUSE', companyId: 'c1', level: 1 } as unknown as Building)
    const api = useBuildingMediaHouse({
      building: b,
      t: (key: string) => key,
      loadCityMediaHouses: async () => {},
      buildingId: () => buildingId.value,
    })
    return { api, b, buildingId }
  }

  it('a slow upgrade for the previous building does not touch the new one', async () => {
    let resolveUpgrade!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveUpgrade = r)))
    const { api, b, buildingId } = await setupScoped()
    const pending = api.upgradeMediaHouse()
    buildingId.value = 'b2'
    b.value = { id: 'b2', type: 'MEDIA_HOUSE', level: 4 } as unknown as Building
    resolveUpgrade({ upgradeMediaHouse: { id: 'b1', level: 2 } })
    await pending
    expect((b.value as unknown as { level: number }).level).toBe(4)
    expect(api.mediaHouseUpgradeSuccess.value).toBe(false)
    expect(gqlRequest).toHaveBeenCalledTimes(1)
  })

  it('slow stats and analytics for the previous building are ignored', async () => {
    let resolveStats!: (v: unknown) => void
    let resolveAnalytics!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveStats = r)))
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveAnalytics = r)))
    const { api, buildingId } = await setupScoped()
    const stats = api.loadMediaHouseStats()
    const analytics = api.loadMediaHouseAnalytics()
    buildingId.value = 'b2'
    resolveStats({ mediaHouseStats: { buildingId: 'b1', units: [{ id: 'old', targetCompanyId: 'c9', isActive: true }] } })
    resolveAnalytics({ mediaHouseAnalytics: { buildingId: 'b1' } })
    await Promise.all([stats, analytics])
    expect(api.mediaHouseStats.value).toBeNull()
    expect(api.mediaHouseAnalytics.value).toBeNull()
    expect(api.mediaHouseUnitId.value).toBeNull()
  })

  it('a slow content budget save for the previous building does not write into the new one', async () => {
    let resolveSave!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveSave = r)))
    const { api, b, buildingId } = await setupScoped()
    const pending = api.saveContentBudget()
    buildingId.value = 'b2'
    b.value = { id: 'b2', type: 'MEDIA_HOUSE', contentBudgetPerTick: 1 } as unknown as Building
    resolveSave({ setMediaHouseContentBudget: { id: 'b1', contentBudgetPerTick: 99, contentValue: 99 } })
    await pending
    expect((b.value as unknown as { contentBudgetPerTick: number }).contentBudgetPerTick).toBe(1)
    expect(api.contentBudgetSuccess.value).toBe(false)
  })

  it('a slow unit configuration save for the previous building shows no banner and reloads nothing', async () => {
    let resolveSave!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveSave = r)))
    const { api, buildingId } = await setupScoped()
    api.mediaHouseTargetCompanyId.value = 'c1'
    const pending = api.saveMediaHouseUnitConfig()
    buildingId.value = 'b2'
    resolveSave({ configureMediaHouseUnit: { id: 'u1' } })
    await pending
    expect(api.mediaHouseUnitSuccess.value).toBe(false)
    expect(gqlRequest).toHaveBeenCalledTimes(1)
  })
})
