export type OperationsPlayerListItem = {
  id: string
  displayName: string
  email: string
  role?: string
  createdAtUtc?: string
  lastLoginAtUtc?: string | null
  personalCash: number
  totalCompanyCash: number
  totalCompanyEquity?: number
  companyCount: number
  cityNames?: string[]
  companies: Array<{ id: string; name: string; cash: number }>
}

export type OperationsPlayersSortKey = 'name' | 'email' | 'lastSeen' | 'balance' | 'companyEquity' | 'joinDate'

export function filterAndSortOperationsPlayers(
  players: OperationsPlayerListItem[],
  options: {
    searchQuery: string
    selectedCity: string
    selectedCompany: string
    sortKey: OperationsPlayersSortKey
    sortAsc: boolean
  },
) {
  const query = options.searchQuery.trim().toLowerCase()

  const filtered = players.filter((player) => {
    const playerCities = player.cityNames ?? []
    const playerCompanyNames = player.companies.map((company) => company.name)

    if (options.selectedCity !== 'ALL' && !playerCities.includes(options.selectedCity)) {
      return false
    }

    if (options.selectedCompany !== 'ALL' && !playerCompanyNames.includes(options.selectedCompany)) {
      return false
    }

    if (!query) {
      return true
    }

    return [
      player.displayName,
      player.email,
      ...playerCities,
      ...playerCompanyNames,
    ]
      .join(' ')
      .toLowerCase()
      .includes(query)
  })

  return [...filtered].sort((left, right) => {
    let compare = 0

    switch (options.sortKey) {
      case 'email':
        compare = left.email.localeCompare(right.email)
        break
      case 'lastSeen':
        compare = (left.lastLoginAtUtc ?? '').localeCompare(right.lastLoginAtUtc ?? '')
        break
      case 'balance':
        compare = left.personalCash + left.totalCompanyCash - (right.personalCash + right.totalCompanyCash)
        break
      case 'companyEquity':
        compare = (left.totalCompanyEquity ?? left.totalCompanyCash) - (right.totalCompanyEquity ?? right.totalCompanyCash)
        break
      case 'joinDate':
        compare = (left.createdAtUtc ?? '').localeCompare(right.createdAtUtc ?? '')
        break
      case 'name':
      default:
        compare = left.displayName.localeCompare(right.displayName)
        break
    }

    return options.sortAsc ? compare : -compare
  })
}

export type OperationsAnalyticsRow = {
  productTypeId: string
  productName: string
  industry: string
  basePrice: number
  totalProduced: number
  activeManufacturerCount: number
  totalSold: number
  totalRevenue: number
  avgSellingPrice: number | null
  marketSize: number
  activeSellerCount: number
  activeCityCount: number
  totalMaterialCost: number
  totalLaborCost: number
  totalEnergyCost: number
  totalCost: number
  marketSaturation: number
  totalMarketingSpend: number
  totalResearchSpend?: number
  marketingScore?: number
  researchScore?: number
}

export type OperationsAnalyticsSortKey =
  | 'basePrice'
  | 'totalProduced'
  | 'activeManufacturerCount'
  | 'totalSold'
  | 'totalRevenue'
  | 'marketSize'
  | 'activeSellerCount'
  | 'activeCityCount'
  | 'totalMaterialCost'
  | 'totalLaborCost'
  | 'totalEnergyCost'
  | 'totalCost'
  | 'totalMarketingSpend'
  | 'totalResearchSpend'
  | 'marketingScore'
  | 'researchScore'
  | 'marketSaturation'

export function filterAndSortOperationsAnalyticsRows(
  rows: OperationsAnalyticsRow[],
  options: {
    searchQuery: string
    industryFilter: string
    sortKey: OperationsAnalyticsSortKey
    sortAsc: boolean
  },
) {
  const query = options.searchQuery.trim().toLowerCase()

  const filtered = rows.filter((row) => {
    if (options.industryFilter !== 'ALL' && row.industry !== options.industryFilter) {
      return false
    }

    if (!query) {
      return true
    }

    return row.productName.toLowerCase().includes(query)
  })

  return [...filtered].sort((left, right) => {
    const leftValue = left[options.sortKey] ?? 0
    const rightValue = right[options.sortKey] ?? 0
    return options.sortAsc ? Number(leftValue) - Number(rightValue) : Number(rightValue) - Number(leftValue)
  })
}

function escapeCsvValue(value: string | number | null | undefined) {
  const raw = value == null ? '' : String(value)
  if (raw.includes(',') || raw.includes('"') || raw.includes('\n') || raw.includes('\r')) {
    return `"${raw.replace(/"/g, '""')}"`
  }

  return raw
}

export function buildOperationsAnalyticsCsv(rows: OperationsAnalyticsRow[]) {
  const headers = [
    'Product',
    'Industry',
    'BasePrice',
    'Produced',
    'Manufacturers',
    'Sold',
    'Revenue',
    'AveragePrice',
    'MarketSize',
    'Sellers',
    'Cities',
    'TotalCost',
    'Materials',
    'Labor',
    'Energy',
    'MarketingSpend',
    'ResearchSpend',
    'MarketingScore',
    'ResearchScore',
    'MarketSaturation',
  ]

  const body = rows.map((row) => [
    escapeCsvValue(row.productName),
    escapeCsvValue(row.industry),
    row.basePrice,
    row.totalProduced,
    row.activeManufacturerCount,
    row.totalSold,
    row.totalRevenue,
    row.avgSellingPrice ?? '',
    row.marketSize,
    row.activeSellerCount,
    row.activeCityCount,
    row.totalCost,
    row.totalMaterialCost,
    row.totalLaborCost,
    row.totalEnergyCost,
    row.totalMarketingSpend,
    row.totalResearchSpend ?? 0,
    row.marketingScore ?? 0,
    row.researchScore ?? 0,
    row.marketSaturation,
  ])

  return [headers, ...body].map((row) => row.join(',')).join('\n')
}
