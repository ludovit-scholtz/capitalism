/** A loan offer published by a bank building. */
export interface LoanOfferSummary {
  id: string
  bankBuildingId: string
  bankBuildingName: string
  cityId: string
  cityName: string
  lenderCompanyId: string
  lenderCompanyName: string
  annualInterestRatePercent: number
  maxPrincipalPerLoan: number
  totalCapacity: number
  usedCapacity: number
  remainingCapacity: number
  durationTicks: number
  isActive: boolean
  createdAtTick: number
  createdAtUtc: string
}

/** Status values for a loan. */
export type LoanStatus = 'ACTIVE' | 'OVERDUE' | 'DEFAULTED' | 'REPAID'

/** An active or historical loan (borrower or lender view). */
export interface LoanSummary {
  id: string
  loanOfferId: string
  borrowerCompanyId: string
  borrowerCompanyName: string
  lenderCompanyId: string
  lenderCompanyName: string
  bankBuildingId: string
  bankBuildingName: string
  loanCurrencyCode: string
  originalPrincipal: number
  remainingPrincipal: number
  annualInterestRatePercent: number
  durationTicks: number
  startTick: number
  dueTick: number
  nextPaymentTick: number
  paymentAmount: number
  paymentsMade: number
  totalPayments: number
  status: LoanStatus
  missedPayments: number
  accumulatedPenalty: number
  defaultedAtTick: number | null
  acceptedAtUtc: string
  closedAtUtc: string | null
  /** ID of the building pledged as collateral, or null for unsecured loans. */
  collateralBuildingId: string | null
  /** Display name of the collateral building, or null for unsecured loans. */
  collateralBuildingName: string | null
  /** Appraised value of the collateral building at origination, or null for unsecured loans. */
  collateralAppraisedValue: number | null
  /** Current asking price of the collateral building when it is listed for sale. */
  collateralListingPrice: number | null
  /** Currency code used by the current collateral listing price. */
  collateralListingCurrencyCode: string | null
}

/** Collateral eligibility summary for one of the player's buildings. */
export interface CollateralEligibilitySummary {
  buildingId: string
  buildingName: string
  buildingType: string
  level: number
  appraisedValue: number
  maxBorrowable: number
  existingSecuredExposure: number
  remainingBorrowingCapacity: number
  /** Currency code for all monetary fields; equals the bank city currency when bankBuildingId was supplied. */
  currencyCode: string
  isEligible: boolean
  ineligibilityReason: string | null
}

/** A bank deposit made by a company into a bank building. */
export interface BankDepositSummary {
  id: string
  bankBuildingId: string
  bankBuildingName: string
  depositorCompanyId: string
  depositorCompanyName: string
  amount: number
  depositInterestRatePercent: number
  isBaseCapital: boolean
  isActive: boolean
  depositedAtTick: number
  depositedAtUtc: string
  withdrawnAtTick: number | null
  withdrawnAtUtc: string | null
  totalInterestPaid: number
  /** ISO 4217 currency code for the city where the bank is located (e.g. "EUR", "CZK"). */
  cityCurrencyCode: string
}

/** Public information about a bank building. */
export interface BankInfoSummary {
  bankBuildingId: string
  bankBuildingName: string
  cityId: string
  cityName: string
  /** ISO 4217 currency code for the city (e.g. "EUR", "CZK"). */
  cityCurrencyCode: string
  /** Display symbol for the city currency (e.g. "€", "Kč"). */
  cityCurrencySymbol: string
  /** Required base capital to open this bank, expressed in the local city currency. */
  baseCapitalRequirement: number
  lenderCompanyId: string
  lenderCompanyName: string
  depositInterestRatePercent: number
  lendingInterestRatePercent: number
  totalDeposits: number
  lendableCapacity: number
  outstandingLoanPrincipal: number
  availableLendingCapacity: number
  baseCapitalDeposited: boolean
  // Liquidity / central-bank fields
  centralBankDebt: number
  centralBankInterestRatePercent: number
  reserveRequirement: number
  availableCash: number
  reserveShortfall: number
  liquidityStatus: 'HEALTHY' | 'PRESSURED' | 'CRITICAL'
  // Pending deposit rate change
  /** Pending new deposit rate (%) scheduled by the owner. Null when no rate change is pending. */
  pendingDepositInterestRatePercent: number | null
  /** Game tick at which pendingDepositInterestRatePercent becomes effective. Null when no rate change is pending. */
  pendingDepositRateEffectiveTick: number | null
}

/** An immutable audit record for a bank deposit interest rate change. */
export interface BankDepositRateHistorySummary {
  id: string
  bankBuildingId: string
  previousRatePercent: number
  newRatePercent: number
  effectiveTick: number
  effectiveUtc: string
  scheduledAtTick: number
  scheduledAtUtc: string
  /** Number of deposits updated when the rate became effective (0 until applied). */
  affectedDepositCount: number
  /** True when the tick processor has already applied this rate to all deposits. */
  isApplied: boolean
  changedByPlayerName: string
}

/** FX exchange rate summary returned by the fxRates query. EUR-based (1 EUR = rate units of quoteCurrency). */
export interface FxRate {
  baseCurrencyCode: string
  quoteCurrencyCode: string
  /** How many units of quoteCurrency equal 1 EUR. */
  rate: number
  rateDate: string
  source: 'NBS' | 'FALLBACK'
  quoteCurrencySymbol: string
}

/** Quote for a forex swap — preview before execution. */
export interface ForexQuote {
  fromCurrencyCode: string
  toCurrencyCode: string
  fromAmount: number
  toAmount: number
  feeAmount: number
  feePercent: number
  rate: number
  availableFromBalance: number
  fromCurrencySymbol: string
  toCurrencySymbol: string
  /** Single-use UUID v4 nonce for replay protection. Pass back to executeForexSwap. */
  quoteNonce: string
  /** UTC timestamp when the quote was issued by the server (ISO 8601). */
  quotedAtUtc: string
  /** Number of seconds the quote is valid (default 30). */
  quoteExpiresInSeconds: number
}

/** Result of a successfully executed forex trade. */
export interface ForexTradeResult {
  tradeId: string
  fromCurrencyCode: string
  toCurrencyCode: string
  fromAmount: number
  toAmount: number
  feeAmount: number
  rate: number
  newFromBalance: number
  newToBalance: number
  fromCurrencySymbol: string
  toCurrencySymbol: string
}

/** A single entry in the player's forex trade history. */
export interface ForexTradeHistoryEntry {
  id: string
  fromCurrencyCode: string
  toCurrencyCode: string
  fromAmount: number
  toAmount: number
  feeAmount: number
  rate: number
  executedAtTick: number
  executedAtUtc: string
  fromCurrencySymbol: string
  toCurrencySymbol: string
}

/** A currency balance in the player's personal multi-currency wallet. */
export interface CurrencyBalance {
  currencyCode: string
  currencySymbol: string
  balance: number
}

// ── Gold AMM types ────────────────────────────────────────────────────────────

/** Summary of a gold AMM liquidity pool. */
export interface GoldAmmPool {
  id: string
  currencyCode: string
  currencySymbol: string
  fiatReserve: number
  goldReserve: number
  totalLiquidityShares: number
  impliedGoldPrice: number
  myPosition?: GoldAmmPosition | null
}

/** A player's liquidity position in a gold AMM pool. */
export interface GoldAmmPosition {
  id: string
  poolId: string
  currencyCode: string
  liquidityShares: number
  sharePercent: number
  claimableFiat: number
  claimableGold: number
  fiatProvided: number
  goldProvided: number
}

/** Quote for a gold AMM swap. */
export interface GoldAmmSwapQuote {
  direction: string
  currencyCode: string
  currencySymbol: string
  inputAmount: number
  outputAmount: number
  feeAmount: number
  feePercent: number
  impliedPrice: number
  slippagePercent: number
  poolFiatReserve: number
  poolGoldReserve: number
  availableInputBalance: number
}

/** Result of an executed gold AMM swap. */
export interface GoldAmmSwapResult {
  tradeId: string
  direction: string
  currencyCode: string
  inputAmount: number
  outputAmount: number
  feeAmount: number
  impliedPrice: number
  newFiatBalance: number
  newGoldBalance: number
}

/** Result of creating a pool or adding liquidity. */
export interface GoldAmmLiquidityResult {
  poolId: string
  positionId: string
  currencyCode: string
  liquidityShares: number
  fiatProvided: number
  goldProvided: number
  poolFiatReserve: number
  poolGoldReserve: number
  newFiatBalance: number
  newGoldBalance: number
}

/** Result of removing liquidity from a gold AMM pool. */
export interface GoldAmmRemoveLiquidityResult {
  positionId: string
  currencyCode: string
  fiatReturned: number
  goldReturned: number
  remainingShares: number
  newFiatBalance: number
  newGoldBalance: number
}

/** Player's gold (XAU) balance. */
export interface GoldBalanceInfo {
  balance: number
  blockedInPools: number
  availableBalance: number
}

/**
 * EUR-based FX rate entry returned by the `eurFxRates` query.
 * `rate` = units of this currency per 1 EUR (e.g. 25.20 for CZK, 1.08 for USD).
 */
export interface EurFxRate {
  currencyCode: string
  rate: number
  /** Mid-market rate (same as rate). */
  midRate?: number
  /** Buy rate (ask): slightly worse for the buyer than mid rate. */
  buyRate?: number
  /** Sell rate (bid): slightly better for the seller than mid rate. */
  sellRate?: number
}

/**
 * A single historical FX rate snapshot captured at one game tick.
 * Used to render buy/mid/sell charting in the FX rates tab.
 */
export interface FxRateSnapshot {
  baseCurrencyCode: string
  quoteCurrencyCode: string
  midRate: number
  buyRate: number
  sellRate: number
  gameTick: number
  capturedAtUtc: string
}

// ── Bank Statement types ──────────────────────────────────────────────────────

/** A single row in a company bank statement (maps to one LedgerEntry). */
export interface BankStatementRow {
  id: string
  recordedAtTick: number
  recordedAtUtc: string
  description: string
  category: string
  /** Positive = credit (income), negative = debit (expense). */
  amount: number
  /** Running account balance after this entry. */
  runningBalance: number
  buildingId: string | null
  buildingName: string | null
}

/** Top-level result from the `bankStatement` query. */
export interface BankStatementResult {
  companyId: string
  companyName: string
  currencyCode: string
  currencySymbol: string
  currentBalance: number
  totalEntries: number
  rows: BankStatementRow[]
}

/** Bank account info for a building (from the `buildingBankAccount` query). */
export interface BuildingBankAccountInfo {
  buildingId: string
  buildingName: string
  cityName: string
  currencyCode: string
  hasBankAccount: boolean
  bankAccountId: string | null
  accountNumber: string | null
  balance: number | null
  alertMinBalanceThreshold: number | null
  isSuspendedForFunds: boolean
  /** null | 'MISSING_BANK_ACCOUNT' | 'INSUFFICIENT_FUNDS:<amount>' */
  suspendedReason: string | null
}

/** A bank account owned by a company (from the `companyBankAccounts` query). */
export interface CompanyBankAccountSummary {
  id: string
  accountNumber: string
  currencyCode: string
  balance: number
  alertMinBalanceThreshold: number | null
}

/** A bank account owned by the player or one of the player's companies. Returned by the `myBankAccounts` query. */
export interface PlayerBankAccountSummary {
  id: string
  accountNumber: string
  currencyCode: string
  currencySymbol: string
  balance: number
  alertMinBalanceThreshold: number | null
  companyId: string | null
  companyName: string | null
  ownerType: 'PERSON' | 'COMPANY'
  ownerDisplayName: string
  bankBuildingId: string | null
  cityId: string | null
  isDepositAccount: boolean
}

/** Result from the `fundBuildingBankAccount` mutation. */
export interface FundBuildingBankAccountResult {
  bankAccount: BuildingBankAccountInfo
  remainingCompanyCash: number
}

/** Result from the `assignBuildingBankAccount` mutation. */
export interface AssignBuildingBankAccountResult {
  bankAccount: BuildingBankAccountInfo
}

/** Result from the `createCompanyBankAccount` mutation. */
export interface CreateCompanyBankAccountResult {
  account: CompanyBankAccountSummary
}

/** Result from the `transferFunds` mutation. */
export interface TransferFundsResult {
  amount: number
  currencyCode: string
  fromAccount: PlayerBankAccountSummary
  toAccount: PlayerBankAccountSummary
}
