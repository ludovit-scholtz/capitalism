/**
 * Pure helpers for the city labor market section. The constants and formulas mirror
 * `projects/Shared/Economy/LaborMarketCalculator.cs`; keep them in sync.
 */

export interface AffectedBuilding {
  buildingId: string
  name: string
  staffingFactor: number
  onStrike: boolean
  strikeUntilTick: number | null
}

export interface CityLaborMarket {
  cityId: string
  cityName: string
  population: number
  laborPool: number
  laborDemand: number
  utilization: number
  unemploymentRate: number
  wageIndex: number
  staffingFillRate: number
  averageSalaryMultiplier: number
  updatedAtTick: number
  yourSalaryMultiplier: number | null
  payRatio: number | null
  strikeProne: boolean | null
  affectedBuildings: AffectedBuilding[]
}

export type StrikeRisk = 'NONE' | 'ELEVATED' | 'HIGH'
export type PayPosition = 'BELOW' | 'FAIR' | 'ABOVE'

/** A company paying less than this share of the city reference wage is at risk of strikes. */
export const STRIKE_PAY_RATIO_THRESHOLD = 0.85

/** Below this ratio strikes become frequent. */
export const HIGH_STRIKE_RISK_PAY_RATIO = 0.7

export const CITY_LABOR_MARKETS_QUERY = `query CityLaborMarkets($companyId: UUID) {
  cityLaborMarkets(companyId: $companyId) {
    cityId
    cityName
    population
    laborPool
    laborDemand
    utilization
    unemploymentRate
    wageIndex
    staffingFillRate
    averageSalaryMultiplier
    updatedAtTick
    yourSalaryMultiplier
    payRatio
    strikeProne
    affectedBuildings {
      buildingId
      name
      staffingFactor
      onStrike
      strikeUntilTick
    }
  }
}`

export function strikeRisk(payRatio: number): StrikeRisk {
  if (payRatio >= STRIKE_PAY_RATIO_THRESHOLD) return 'NONE'
  return payRatio >= HIGH_STRIKE_RISK_PAY_RATIO ? 'ELEVATED' : 'HIGH'
}

export function payPosition(payRatio: number): PayPosition {
  if (payRatio < 0.95) return 'BELOW'
  return payRatio > 1.05 ? 'ABOVE' : 'FAIR'
}

export function unemploymentPercent(rate: number): number {
  return Math.round(Math.min(Math.max(rate, 0), 1) * 100)
}

/** Wage pressure as a signed percent change of the base wage, e.g. 1.5 -> "+50%". */
export function formatWagePressure(wageIndex: number): string {
  const percent = Math.round((wageIndex - 1) * 100)
  return `${percent >= 0 ? '+' : ''}${percent}%`
}
