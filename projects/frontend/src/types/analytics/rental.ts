export interface RentalTickSnapshot {
  tick: number
  revenue: number
  occupancyPercent: number
  rentPerSqm: number
}

export interface ApartmentBuildingDetail {
  buildingId: string
  occupancyPercent: number
  totalAreaSqm: number
  rentPerSqm: number | null
  cityAverageRentPerSqm: number
  currencyCode: string
  revenueHistory: RentalTickSnapshot[]
}
