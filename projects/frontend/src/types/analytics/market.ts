export interface MarketPriceResult {
  cityId: string
  productTypeId: string
  productName: string
  clearingPrice: number
  totalVolume: number
  totalRevenue: number
  sellerCount: number
  currencyCode: string
  fromTick: number
  toTick: number
}

export interface MarketPriceHistoryPoint {
  tick: number
  clearingPrice: number
  totalVolume: number
  totalRevenue: number
  sellerCount: number
}

export interface ProductDemandEntry {
  productTypeId: string
  productName: string
  industry: string
  totalDemand: number
  totalQuantitySold: number
  satisfactionRate: number
  averageClearingPrice: number
  totalRevenue: number
  sellerCount: number
  topCompetitorCompanyName: string | null
  topCompetitorMarketSharePercent: number
}

export interface CityDemandSummaryResult {
  cityId: string
  cityName: string
  currencyCode: string
  fromTick: number
  toTick: number
  products: ProductDemandEntry[]
}
