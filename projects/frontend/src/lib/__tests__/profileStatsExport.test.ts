import { buildProfileStatsCsv } from '@/lib/profileStatsExport'
import { describe, it, expect } from 'vitest'

describe('buildProfileStatsCsv', () => {
  it('exports expected headers and core fields', () => {
    const csv = buildProfileStatsCsv({
      displayName: 'Alice',
      leaderboardRank: 3,
      totalWealthUsd: 1234.5,
      totalCompanyEquityUsd: 987.6,
      companyCount: 2,
      totalProductsSold: 10,
      citiesWithBuildings: 1,
      activeBuildingTypes: ['FACTORY'],
      badgeTypes: ['FIRST_B2B_TRADE'],
      bestRank: 1,
    })

    expect(csv).toContain('Metric,Value')
    expect(csv).toContain('Display Name,Alice')
    expect(csv).toContain('Current Rank,3')
    expect(csv).toContain('Best Rank,1')
    expect(csv).toContain('Total Wealth (USD),1234.50')
  })

  it('escapes commas and quotes', () => {
    const csv = buildProfileStatsCsv({
      displayName: 'Alice, "The Trader"',
      leaderboardRank: 0,
      totalWealthUsd: 0,
      totalCompanyEquityUsd: 0,
      companyCount: 0,
      totalProductsSold: 0,
      citiesWithBuildings: 0,
      activeBuildingTypes: [],
      badgeTypes: [],
      bestRank: null,
    })

    expect(csv).toContain('"Alice, ""The Trader"""')
    expect(csv).toContain('Current Rank,N/A')
    expect(csv).toContain('Badges,N/A')
  })
})
