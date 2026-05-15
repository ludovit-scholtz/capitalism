export interface GlobalExchangeOffer {
  cityId: string
  cityName: string
  resourceTypeId: string
  resourceName: string
  resourceSlug: string
  unitSymbol: string
  localAbundance: number
  exchangePricePerUnit: number
  /** Typical (central) quality for this city/resource abundance level. */
  estimatedQuality: number
  /**
   * Minimum quality in the variability band. Actual purchase quality varies
   * between qualityMin and qualityMax each tick.
   */
  qualityMin: number
  /**
   * Maximum quality in the variability band. Actual purchase quality varies
   * between qualityMin and qualityMax each tick.
   */
  qualityMax: number
  transitCostPerUnit: number
  deliveredPricePerUnit: number
  distanceKm: number
  /** Destination city fuel price index (1.0 = EUR baseline). */
  fuelPriceIndex?: number
  /** Last 50 ticks of ask-price history for sparkline rendering. */
  askPriceHistory?: ResourceAskPricePoint[]
}

export interface ResourceAskPricePoint {
  tick: number
  askPricePerUnit: number
}

/** A product marketplace listing from a player-placed SELL exchange order. */
export interface GlobalExchangeProductListing {
  orderId: string
  productTypeId: string
  productName: string
  productSlug: string
  productIndustry: string
  unitSymbol: string
  unitName: string
  basePrice: number
  pricePerUnit: number
  remainingQuantity: number
  sellerCityId: string
  sellerCityName: string
  sellerCompanyId: string
  sellerCompanyName: string
  createdAtUtc: string
}

export interface GlobalExchangeProductQuote {
  productTypeId: string
  productName: string
  productSlug: string
  productIndustry: string
  unitSymbol: string
  basePrice: number
  bidPricePerUnit: number
  offerPricePerUnit: number
  estimatedQuality: number
}

export interface InGameChatMessage {
  id: string
  authorPlayerId: string
  authorDisplayName: string
  cityId: string | null
  content: string
  createdAtUtc: string
  isVisible: boolean
  isRemovedForViewer: boolean
  isOwnMessage: boolean
}
