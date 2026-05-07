export interface ProfileExportData {
  displayName: string
  leaderboardRank: number
  totalWealthUsd: number
  totalCompanyEquityUsd: number
  companyCount: number
  totalProductsSold: number
  citiesWithBuildings: number
  activeBuildingTypes: string[]
  badgeTypes: string[]
  bestRank: number | null
}

function escapeCsvCell(value: string | number): string {
  const raw = String(value)
  if (raw.includes(',') || raw.includes('"') || raw.includes('\n')) {
    return `"${raw.replace(/"/g, '""')}"`
  }
  return raw
}

export function buildProfileStatsCsv(data: ProfileExportData): string {
  const rows: Array<[string, string | number]> = [
    ['Display Name', data.displayName],
    ['Current Rank', data.leaderboardRank > 0 ? data.leaderboardRank : 'N/A'],
    ['Best Rank', data.bestRank ?? 'N/A'],
    ['Total Wealth (USD)', data.totalWealthUsd.toFixed(2)],
    ['Company Equity (USD)', data.totalCompanyEquityUsd.toFixed(2)],
    ['Company Count', data.companyCount],
    ['Total Products Sold', data.totalProductsSold],
    ['Cities Active', data.citiesWithBuildings],
    ['Active Building Types', data.activeBuildingTypes.join(', ') || 'N/A'],
    ['Badges', data.badgeTypes.join(', ') || 'N/A'],
  ]

  const lines = ['Metric,Value']
  for (const [metric, value] of rows) {
    lines.push(`${escapeCsvCell(metric)},${escapeCsvCell(value)}`)
  }
  return `${lines.join('\n')}\n`
}
