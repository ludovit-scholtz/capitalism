/** Matches backend PlayerRole constants */
export type PlayerRole = 'PLAYER' | 'ADMIN'

/** Matches backend Player entity */
export interface Player {
  id: string
  email: string
  displayName: string
  role: PlayerRole
  createdAtUtc: string
  lastLoginAtUtc: string | null
  companies: Company[]
}

/** Matches backend AuthPayload response */
export interface AuthPayload {
  token: string
  expiresAtUtc: string
  player: Player
}

/** Matches backend Company entity */
export interface Company {
  id: string
  playerId: string
  name: string
  cash: number
  foundedAtUtc: string
  buildings: Building[]
}

/** Matches backend Building entity */
export interface Building {
  id: string
  companyId: string
  cityId: string
  type: string
  name: string
  latitude: number
  longitude: number
  level: number
  powerConsumption: number
  isForSale: boolean
  askingPrice: number | null
  pricePerSqm: number | null
  occupancyPercent: number | null
  totalAreaSqm: number | null
  powerPlantType: string | null
  powerOutput: number | null
  mediaType: string | null
  interestRate: number | null
  builtAtUtc: string
  units: BuildingUnit[]
}

/** Matches backend BuildingUnit entity */
export interface BuildingUnit {
  id: string
  buildingId: string
  unitType: string
  gridX: number
  gridY: number
  level: number
  linkRight: boolean
  linkDown: boolean
  linkDiagonalDown: boolean
  linkDiagonalUp: boolean
}

/** Matches backend City entity */
export interface City {
  id: string
  name: string
  countryCode: string
  latitude: number
  longitude: number
  population: number
  averageRentPerSqm: number
  resources: CityResource[]
}

/** Matches backend CityResource entity */
export interface CityResource {
  id: string
  cityId: string
  resourceType: ResourceType
  abundance: number
}

/** Matches backend ResourceType entity */
export interface ResourceType {
  id: string
  name: string
  slug: string
  category: string
  basePrice: number
  weightPerUnit: number
  description: string | null
}

/** Matches backend ProductType entity */
export interface ProductType {
  id: string
  name: string
  slug: string
  industry: string
  basePrice: number
  baseCraftTicks: number
  isProOnly: boolean
  description: string | null
  recipes: ProductRecipe[]
}

/** Matches backend ProductRecipe entity */
export interface ProductRecipe {
  id: string
  resourceType: ResourceType
  quantity: number
}

/** Matches backend PlayerRanking response */
export interface PlayerRanking {
  playerId: string
  displayName: string
  totalWealth: number
  companyCount: number
}

/** Matches backend GameState entity */
export interface GameState {
  currentTick: number
  tickIntervalSeconds: number
  taxCycleTicks: number
  taxRate: number
}

/** Matches backend OnboardingResult response */
export interface OnboardingResult {
  company: Company
  factory: Building
  salesShop: Building
  selectedProduct: ProductType
}
