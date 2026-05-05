export interface MarketCity {
  id: string
  name: string
  currencyCode: string
  countryCode?: string
}

export interface MarketCompany {
  id: string
  name: string
  player?: { displayName: string }
}

export interface MarketBuilding {
  id: string
  name: string
  type: string
  level: number
  isForSale: boolean
  askingPrice: number | null
  city: MarketCity
  company: MarketCompany
}

export interface MarketOffer {
  id: string
  offeredPrice: number
  status: 'PENDING' | 'ACCEPTED' | 'REJECTED'
  negotiationNote: string | null
  createdAtUtc: string
  resolvedAtUtc: string | null
  buyerPlayer: { displayName: string }
  buyerCompany: { id: string; name: string }
}

export interface BuildingMarketListing {
  building: MarketBuilding
  pendingOfferCount: number
}

export interface BuildingMarketMyListing {
  building: MarketBuilding
  offers: MarketOffer[]
}

export interface FilterCity {
  id: string
  name: string
}
