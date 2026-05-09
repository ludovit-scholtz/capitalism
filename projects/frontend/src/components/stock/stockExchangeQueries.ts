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
      stockSymbol
      companyName
      primaryCityName
      primaryIndustry
      totalSharesIssued
      publicFloatShares
      sharePrice
      dailyChangePercent
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

export const OPEN_ORDERS_QUERY = `
  query MyOpenOrders {
    myOpenOrders {
      id
      companyId
      companyName
      stockSymbol
      side
      limitPrice
      quantity
      filledQuantity
      remainingQuantity
      status
      createdAtTick
      updatedAtTick
    }
  }
`

export const ORDER_BOOK_QUERY = `
  query GetOrderBook($stockSymbol: String!) {
    orderBook: getOrderBook(stockSymbol: $stockSymbol) {
      stockSymbol
      bids {
        price
        totalQuantity
      }
      asks {
        price
        totalQuantity
      }
    }
  }
`

export const STOCK_TRADE_HISTORY_QUERY = `
  query StockTradeHistory($stockSymbol: String!, $limit: Int!) {
    stockTradeHistory(stockSymbol: $stockSymbol, limit: $limit) {
      id
      stockSymbol
      price
      quantity
      executedAtTick
      executedAtUtc
    }
  }
`

export const PLACE_LIMIT_ORDER_MUTATION = `
  mutation PlaceLimitOrder($input: PlaceLimitOrderInput!) {
    placeLimitOrder(input: $input) {
      id
      companyId
      companyName
      stockSymbol
      side
      limitPrice
      quantity
      filledQuantity
      remainingQuantity
      status
      createdAtTick
      updatedAtTick
    }
  }
`

export const CANCEL_LIMIT_ORDER_MUTATION = `
  mutation CancelLimitOrder($orderId: UUID!) {
    cancelLimitOrder(orderId: $orderId) {
      id
      status
      remainingQuantity
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
