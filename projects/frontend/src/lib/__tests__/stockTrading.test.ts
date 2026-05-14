import { computeStockPositionSummary, findStockListingByCompanyId, stockSymbolForListing } from '@/lib/stockTrading'
import type { PersonAccount, StockExchangeListing } from '@/types'
import { describe, expect, it } from 'vitest'

function makeListing(overrides?: Partial<StockExchangeListing>): StockExchangeListing {
  return {
    companyId: 'company-a',
    stockSymbol: 'CMPA',
    companyName: 'Company A',
    primaryCityName: 'Bratislava',
    primaryIndustry: 'FURNITURE',
    totalSharesIssued: 10000,
    publicFloatShares: 5000,
    sharePrice: 120,
    dailyChangePercent: 0,
    marketValue: 1200000,
    bidPrice: 118.8,
    askPrice: 121.2,
    dividendPayoutRatio: 0.2,
    playerOwnedShares: 0,
    controlledCompanyOwnedShares: 0,
    combinedControlledOwnershipRatio: 0,
    canProposeDividend: false,
    canClaimControl: false,
    canMerge: false,
    ...overrides,
  }
}

function makePersonAccount(): PersonAccount {
  return {
    playerId: 'player-1',
    displayName: 'Player',
    personalCash: 1000,
    taxReserve: 0,
    availableCash: 1000,
    totalNetWealth: 2000,
    activeAccountType: 'PERSON',
    activeCompanyId: null,
    shareholdings: [],
    interestPayments: [],
    dividendPayments: [],
    stockTrades: [],
  }
}

describe('stockTrading', () => {
  it('finds listing by route companyId', () => {
    const listings = [makeListing(), makeListing({ companyId: 'company-b', companyName: 'Company B' })]
    expect(findStockListingByCompanyId(listings, 'company-b')?.companyName).toBe('Company B')
    expect(findStockListingByCompanyId(listings, 'missing')).toBeNull()
  })

  it('uses fallback stock symbol when listing symbol is empty', () => {
    const listing = makeListing({ companyId: 'company-abc-123', stockSymbol: '' })
    expect(stockSymbolForListing(listing)).toBe('CMP-COMPANYABC123')
  })

  it('computes average buy price and unrealized pnl from trade history', () => {
    const account = makePersonAccount()
    account.shareholdings = [{ companyId: 'company-a', companyName: 'Company A', shareCount: 60, ownershipRatio: 0.006, sharePrice: 120, marketValue: 7200 }]
    account.stockTrades = [
      { id: 't1', companyId: 'company-a', companyName: 'Company A', direction: 'BUY', shareCount: 100, pricePerShare: 100, totalValue: 10000, recordedAtTick: 1, recordedAtUtc: '2026-01-01T00:00:00Z' },
      { id: 't2', companyId: 'company-a', companyName: 'Company A', direction: 'SELL', shareCount: 40, pricePerShare: 110, totalValue: 4400, recordedAtTick: 2, recordedAtUtc: '2026-01-01T01:00:00Z' },
    ]

    const result = computeStockPositionSummary(account, 'company-a', 120)
    expect(result.sharesOwned).toBe(60)
    expect(result.averageBuyPrice).toBeCloseTo(100, 6)
    expect(result.unrealizedPnl).toBeCloseTo(1200, 6)
  })

  it('returns null average and pnl when player owns no shares', () => {
    const account = makePersonAccount()
    const result = computeStockPositionSummary(account, 'company-a', 120)
    expect(result.sharesOwned).toBe(0)
    expect(result.averageBuyPrice).toBeNull()
    expect(result.unrealizedPnl).toBeNull()
  })
})
