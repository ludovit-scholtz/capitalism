export const PERSON_ACCOUNT_QUERY = `
  query PersonAccount {
    personAccount {
      playerId
      displayName
      personalCash
      taxReserve
      availableCash
      totalNetWealth
      activeAccountType
      activeCompanyId
      shareholdings {
        companyId
        companyName
        shareCount
        ownershipRatio
        sharePrice
        marketValue
      }
      dividendPayments {
        id
        companyId
        companyName
        shareCount
        amountPerShare
        totalAmount
        gameYear
        recordedAtTick
        recordedAtUtc
        description
      }
      stockTrades {
        id
        companyId
        companyName
        direction
        shareCount
        pricePerShare
        totalValue
        recordedAtTick
        recordedAtUtc
      }
    }
  }
`

export const LISTINGS_QUERY = `
  query StockExchangeListings {
    stockExchangeListings {
      companyId
      companyName
      totalSharesIssued
      publicFloatShares
      sharePrice
      marketValue
      bidPrice
      askPrice
      dividendPayoutRatio
      playerOwnedShares
      controlledCompanyOwnedShares
      combinedControlledOwnershipRatio
      canClaimControl
      canMerge
    }
  }
`

export const MY_BANK_ACCOUNTS_QUERY = `
  query MyBankAccounts {
    myBankAccounts {
      id
      accountNumber
      currencyCode
      balance
      companyId
      companyName
      ownerType
      ownerDisplayName
    }
  }
`

export const BUY_MUTATION = `
  mutation BuyShares($input: BuySharesInput!) {
    buyShares(input: $input) {
      companyId
      companyName
      accountType
      accountCompanyId
      accountName
      shareCount
      pricePerShare
      totalValue
      ownedShareCount
      publicFloatShares
      personalCash
      personalTaxReserve
      companyCash
    }
  }
`

export const SELL_MUTATION = `
  mutation SellShares($input: SellSharesInput!) {
    sellShares(input: $input) {
      companyId
      companyName
      accountType
      accountCompanyId
      accountName
      shareCount
      pricePerShare
      totalValue
      taxReserved
      ownedShareCount
      publicFloatShares
      personalCash
      personalTaxReserve
      companyCash
    }
  }
`

export const PRICE_HISTORY_QUERY = `
  query StockExchangePriceHistory($companyId: UUID!) {
    stockExchangePriceHistory(companyId: $companyId) {
      companyId
      tick
      price
      recordedAtUtc
    }
  }
`

export const MERGE_MUTATION = `
  mutation MergeCompany($input: MergeCompanyInput!) {
    mergeCompany(input: $input) {
      destinationCompanyId
      destinationCompanyName
      absorbedCompanyName
      cashTransferred
      buildingsTransferred
    }
  }
`

export const COMPANY_SHAREHOLDERS_QUERY = `
  query CompanyShareholders($companyId: UUID!) {
    companyShareholders(companyId: $companyId) {
      companyId
      companyName
      totalSharesIssued
      publicFloatShares
      shareholderCount
      shareholders {
        holderName
        holderType
        holderPlayerId
        holderCompanyId
        shareCount
        ownershipRatio
      }
    }
  }
`
