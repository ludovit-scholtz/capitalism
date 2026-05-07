// GraphQL query/mutation strings for BankManagementView

export const BANK_LOANS_QUERY = `
  query BankLoans($bankBuildingId: UUID!) {
    bankLoans(bankBuildingId: $bankBuildingId) {
      id
      loanOfferId
      borrowerCompanyId
      borrowerCompanyName
      lenderCompanyId
      lenderCompanyName
      bankBuildingId
      bankBuildingName
      originalPrincipal
      remainingPrincipal
      annualInterestRatePercent
      durationTicks
      startTick
      dueTick
      nextPaymentTick
      paymentAmount
      paymentsMade
      totalPayments
      status
      missedPayments
      accumulatedPenalty
      defaultedAtTick
      acceptedAtUtc
      closedAtUtc
      collateralBuildingId
      collateralBuildingName
      collateralAppraisedValue
    }
  }
`

export const BANK_INFO_QUERY = `
  query BankInfo($id: UUID!) {
    bankInfo(bankBuildingId: $id) {
      bankBuildingId
      bankBuildingName
      cityId
      cityName
      cityCurrencyCode
      cityCurrencySymbol
      baseCapitalRequirement
      lenderCompanyId
      lenderCompanyName
      depositInterestRatePercent
      lendingInterestRatePercent
      totalDeposits
      lendableCapacity
      outstandingLoanPrincipal
      availableLendingCapacity
      baseCapitalDeposited
      centralBankDebt
      centralBankInterestRatePercent
      reserveRequirement
      availableCash
      reserveShortfall
      liquidityStatus
      pendingDepositInterestRatePercent
      pendingDepositRateEffectiveTick
    }
  }
`

export const BANK_DEPOSITS_QUERY = `
  query BankDeposits($id: UUID!) {
    bankDeposits(bankBuildingId: $id) {
      id
      bankBuildingId
      bankBuildingName
      depositorCompanyId
      depositorCompanyName
      amount
      depositInterestRatePercent
      isBaseCapital
      isActive
      depositedAtTick
      depositedAtUtc
      totalInterestPaid
      cityCurrencyCode
    }
  }
`

export const SET_BANK_RATES_MUTATION = `
  mutation SetBankRates($input: SetBankRatesInput!) {
    setBankRates(input: $input) {
      bankBuildingId
      cityCurrencyCode
      cityCurrencySymbol
      baseCapitalRequirement
      depositInterestRatePercent
      lendingInterestRatePercent
      totalDeposits
      lendableCapacity
      availableLendingCapacity
      baseCapitalDeposited
      centralBankDebt
      centralBankInterestRatePercent
      reserveRequirement
      availableCash
      reserveShortfall
      liquidityStatus
    }
  }
`

export const MY_COMPANIES_QUERY = `
  {
    me {
      companies {
        id
        name
        cash
        playerId
        buildings { id type }
      }
    }
  }
`

export const MY_BANK_ACCOUNTS_QUERY = `
  {
    myBankAccounts {
      id
      accountNumber
      currencyCode
      currencySymbol
      balance
      companyId
      companyName
      ownerType
      ownerDisplayName
      bankBuildingId
    }
  }
`

export const MY_DEPOSITS_QUERY = `
  {
    myDeposits {
      id
      bankBuildingId
      bankBuildingName
      depositorCompanyId
      depositorCompanyName
      amount
      depositInterestRatePercent
      isBaseCapital
      isActive
      depositedAtTick
      depositedAtUtc
      totalInterestPaid
      cityCurrencyCode
    }
  }
`

export const MY_LOANS_QUERY = `
  {
    myLoans {
      id
      bankBuildingId
      bankBuildingName
      originalPrincipal
      remainingPrincipal
      annualInterestRatePercent
      nextPaymentTick
      paymentAmount
      paymentsMade
      totalPayments
      status
      missedPayments
      accumulatedPenalty
      defaultedAtTick
      collateralBuildingId
      collateralBuildingName
      collateralAppraisedValue
    }
  }
`

export const INITIATE_BASE_DEPOSIT_MUTATION = `
  mutation InitiateBaseDeposit($bankBuildingId: UUID!) {
    initiateBaseDeposit(bankBuildingId: $bankBuildingId) {
      bankBuildingId
      bankBuildingName
      cityCurrencyCode
      cityCurrencySymbol
      baseCapitalRequirement
      depositInterestRatePercent
      lendingInterestRatePercent
      totalDeposits
      lendableCapacity
      outstandingLoanPrincipal
      availableLendingCapacity
      baseCapitalDeposited
      centralBankDebt
      centralBankInterestRatePercent
      reserveRequirement
      availableCash
      reserveShortfall
      liquidityStatus
    }
  }
`

export const UPDATE_BANK_DEPOSIT_RATE_MUTATION = `
  mutation UpdateBankDepositRate($input: UpdateBankDepositRateInput!) {
    updateBankDepositRate(input: $input) {
      bankBuildingId
      depositInterestRatePercent
      pendingDepositInterestRatePercent
      pendingDepositRateEffectiveTick
      liquidityStatus
    }
  }
`

export const BANK_DEPOSIT_RATE_HISTORY_QUERY = `
  query BankDepositRateHistory($bankBuildingId: UUID!) {
    bankDepositRateHistory(bankBuildingId: $bankBuildingId) {
      id
      bankBuildingId
      previousRatePercent
      newRatePercent
      effectiveTick
      effectiveUtc
      scheduledAtTick
      scheduledAtUtc
      affectedDepositCount
      isApplied
      changedByPlayerName
    }
  }
`
