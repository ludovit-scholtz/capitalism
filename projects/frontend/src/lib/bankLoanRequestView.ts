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

export const MY_COLLATERAL_BUILDINGS_QUERY = `
  query MyCollateralBuildings($bankBuildingId: UUID) {
    myCollateralBuildings(bankBuildingId: $bankBuildingId) {
      buildingId
      buildingName
      buildingType
      level
      appraisedValue
      maxBorrowable
      existingSecuredExposure
      remainingBorrowingCapacity
      currencyCode
      isEligible
      ineligibilityReason
    }
  }
`

export const MY_COLLATERAL_BUILDINGS_LEGACY_QUERY = `
  {
    myCollateralBuildings {
      buildingId
      buildingName
      buildingType
      level
      appraisedValue
      maxBorrowable
      existingSecuredExposure
      remainingBorrowingCapacity
      currencyCode
      isEligible
      ineligibilityReason
    }
  }
`

export const COMPANY_BANK_ACCOUNTS_QUERY = `
  query CompanyBankAccounts($companyId: UUID!) {
    companyBankAccounts(companyId: $companyId) {
      id
      accountNumber
      currencyCode
      balance
    }
  }
`

export const ACCEPT_LOAN_MUTATION = `
  mutation AcceptLoan($input: AcceptLoanInput!) {
    acceptLoan(input: $input) {
      id
      status
      originalPrincipal
    }
  }
`
