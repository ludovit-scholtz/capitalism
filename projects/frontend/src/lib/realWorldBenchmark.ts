import type { RealWorldWealth } from '@/types'

export function computeTargetProgressPercent(currentWealthUsd: number, winningThresholdUsd: number): number {
  if (winningThresholdUsd <= 0 || currentWealthUsd <= 0) {
    return 0
  }

  return Math.min(100, (currentWealthUsd / winningThresholdUsd) * 100)
}

export function computeDistanceToWinUsd(currentWealthUsd: number, winningThresholdUsd: number): number {
  if (winningThresholdUsd <= 0) {
    return 0
  }

  return Math.max(0, winningThresholdUsd - Math.max(currentWealthUsd, 0))
}

export function findSurpassedBenchmark(
  benchmarks: RealWorldWealth[],
  currentWealthUsd: number,
): RealWorldWealth | null {
  if (currentWealthUsd <= 0 || benchmarks.length === 0) {
    return null
  }

  return (
    [...benchmarks]
      .sort((left, right) => left.rank - right.rank)
      .find((entry) => currentWealthUsd >= entry.wealthUsd) ?? null
  )
}
