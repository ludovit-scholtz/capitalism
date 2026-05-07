import { describe, expect, it } from 'vitest'
import {
  buildOperationsAnalyticsCsv,
  filterAndSortOperationsAnalyticsRows,
  filterAndSortOperationsPlayers,
  type OperationsAnalyticsRow,
  type OperationsPlayerListItem,
} from '../operationsDashboard'

describe('operationsDashboard helpers', () => {
  const players: OperationsPlayerListItem[] = [
    {
      id: 'player-a',
      displayName: 'Alice Admin',
      email: 'alice@test.com',
      createdAtUtc: '2026-01-01T00:00:00Z',
      lastLoginAtUtc: '2026-01-03T00:00:00Z',
      personalCash: 100,
      totalCompanyCash: 300,
      totalCompanyEquity: 700,
      companyCount: 1,
      cityNames: ['Bratislava'],
      companies: [{ id: 'company-a', name: 'Alpha Works', cash: 300 }],
    },
    {
      id: 'player-b',
      displayName: 'Bob Builder',
      email: 'bob@test.com',
      createdAtUtc: '2026-02-01T00:00:00Z',
      lastLoginAtUtc: '2026-01-02T00:00:00Z',
      personalCash: 50,
      totalCompanyCash: 200,
      totalCompanyEquity: 400,
      companyCount: 1,
      cityNames: ['Prague'],
      companies: [{ id: 'company-b', name: 'Brick Labs', cash: 200 }],
    },
  ]

  const analyticsRows: OperationsAnalyticsRow[] = [
    {
      productTypeId: 'chair',
      productName: 'Wooden Chair',
      industry: 'FURNITURE',
      basePrice: 45,
      totalProduced: 10,
      activeManufacturerCount: 1,
      totalSold: 9,
      totalRevenue: 450,
      avgSellingPrice: 50,
      marketSize: 12,
      activeSellerCount: 1,
      activeCityCount: 1,
      totalMaterialCost: 80,
      totalLaborCost: 40,
      totalEnergyCost: 20,
      totalCost: 140,
      marketSaturation: 75,
      totalMarketingSpend: 15,
      totalResearchSpend: 12,
      marketingScore: 60,
      researchScore: 42,
    },
    {
      productTypeId: 'bread',
      productName: 'Bread',
      industry: 'FOOD_PROCESSING',
      basePrice: 3,
      totalProduced: 50,
      activeManufacturerCount: 2,
      totalSold: 45,
      totalRevenue: 135,
      avgSellingPrice: 3,
      marketSize: 60,
      activeSellerCount: 2,
      activeCityCount: 2,
      totalMaterialCost: 30,
      totalLaborCost: 20,
      totalEnergyCost: 10,
      totalCost: 60,
      marketSaturation: 50,
      totalMarketingSpend: 5,
      totalResearchSpend: 4,
      marketingScore: 25,
      researchScore: 18,
    },
  ]

  it('filters players by city and sorts by company equity descending', () => {
    const result = filterAndSortOperationsPlayers(players, {
      searchQuery: '',
      selectedCity: 'Bratislava',
      selectedCompany: 'ALL',
      sortKey: 'companyEquity',
      sortAsc: false,
    })

    expect(result).toHaveLength(1)
    expect(result[0]?.displayName).toBe('Alice Admin')
  })

  it('matches players by company name and email search', () => {
    const result = filterAndSortOperationsPlayers(players, {
      searchQuery: 'brick',
      selectedCity: 'ALL',
      selectedCompany: 'ALL',
      sortKey: 'name',
      sortAsc: true,
    })

    expect(result).toHaveLength(1)
    expect(result[0]?.email).toBe('bob@test.com')
  })

  it('filters analytics rows by industry and sorts by research score', () => {
    const result = filterAndSortOperationsAnalyticsRows(analyticsRows, {
      searchQuery: '',
      industryFilter: 'FURNITURE',
      sortKey: 'researchScore',
      sortAsc: false,
    })

    expect(result).toHaveLength(1)
    expect(result[0]?.productName).toBe('Wooden Chair')
  })

  it('builds analytics csv with score columns', () => {
    const csv = buildOperationsAnalyticsCsv(analyticsRows)

    expect(csv).toContain('MarketingScore')
    expect(csv).toContain('ResearchScore')
    expect(csv).toContain('Wooden Chair')
    expect(csv).toContain('60')
    expect(csv).toContain('42')
  })
})
