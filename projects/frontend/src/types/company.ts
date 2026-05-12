import type { Building } from './building'
import type { AccountContextType } from './auth'

/** Matches backend Company entity */
export interface Company {
  id: string
  playerId: string
  name: string
  cash: number
  totalSharesIssued?: number
  dividendPayoutRatio?: number
  foundedAtUtc: string
  foundedAtTick?: number
  buildings: Building[]
}

export interface CompanyCitySalarySetting {
  cityId: string
  cityName: string
  /** ISO 4217 currency code of this city. Wages are denominated in this currency. */
  currencyCode: string
  baseSalaryPerManhour: number
  salaryMultiplier: number
  effectiveSalaryPerManhour: number
}

export interface CompanySettings {
  companyId: string
  companyName: string
  cash: number
  totalSharesIssued: number
  dividendPayoutRatio: number
  foundedAtTick: number
  administrationOverheadRate: number
  /** 0–1: how much company age contributes to overhead (reaches 1 at 2 years old) */
  ageFactor: number
  /** 0–1: how much company scale (assets) contributes to overhead */
  assetFactor: number
  assetValue: number
  /** ISO 4217 currency code for this company's local currency (e.g. "EUR", "CZK", "USD") */
  currencyCode: string
  citySalarySettings: CompanyCitySalarySetting[]
  pendingDividendProposal: CompanyDividendPolicyProposal | null
}

export interface CompanyDividendPolicyProposal {
  id: string
  dividendPercent: number
  votingCloseTick: number
  ticksRemaining: number
  forVotes: number
  againstVotes: number
  myVoteChoice: 'FOR' | 'AGAINST' | null
}

export interface PortfolioHolding {
  companyId: string
  companyName: string
  shareCount: number
  ownershipRatio: number
  sharePrice: number
  marketValue: number
}

export interface DividendPayment {
  id: string
  companyId: string
  companyName: string
  shareCount: number
  amountPerShare: number
  totalAmount: number
  gameYear: number
  recordedAtTick: number
  recordedAtUtc: string
  description: string
}

export interface PersonTradeRecord {
  id: string
  companyId: string
  companyName: string
  direction: 'BUY' | 'SELL'
  shareCount: number
  pricePerShare: number
  totalValue: number
  recordedAtTick: number
  recordedAtUtc: string
}

export interface PersonalInterestPayment {
  id: string
  companyId: string
  companyName: string
  bankBuildingId: string | null
  bankBuildingName: string | null
  amount: number
  recordedAtTick: number
  recordedAtUtc: string
  currencyCode: string
  description: string
}

export interface PersonAccount {
  playerId: string
  displayName: string
  /** Gross personal cash (includes the blocked tax reserve). */
  personalCash: number
  /** Amount blocked for future tax payment (15% of personal stock-sale proceeds). */
  taxReserve: number
  /** Spendable cash = personalCash - taxReserve. */
  availableCash: number
  /** Total net wealth = availableCash + portfolio market value. */
  totalNetWealth: number
  activeAccountType: AccountContextType
  activeCompanyId: string | null
  shareholdings: PortfolioHolding[]
  interestPayments: PersonalInterestPayment[]
  dividendPayments: DividendPayment[]
  stockTrades: PersonTradeRecord[]
}

export interface CompanyShareholder {
  holderName: string
  holderType: 'PERSON' | 'COMPANY'
  holderPlayerId: string | null
  holderCompanyId: string | null
  shareCount: number
  ownershipRatio: number
}

export interface CompanyOwnership {
  companyId: string
  companyName: string
  totalSharesIssued: number
  publicFloatShares: number
  shareholderCount: number
  shareholders: CompanyShareholder[]
}

export interface MergeCompanyResult {
  destinationCompanyId: string
  destinationCompanyName: string
  absorbedCompanyName: string
  cashTransferred: number
  buildingsTransferred: number
}

export interface ReplaceCeoResult {
  companyId: string
  companyName: string
  newCeoPlayerId: string
  newCeoDisplayName: string
}
