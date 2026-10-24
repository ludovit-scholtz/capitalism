import type { Building } from './building'

export type BuildingSaleOfferStatus = 'PENDING' | 'ACCEPTED' | 'REJECTED'

export interface BuildingSaleOffer {
  id: string
  buildingId: string
  buyerPlayerId: string
  buyerPlayer: { displayName: string; email: string }
  buyerCompanyId: string
  buyerCompany: { id: string; name: string }
  offeredPrice: number
  negotiationNote: string | null
  status: BuildingSaleOfferStatus
  createdAtUtc: string
  resolvedAtUtc: string | null
}

/** Item returned by `buildingMarket` query. */
export interface BuildingMarketListing {
  building: Building
  pendingOfferCount: number
}

/** Item returned by `myBuildingListings` query. */
export interface BuildingMarketMyListing {
  building: Building
  offers: BuildingSaleOffer[]
}

export interface AcceptBuildingOfferResult {
  building: Building
  offer: BuildingSaleOffer
}
