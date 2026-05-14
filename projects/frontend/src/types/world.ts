export interface ResourceType {
  id: string
  name: string
  slug: string
  category: string
  basePrice: number
  weightPerUnit: number
  unitName: string
  unitSymbol: string
  imageUrl: string | null
  description: string | null
}

export interface Resource {
  resourceType: ResourceType
  abundance: number
}

export interface ProductType {
  id: string
  name: string
  slug: string
  imageUrl?: string | null
  industry: string
  basePrice: number
  baseCraftTicks: number
  outputQuantity: number
  energyConsumptionMwh: number
  basicLaborHours: number
  unitName: string
  unitSymbol: string
  isProOnly: boolean
  isUnlockedForCurrentPlayer: boolean
  description: string | null
  /** Whether this product decays in storage (food, beverage, healthcare). */
  isPerishable: boolean
  recipes: Recipe[]
}

export interface Recipe {
  resourceType: ResourceType | null
  inputProductType: Pick<ProductType, 'id' | 'name' | 'slug' | 'unitName' | 'unitSymbol'> | null
  quantity: number
}

/** Onboarding types */
export interface City {
  id: string
  name: string
  countryCode: string
  /** ISO 4217 currency code (e.g. "EUR", "CZK"). */
  currencyCode: string
  latitude: number
  longitude: number
  population: number
  baseSalaryPerManhour?: number
  /** Local fuel-price index relative to EUR baseline (1.0). Values > 1.0 mean costlier fuel. */
  fuelPriceIndex?: number
  resources: Resource[]
}

/** Summary of a single power plant in the city power balance view. */
export interface PowerPlantSummary {
  buildingId: string
  buildingName: string
  /** Plant type: COAL | GAS | SOLAR | WIND | NUCLEAR */
  plantType: string
  outputMw: number
  powerStatus: string
}

/** City-level power balance snapshot returned by the cityPowerBalance query. */
export interface CityPowerBalance {
  cityId: string
  totalSupplyMw: number
  totalDemandMw: number
  reserveMw: number
  reservePercent: number
  /** BALANCED | CONSTRAINED | CRITICAL */
  status: string
  powerPlants: PowerPlantSummary[]
  powerPlantCount: number
  consumerBuildingCount: number
}

/** A single future-tick weather forecast entry. */
export interface WeatherTick {
  tick: number
  windPercent: number
  solarPercent: number
}

/** Rolling 50-tick weather forecast for a city returned by cityWeatherForecast(cityId). */
export interface CityWeatherForecast {
  cityId: string
  currentWindPercent: number
  currentSolarPercent: number
  forecast: WeatherTick[]
}

/** A media house building in a city, returned by cityMediaHouses query. */
export interface CityMediaHouseInfo {
  id: string
  name: string
  cityId: string
  cityName: string
  /** NEWSPAPER | RADIO | TV — null if not yet configured */
  mediaType: string | null
  ownerCompanyId: string
  ownerCompanyName: string
  /** 1.0 = spaper, 1.5 = Radio, 2.0 = TV */
  effectivenessMultiplier: number
  /** POWERED | CONSTRAINED | OFFLINE */
  powerStatus: string
  isUnderConstruction: boolean
  /** Content ranking as a percentage (0–100) relative to the top outlet in the same city+category */
  contentRanking: number
  /** Current accumulated content value */
  contentValue: number
  /** Per-tick content spending configured by the owner, null if not set */
  contentBudgetPerTick: number | null
  /** True when the outlet is a government-seeded baseline media house */
  isGovernmentOwned: boolean
}

export interface CityEconomicReport {
  id: string
  cityId: string
  taxCycleEnd: number
  totalSalaries: number
  totalPublicRevenue: number
  activeCompanies: number
  totalPowerConsumption: number
  totalPowerSupply: number
  averageProductQuality: number
  economicIndex: number
  computedAtUtc: string
}

export interface CityEconomicReportResult {
  latest: CityEconomicReport | null
  history: CityEconomicReport[]
}

export interface NpcCompanySummary {
  id: string
  companyId: string
  name: string
  archetype: string
  difficultyLevel: number
  homeCityId: string
  homeCityName: string
  isActive: boolean
  createdAtUtc: string
  buildingCount: number
}

export interface CompetitorMarketShareByCategory {
  category: string
  sharePercent: number
}

export interface CityCompetitorEntry {
  companyId: string
  companyName: string
  isNpc: boolean
  npcCompanyId: string | null
  archetype: string | null
  buildingCount: number
  estimatedRevenueLastTicks: number
  marketSharePercent: number
  marketShareByCategory: CompetitorMarketShareByCategory[]
  trend: 'UP' | 'DOWN' | 'STABLE'
}
