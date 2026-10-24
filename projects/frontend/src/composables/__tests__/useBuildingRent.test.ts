import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import type { Building } from '@/types'

const gqlRequest = vi.fn()
vi.mock('@/lib/graphql', () => ({ gqlRequest: (...args: unknown[]) => gqlRequest(...args) }))

async function setup(building: Record<string, unknown> = {}) {
  const { useBuildingRent } = await import('../useBuildingRent')
  const b = ref({ id: 'b1', pricePerSqm: 10, ...building } as unknown as Building)
  const api = useBuildingRent({ building: b, t: (key: string) => key })
  return { api, b }
}

describe('useBuildingRent', () => {
  beforeEach(() => gqlRequest.mockReset())

  it('openRentDialog prefers the pending rent over the current one', async () => {
    const { api } = await setup({ pendingPricePerSqm: 12 })
    api.openRentDialog()
    expect(api.showRentDialog.value).toBe(true)
    expect(api.newRentPerSqm.value).toBe(12)
  })

  it('openRentDialog falls back to the current rent', async () => {
    const { api } = await setup()
    api.openRentDialog()
    expect(api.newRentPerSqm.value).toBe(10)
  })

  it('closeRentDialog hides the dialog and clears the error', async () => {
    const { api } = await setup()
    api.openRentDialog()
    api.rentSaveError.value = 'x'
    api.closeRentDialog()
    expect(api.showRentDialog.value).toBe(false)
    expect(api.rentSaveError.value).toBeNull()
  })

  it('saveRentPerSqm does nothing without a value', async () => {
    const { api } = await setup()
    api.newRentPerSqm.value = null
    await api.saveRentPerSqm()
    expect(gqlRequest).not.toHaveBeenCalled()
  })

  it('saveRentPerSqm stores the pending rent and closes the dialog', async () => {
    gqlRequest.mockResolvedValue({
      setRentPerSqm: { id: 'b1', pricePerSqm: 10, pendingPricePerSqm: 15, pendingPriceActivationTick: 99 },
    })
    const { api, b } = await setup()
    api.openRentDialog()
    api.newRentPerSqm.value = 15
    await api.saveRentPerSqm()
    const rec = b.value as unknown as { pendingPricePerSqm: number; pendingPriceActivationTick: number }
    expect(rec.pendingPricePerSqm).toBe(15)
    expect(rec.pendingPriceActivationTick).toBe(99)
    expect(api.showRentDialog.value).toBe(false)
    expect(api.savingRent.value).toBe(false)
    expect(gqlRequest.mock.calls[0][1]).toEqual({ input: { buildingId: 'b1', rentPerSqm: 15 } })
  })

  it('saveRentPerSqm keeps the dialog open and shows the error', async () => {
    gqlRequest.mockRejectedValueOnce(new Error('too high'))
    const { api } = await setup()
    api.openRentDialog()
    await api.saveRentPerSqm()
    expect(api.rentSaveError.value).toBe('too high')
    expect(api.showRentDialog.value).toBe(true)
  })

  it('saveRentPerSqm uses the translated fallback for non-Error failures', async () => {
    gqlRequest.mockRejectedValueOnce('x')
    const { api } = await setup()
    api.openRentDialog()
    await api.saveRentPerSqm()
    expect(api.rentSaveError.value).toBe('property.saveFailed')
  })

  it('closes the dialog and clears the draft synchronously when the watched building id changes', async () => {
    const { useBuildingRent } = await import('../useBuildingRent')
    const buildingId = ref('b1')
    const b = ref({ id: 'b1', pricePerSqm: 10 } as unknown as Building)
    const api = useBuildingRent({ building: b, t: (key: string) => key, buildingId: () => buildingId.value })
    api.openRentDialog()
    api.rentSaveError.value = 'x'
    buildingId.value = 'b2'
    expect(api.showRentDialog.value).toBe(false)
    expect(api.newRentPerSqm.value).toBeNull()
    expect(api.rentSaveError.value).toBeNull()
  })

  it('a slow rent save for the previous building does not write into the new one', async () => {
    const { useBuildingRent } = await import('../useBuildingRent')
    const buildingId = ref('b1')
    const b = ref({ id: 'b1', pricePerSqm: 10 } as unknown as Building)
    const api = useBuildingRent({ building: b, t: (key: string) => key, buildingId: () => buildingId.value })
    let resolveSave!: (v: unknown) => void
    gqlRequest.mockImplementationOnce(() => new Promise((r) => (resolveSave = r)))
    api.openRentDialog()
    api.newRentPerSqm.value = 15
    const pending = api.saveRentPerSqm()
    buildingId.value = 'b2'
    b.value = { id: 'b2', pricePerSqm: 20 } as unknown as Building
    resolveSave({ setRentPerSqm: { id: 'b1', pricePerSqm: 10, pendingPricePerSqm: 15, pendingPriceActivationTick: 7 } })
    await pending
    expect((b.value as unknown as { pendingPricePerSqm?: number }).pendingPricePerSqm).toBeUndefined()
    expect(api.showRentDialog.value).toBe(false)
  })
})
