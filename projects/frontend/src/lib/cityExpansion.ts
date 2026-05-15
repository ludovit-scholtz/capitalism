import type { CityUnlockStatus } from '@/types'

export function computeCityUnlockProgress(status: Pick<CityUnlockStatus, 'isUnlocked' | 'requiredNetWorth' | 'currentNetWorth' | 'progressPercent'>): number {
  if (status.isUnlocked) {
    return 100
  }

  if (status.requiredNetWorth <= 0) {
    return 100
  }

  if (Number.isFinite(status.progressPercent) && status.progressPercent > 0) {
    return Math.max(0, Math.min(100, Math.round(status.progressPercent)))
  }

  return Math.max(0, Math.min(99, Math.round((status.currentNetWorth / status.requiredNetWorth) * 100)))
}

export function formatEstimatedTicksLabel(estimatedTicksToUnlock: number | null | undefined, locale = 'en-US'): string {
  if (estimatedTicksToUnlock == null || !Number.isFinite(estimatedTicksToUnlock) || estimatedTicksToUnlock <= 0) {
    return '—'
  }

  return new Intl.NumberFormat(locale).format(Math.round(estimatedTicksToUnlock))
}
