import type { Company } from './company'

/** Matches backend PlayerRole constants */
export type PlayerRole = 'PLAYER' | 'ADMIN'

export type AccountContextType = 'PERSON' | 'COMPANY'

/** Matches backend Player entity */
export interface Player {
  id: string
  email: string
  displayName: string
  role: PlayerRole
  createdAtUtc: string
  lastLoginAtUtc: string | null
  personalCash: number
  activeAccountType: AccountContextType
  activeCompanyId: string | null
  onboardingCompletedAtUtc: string | null
  onboardingCurrentStep: string | null
  onboardingIndustry: string | null
  onboardingCityId: string | null
  onboardingCompanyId: string | null
  onboardingFactoryLotId: string | null
  onboardingShopBuildingId: string | null
  onboardingFirstSaleCompletedAtUtc: string | null
  proSubscriptionEndsAtUtc: string | null
  companies: Company[]
}

/** Matches backend AuthPayload response */
export interface AuthPayload {
  token: string
  expiresAtUtc: string
  player: Player
}

/** Matches backend ApplicationUser entity */
export interface User {
  id: string
  email: string
  displayName: string
  role: 'ADMIN' | 'CONTRIBUTOR'
  createdAtUtc: string
  lastLoginAtUtc: string | null
}

export interface AccountContextResult {
  activeAccountType: AccountContextType
  activeCompanyId: string | null
  activeAccountName: string
}
