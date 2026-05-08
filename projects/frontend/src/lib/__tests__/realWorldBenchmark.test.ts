import { describe, expect, it } from 'vitest'
import {
  computeDistanceToWinUsd,
  computeTargetProgressPercent,
  findSurpassedBenchmark,
} from '@/lib/realWorldBenchmark'
import type { RealWorldWealth } from '@/types'

const benchmarks: RealWorldWealth[] = [
  { id: 'rw-1', rank: 1, name: 'Elon Musk', wealthUsd: 430_000_000_000 },
  { id: 'rw-2', rank: 2, name: 'Jeff Bezos', wealthUsd: 245_000_000_000 },
  { id: 'rw-3', rank: 3, name: 'Mark Zuckerberg', wealthUsd: 216_000_000_000 },
]

describe('realWorldBenchmark helpers', () => {
  it('computes target progress and caps at 100%', () => {
    expect(computeTargetProgressPercent(0, 430_000_000_000)).toBe(0)
    expect(computeTargetProgressPercent(215_000_000_000, 430_000_000_000)).toBe(50)
    expect(computeTargetProgressPercent(500_000_000_000, 430_000_000_000)).toBe(100)
  })

  it('computes remaining distance to win and never goes below zero', () => {
    expect(computeDistanceToWinUsd(200_000_000_000, 430_000_000_000)).toBe(230_000_000_000)
    expect(computeDistanceToWinUsd(500_000_000_000, 430_000_000_000)).toBe(0)
  })

  it('finds the highest reached billionaire rank from benchmark list', () => {
    expect(findSurpassedBenchmark(benchmarks, 100_000_000_000)).toBeNull()
    expect(findSurpassedBenchmark(benchmarks, 220_000_000_000)?.rank).toBe(3)
    expect(findSurpassedBenchmark(benchmarks, 500_000_000_000)?.rank).toBe(1)
  })
})
