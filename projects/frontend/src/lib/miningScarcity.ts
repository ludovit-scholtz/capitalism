export function computeMiningEfficiencyFactor(
  quantityRemaining: number | null | undefined,
  initialQuantity: number | null | undefined,
): number {
  if (quantityRemaining == null || initialQuantity == null || initialQuantity <= 0) {
    return 1
  }

  const ratio = Math.max(0, Math.min(1, quantityRemaining / initialQuantity))

  if (ratio > 0.7) return 1

  if (ratio > 0.2) {
    const segmentProgress = (ratio - 0.2) / 0.5
    return 0.6 + segmentProgress * 0.4
  }

  const lowSegmentProgress = ratio / 0.2
  return 0.3 + lowSegmentProgress * 0.3
}
