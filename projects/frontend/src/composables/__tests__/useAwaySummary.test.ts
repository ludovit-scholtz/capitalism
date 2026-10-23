import { describe, expect, it, vi } from 'vitest'
import type { AwaySummary } from '@/lib/awaySummary'

vi.mock('@/lib/graphql', () => ({ gqlRequest: vi.fn() }))

function notable(): AwaySummary {
  return { sinceTick: 100, currentTick: 140, ticksAway: 40, revenueUsd: 18_000, costsUsd: -3_000, netUsd: 15_000, loanPaymentsCount: 2 }
}

async function setup(options: { lastSeen: number | null; shown?: boolean; summary?: AwaySummary | Error }) {
  const { createAwaySummaryController } = await import('../useAwaySummary')
  const store = { lastSeen: options.lastSeen, shown: options.shown ?? false }
  const fetchSummary = vi.fn(async (sinceTick: number) => {
    void sinceTick
    const result = options.summary ?? notable()
    if (result instanceof Error) throw result
    return result
  })
  const controller = createAwaySummaryController({
    readLastSeenTick: () => store.lastSeen,
    readShown: () => store.shown,
    markShown: () => {
      store.shown = true
    },
    fetchSummary,
  })
  return { controller, store, fetchSummary }
}

describe('createAwaySummaryController', () => {
  it('fetches from the remembered tick and opens the summary when something happened', async () => {
    const { controller, fetchSummary } = await setup({ lastSeen: 100 })

    await controller.onTick(140)

    expect(fetchSummary).toHaveBeenCalledWith(100)
    expect(controller.summary.value?.netUsd).toBe(15_000)
    expect(controller.open.value).toBe(true)
  })

  it('asks only once per session', async () => {
    const { controller, fetchSummary, store } = await setup({ lastSeen: 100 })

    await controller.onTick(140)
    controller.close()
    await controller.onTick(200)

    expect(fetchSummary).toHaveBeenCalledTimes(1)
    expect(store.shown).toBe(true)
    expect(controller.open.value).toBe(false)
  })

  it('does not ask for a short absence', async () => {
    const { controller, fetchSummary } = await setup({ lastSeen: 138 })

    await controller.onTick(140)

    expect(fetchSummary).not.toHaveBeenCalled()
    expect(controller.open.value).toBe(false)
  })

  it('does not ask a browser that never saw a tick', async () => {
    const { controller, fetchSummary } = await setup({ lastSeen: null })

    await controller.onTick(500)

    expect(fetchSummary).not.toHaveBeenCalled()
  })

  it('stays closed when nothing happened while away', async () => {
    const quiet: AwaySummary = { ...notable(), revenueUsd: 0, costsUsd: 0, netUsd: 0, loanPaymentsCount: 0 }
    const { controller } = await setup({ lastSeen: 100, summary: quiet })

    await controller.onTick(140)

    expect(controller.open.value).toBe(false)
  })

  it('stays closed and does not throw when the server call fails', async () => {
    const { controller } = await setup({ lastSeen: 100, summary: new Error('offline') })

    await controller.onTick(140)

    expect(controller.summary.value).toBeNull()
    expect(controller.open.value).toBe(false)
  })

  it('does not fetch twice when ticks arrive while the first request is in flight', async () => {
    const { controller, fetchSummary } = await setup({ lastSeen: 100 })

    await Promise.all([controller.onTick(140), controller.onTick(141)])

    expect(fetchSummary).toHaveBeenCalledTimes(1)
  })
})

describe('createLastSeenRecorder', () => {
  it('writes the current tick only when the player leaves', async () => {
    const { createLastSeenRecorder } = await import('../useAwaySummary')
    let tick: number | null = 120
    const writes: number[] = []
    const recorder = createLastSeenRecorder({ getTick: () => tick, writeLastSeenTick: (value) => writes.push(value) })

    expect(writes).toEqual([])
    recorder.onLeave()
    tick = 130
    recorder.onLeave()

    expect(writes).toEqual([120, 130])
  })

  it('writes nothing while the tick is still unknown', async () => {
    const { createLastSeenRecorder } = await import('../useAwaySummary')
    const writes: number[] = []
    const recorder = createLastSeenRecorder({ getTick: () => null, writeLastSeenTick: (value) => writes.push(value) })

    recorder.onLeave()

    expect(writes).toEqual([])
  })
})
