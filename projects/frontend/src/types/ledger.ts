import type { BuildingLedgerSummary } from './building'

/** Company financial ledger summary */
export interface CompanyLedgerSummary {
  companyId: string
  companyName: string
  gameYear: number
  isCurrentGameYear: boolean
  currentCash: number
  /** ISO 4217 code for the company's primary operating currency. */
  primaryCurrencyCode: string
  /** Display symbol for the primary currency (e.g. "€", "Kč"). */
  primaryCurrencySymbol: string
  /** True when the company has buildings in multiple cities with different currencies. */
  hasMixedCurrencies: boolean
  totalRevenue: number
  totalMediaHouseIncome: number
  totalPurchasingCosts: number
  totalShippingCosts: number
  totalLaborCosts: number
  totalEnergyCosts: number
  totalMarketingCosts: number
  totalTaxPaid: number
  totalOtherCosts: number
  taxableIncome: number
  estimatedIncomeTax: number
  netIncome: number
  // Banking income/expense
  totalDepositInterestReceived: number
  totalDepositInterestPaid: number
  totalLoanInterestIncome: number
  totalLoanInterestExpense: number
  propertyValue: number
  propertyAppreciation: number
  buildingValue: number
  inventoryValue: number
  totalDepositsPlaced: number
  totalAssets: number
  totalPropertyPurchases: number
  totalStockPurchaseCashOut: number
  totalStockSaleCashIn: number
  cashFromOperations: number
  cashFromInvestments: number
  cashFromBanking: number
  firstRecordedTick: number
  lastRecordedTick: number
  incomeTaxDueAtTick: number
  incomeTaxDueGameTimeUtc: string
  incomeTaxDueGameYear: number
  isIncomeTaxSettled: boolean
  buildingSummaries: BuildingLedgerSummary[]
  history: CompanyLedgerHistoryYear[]
}

export interface CompanyLedgerHistoryYear {
  gameYear: number
  isCurrentGameYear: boolean
  totalRevenue: number
  totalLaborCosts: number
  totalEnergyCosts: number
  netIncome: number
  totalTaxPaid: number
  taxableIncome: number
  estimatedIncomeTax: number
  firstRecordedTick: number
  lastRecordedTick: number
}

export interface LedgerEntryResult {
  id: string
  category: string
  description: string
  amount: number
  recordedAtTick: number
  buildingId: string | null
  buildingName: string | null
  buildingType: string | null
  buildingUnitId: string | null
  productTypeId: string | null
  productName: string | null
  resourceTypeId: string | null
  resourceName: string | null
  currencyCode: string
  currencySymbol: string
  eventTag?: string | null
  eventDescription?: string | null
}
