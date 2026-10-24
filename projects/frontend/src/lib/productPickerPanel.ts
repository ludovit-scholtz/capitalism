import type { RankedProductResult } from '@/types'
import { getProductImageUrl } from '@/lib/catalogPresentation'

export function productImage(r: RankedProductResult): string {
  return getProductImageUrl(r.productType)
}

export function rankingReasonClass(reason: string): string {
  if (reason === 'connected') return 'badge-connected'
  if (reason === 'manufacturing') return 'badge-active-production'
  if (reason === 'used_by_company') return 'badge-used'
  return ''
}

export const availabilityReasonMeta = {
  connected_upstream: {
    labelKey: 'productPicker.reasonConnectedUpstream',
    detailKey: 'productPicker.contextConnectedUpstream',
    className: 'badge-connected',
  },
  current_stock: {
    labelKey: 'productPicker.reasonCurrentStock',
    detailKey: 'productPicker.contextCurrentStock',
    className: 'badge-stock',
  },
  connected_and_stock: {
    labelKey: 'productPicker.reasonConnectedAndStock',
    detailKey: 'productPicker.contextConnectedAndStock',
    className: 'badge-connected-stock',
  },
} as const

export function getAvailabilityMeta(entry: RankedProductResult) {
  return entry.availabilityReason ? availabilityReasonMeta[entry.availabilityReason] : null
}

export function availabilityReasonClass(entry: RankedProductResult): string {
  const meta = getAvailabilityMeta(entry)
  if (meta) return meta.className
  return rankingReasonClass(entry.rankingReason)
}

export function isLocked(entry: RankedProductResult): boolean {
  return entry.productType.isProOnly && !entry.productType.isUnlockedForCurrentPlayer
}
