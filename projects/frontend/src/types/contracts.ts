export interface GovernmentContractCard {
  id: string
  cityId: string
  cityName: string
  currencyCode: string
  title: string
  description: string
  productTypeId: string
  productName: string
  quantityRequired: number
  minimumQuality: number
  budgetCap: number
  deadlineTick: number
  status: 'OPEN' | 'AWARDED' | 'FULFILLED' | 'EXPIRED'
  winnerCompanyId: string | null
  winnerCompanyName: string | null
  createdAtTick: number
  bidCount: number
  awardedBidPricePerUnit: number | null
  fulfilledQuantity: number | null
  fulfillmentPercent: number | null
}

export interface GovernmentContractEligibility {
  isEligible: boolean
  reasonCode: string | null
  reasonMessage: string | null
  currentQualityLevel: number
}

export interface GovernmentContractDetailResult {
  contract: GovernmentContractCard
  competingBidCount: number
  eligibility: GovernmentContractEligibility | null
}

export interface ContractBidResult {
  id: string
  contractId: string
  companyId: string
  companyName: string
  bidPricePerUnit: number
  estimatedDeliveryTick: number
  submittedAtTick: number
  contractStatus: 'OPEN' | 'AWARDED' | 'FULFILLED' | 'EXPIRED'
}

export interface ContractFulfillmentResult {
  contractId: string
  status: 'OPEN' | 'AWARDED' | 'FULFILLED' | 'EXPIRED'
  quantityDelivered: number
  quantityRequired: number
  fulfillmentPercent: number
  settledRevenue: number | null
  latePenaltyApplied: boolean
}
