import type { GlobalExchangeOffer, GlobalExchangeProductListing, GlobalExchangeProductQuote } from '@/types'

export interface City {
  id: string
  name: string
  countryCode: string
  currencyCode: string
  latitude: number
  longitude: number
}

export interface ExchangeRow {
  resourceId: string
  resourceName: string
  resourceSlug: string
  unitSymbol: string
  category: string
  offers: GlobalExchangeOffer[]
  bestDeliveredPrice: number
  bestCityId: string
}

export interface ProductRow {
  productId: string
  productName: string
  productSlug: string
  productIndustry: string
  unitSymbol: string
  basePrice: number
  marketQuote: GlobalExchangeProductQuote
  listings: GlobalExchangeProductListing[]
  bestPrice: number
}

export type MarketMode = 'resources' | 'products'

export const CITIES_QUERY = `
  {
    cities {
      id
      name
      countryCode
      currencyCode
      latitude
      longitude
    }
  }
`

export const RESOURCES_QUERY = `
  {
    resourceTypes {
      id
      name
      slug
      category
      basePrice
      weightPerUnit
      unitName
      unitSymbol
    }
  }
`

export const PRODUCTS_QUERY = `
  {
    productTypes {
      id
      name
      slug
      industry
      basePrice
      unitName
      unitSymbol
      isProOnly
    }
  }
`

export const EXCHANGE_QUERY = `
  query GlobalExchangeOffers($destinationCityId: UUID!) {
    globalExchangeOffers(destinationCityId: $destinationCityId) {
      cityId
      cityName
      resourceTypeId
      resourceName
      resourceSlug
      unitSymbol
      localAbundance
      exchangePricePerUnit
      estimatedQuality
      qualityMin
      qualityMax
      transitCostPerUnit
      deliveredPricePerUnit
      distanceKm
      fuelPriceIndex
      askPriceHistory {
        tick
        askPricePerUnit
      }
    }
  }
`

export const PRODUCT_LISTINGS_QUERY = `
  query GlobalExchangeProductListings($productTypeId: UUID) {
    globalExchangeProductListings(productTypeId: $productTypeId) {
      orderId
      productTypeId
      productName
      productSlug
      productIndustry
      unitSymbol
      unitName
      basePrice
      pricePerUnit
      remainingQuantity
      sellerCityId
      sellerCityName
      sellerCompanyId
      sellerCompanyName
      createdAtUtc
    }
  }
`

export function formatPercent(value: number): string {
  return `${Math.round(value * 100)}%`
}

export function priceVsBase(pricePerUnit: number, basePrice: number): string {
  if (basePrice <= 0) return ''
  const diff = pricePerUnit - basePrice
  const pct = Math.round((diff / basePrice) * 100)
  return pct >= 0 ? `+${pct}%` : `${pct}%`
}

export function priceVsBaseClass(pricePerUnit: number, basePrice: number): string {
  if (basePrice <= 0) return ''
  const diff = pricePerUnit - basePrice
  return diff > 0 ? 'price-above-base' : diff < 0 ? 'price-below-base' : 'price-at-base'
}
