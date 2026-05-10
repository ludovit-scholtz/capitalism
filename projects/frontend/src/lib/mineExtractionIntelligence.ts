export interface MineExtractionDailyPoint {
  dayIndex: number
  extractedAmount: number
  efficiencyPercent: number
  reserveRemaining: number
}

export function buildSparklinePath(
  data: MineExtractionDailyPoint[],
  width: number,
  height: number,
  projectedDays: number,
): string {
  if (data.length < 2) return ''
  const max = Math.max(...data.map((d) => d.extractedAmount), 0.001)
  const span = Math.max(1, data.length - 1 + Math.max(0, projectedDays))
  const step = width / span

  return data
    .map((point, i) => {
      const x = i * step
      const y = height - (point.extractedAmount / max) * height
      return `${i === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`
    })
    .join(' ')
}

export function buildDepletionTrendlinePath(
  data: MineExtractionDailyPoint[],
  width: number,
  height: number,
  projectedDays: number,
): string {
  if (data.length < 2 || projectedDays <= 0) return ''
  const max = Math.max(...data.map((d) => d.extractedAmount), 0.001)
  const span = Math.max(1, data.length - 1 + projectedDays)
  const step = width / span
  const last = data[data.length - 1]
  if (!last) return ''

  const xStart = (data.length - 1) * step
  const yStart = height - (last.extractedAmount / max) * height
  const xEnd = (data.length - 1 + projectedDays) * step

  return `M ${xStart.toFixed(1)} ${yStart.toFixed(1)} L ${xEnd.toFixed(1)} ${height.toFixed(1)}`
}

export function summarizeExtractionTrend(
  data: MineExtractionDailyPoint[],
  estimatedDaysRemaining: number | null | undefined,
): 'empty' | 'growing' | 'declining' | 'stable' {
  if (data.length === 0) return 'empty'
  if (data.length === 1) return 'stable'
  const first = data[0]?.extractedAmount ?? 0
  const last = data[data.length - 1]?.extractedAmount ?? 0
  const delta = last - first
  if (Math.abs(delta) < 0.5) return 'stable'
  if (estimatedDaysRemaining !== null && estimatedDaysRemaining !== undefined && estimatedDaysRemaining < 10) {
    return 'declining'
  }
  return delta > 0 ? 'growing' : 'declining'
}
