import type { PersonAccount, StockExchangeListing, PersonTradeRecord } from '@/types'

export function stockSymbolForListing(listing: StockExchangeListing): string {
  return listing.stockSymbol?.trim().length ? listing.stockSymbol : `CMP-${listing.companyId.replace(/-/g, '').toUpperCase()}`
}

export function findStockListingByCompanyId(listings: StockExchangeListing[], companyId: string): StockExchangeListing | null {
  return listings.find((listing) => listing.companyId === companyId) ?? null
}

export type StockPositionSummary = {
  sharesOwned: number
  ownershipRatio: number
  averageBuyPrice: number | null
  marketValue: number
  unrealizedPnl: number | null
}

function sortTradesByTick(trades: PersonTradeRecord[]): PersonTradeRecord[] {
  return [...trades].sort((a, b) => {
    if (a.recordedAtTick !== b.recordedAtTick) {
      return a.recordedAtTick - b.recordedAtTick
    }

    return a.recordedAtUtc.localeCompare(b.recordedAtUtc)
  })
}

export function computeStockPositionSummary(personAccount: PersonAccount | null, companyId: string, currentSharePrice: number): StockPositionSummary {
  if (!personAccount) {
    return {
      sharesOwned: 0,
      ownershipRatio: 0,
      averageBuyPrice: null,
      marketValue: 0,
      unrealizedPnl: null,
    }
  }

  const holding = personAccount.shareholdings.find((item) => item.companyId === companyId) ?? null
  const sharesOwned = holding?.shareCount ?? 0
  const ownershipRatio = holding?.ownershipRatio ?? 0
  const marketValue = sharesOwned * currentSharePrice

  if (sharesOwned <= 0) {
    return {
      sharesOwned,
      ownershipRatio,
      averageBuyPrice: null,
      marketValue,
      unrealizedPnl: null,
    }
  }

  let trackedShares = 0
  let trackedCost = 0
  const trades = sortTradesByTick(personAccount.stockTrades.filter((trade) => trade.companyId === companyId))

  for (const trade of trades) {
    if (trade.direction === 'BUY') {
      trackedShares += trade.shareCount
      trackedCost += trade.shareCount * trade.pricePerShare
      continue
    }

    if (trade.shareCount <= 0 || trackedShares <= 0) {
      continue
    }

    const soldShares = Math.min(trade.shareCount, trackedShares)
    const averageTrackedCost = trackedCost / trackedShares
    trackedCost -= averageTrackedCost * soldShares
    trackedShares -= soldShares
  }

  const effectiveAverageBuyPrice = trackedShares > 0 ? trackedCost / trackedShares : null
  const averageBuyPrice = effectiveAverageBuyPrice ?? holding?.sharePrice ?? null
  const unrealizedPnl = averageBuyPrice === null ? null : (currentSharePrice - averageBuyPrice) * sharesOwned

  return {
    sharesOwned,
    ownershipRatio,
    averageBuyPrice,
    marketValue,
    unrealizedPnl,
  }
}
