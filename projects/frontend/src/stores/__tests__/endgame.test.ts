import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useEndgameStore, ENDGAME_MILESTONES } from '@/stores/endgame'

const gqlRequestMock = vi.fn()

vi.mock('@/lib/graphql', () => ({
  gqlRequest: (...args: unknown[]) => gqlRequestMock(...args),
}))

const mockStatus = {
  gameEnded: false,
  winnerPlayerId: null,
  winnerDisplayName: null,
  winnerCompanyName: null,
  gameEndedAtUtc: null,
  winningThresholdUsd: 1_000_000,
  topRealWorldRichest: [],
}

describe('useEndgameStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    gqlRequestMock.mockReset()
  })

  it('fetchStatus populates the store with server data', async () => {
    gqlRequestMock.mockResolvedValue({ endgameStatus: mockStatus })

    const store = useEndgameStore()
    await store.fetchStatus()

    expect(store.status).toEqual(mockStatus)
    expect(store.isGameEnded).toBe(false)
    expect(store.winningThresholdUsd).toBe(1_000_000)
    expect(store.loading).toBe(false)
    expect(store.error).toBe(null)
  })

  it('fetchStatus sets error when the request fails', async () => {
    gqlRequestMock.mockRejectedValue(new Error('Network error'))

    const store = useEndgameStore()
    await store.fetchStatus()

    expect(store.status).toBe(null)
    expect(store.error).toBe('Network error')
  })

  it('progressPercent returns 0 when threshold is not loaded', () => {
    const store = useEndgameStore()
    expect(store.progressPercent(500_000)).toBe(0)
  })

  it('progressPercent computes correctly relative to threshold', async () => {
    gqlRequestMock.mockResolvedValue({ endgameStatus: mockStatus })
    const store = useEndgameStore()
    await store.fetchStatus()

    expect(store.progressPercent(0)).toBe(0)
    expect(store.progressPercent(500_000)).toBe(50)
    expect(store.progressPercent(1_000_000)).toBe(100)
    // Capped at 100
    expect(store.progressPercent(2_000_000)).toBe(100)
  })

  it('checkMilestones returns newly crossed milestones', async () => {
    gqlRequestMock.mockResolvedValue({ endgameStatus: mockStatus })
    const store = useEndgameStore()
    await store.fetchStatus()

    // Player has 1% (10_000 of 1_000_000)
    const first = store.checkMilestones(10_000)
    expect(first).toContain(0.01)
    expect(first).not.toContain(0.1)
  })

  it('checkMilestones does not repeat already-fired milestones', async () => {
    gqlRequestMock.mockResolvedValue({ endgameStatus: mockStatus })
    const store = useEndgameStore()
    await store.fetchStatus()

    store.checkMilestones(10_000) // fires 1%
    const second = store.checkMilestones(10_000)
    expect(second).toHaveLength(0)
  })

  it('checkMilestones fires each milestone only once and in order', async () => {
    gqlRequestMock.mockResolvedValue({ endgameStatus: mockStatus })
    const store = useEndgameStore()
    await store.fetchStatus()

    const at90pct = store.checkMilestones(900_000) // crosses 1%, 10%, 25%, 50%, 75%, 90%
    expect(at90pct).toEqual(expect.arrayContaining([...ENDGAME_MILESTONES]))
    expect(at90pct).toHaveLength(ENDGAME_MILESTONES.length)

    // No more should fire at the same amount
    const again = store.checkMilestones(900_000)
    expect(again).toHaveLength(0)
  })

  it('checkMilestones returns empty array when game has ended', async () => {
    gqlRequestMock.mockResolvedValue({
      endgameStatus: { ...mockStatus, gameEnded: true, winnerDisplayName: 'Alice' },
    })
    const store = useEndgameStore()
    await store.fetchStatus()

    const milestones = store.checkMilestones(900_000)
    expect(milestones).toHaveLength(0)
  })

  it('resetMilestones clears triggered milestones', async () => {
    gqlRequestMock.mockResolvedValue({ endgameStatus: mockStatus })
    const store = useEndgameStore()
    await store.fetchStatus()

    store.checkMilestones(10_000) // fire 1%
    expect(store.triggeredMilestones.size).toBe(1)

    store.resetMilestones()
    expect(store.triggeredMilestones.size).toBe(0)
  })

  it('startPolling calls fetchStatus immediately and sets up interval', async () => {
    vi.useFakeTimers()
    gqlRequestMock.mockResolvedValue({ endgameStatus: mockStatus })
    const store = useEndgameStore()

    store.startPolling(30_000)
    // Wait for the initial async fetchStatus to complete
    await Promise.resolve()
    await Promise.resolve()
    expect(gqlRequestMock).toHaveBeenCalledTimes(1)

    await vi.advanceTimersByTimeAsync(30_000)
    await Promise.resolve()
    expect(gqlRequestMock).toHaveBeenCalledTimes(2)

    store.stopPolling()
    vi.useRealTimers()
  })

  it('stopPolling halts further fetches', async () => {
    vi.useFakeTimers()
    gqlRequestMock.mockResolvedValue({ endgameStatus: mockStatus })
    const store = useEndgameStore()

    store.startPolling(10_000)
    await Promise.resolve()
    await Promise.resolve()

    store.stopPolling()
    await vi.advanceTimersByTimeAsync(100_000)
    await Promise.resolve()

    // Only 1 call: the immediate one; nothing after stop
    expect(gqlRequestMock).toHaveBeenCalledTimes(1)
    vi.useRealTimers()
  })
})
