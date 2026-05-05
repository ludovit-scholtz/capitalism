import type { User } from './auth'

/** Matches backend CatalogEvent entity */
export interface CatalogEvent {
  id: string
  name: string
  slug: string
  description: string
  startDate: string
  endDate: string | null
  startsAtUtc: string
  endsAtUtc: string | null
  venueName: string | null
  addressLine1: string | null
  city: string | null
  countryCode: string | null
  latitude: number | null
  longitude: number | null
  mapUrl: string | null
  attendanceMode: 'IN_PERSON' | 'ONLINE' | 'HYBRID'
  isFree: boolean
  currencyCode: string
  price: number | null
  eventUrl: string | null
  timezone: string | null
  submittedBy: User
  submittedAtUtc: string
  status: 'PUBLISHED' | 'PENDING_APPROVAL' | 'REJECTED' | 'DRAFT'
  interestedCount: number
  domain: EventDomain | null
}

/** Matches backend EventDomain entity */
export interface EventDomain {
  id: string
  name: string
  slug: string
  subdomain: string
  isActive: boolean
  description: string | null
  logoUrl: string | null
  bannerUrl: string | null
  primaryColor: string | null
  accentColor: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

/** Event filters for discovery */
export interface EventFilters {
  keyword?: string
  search?: string
  location?: string
  mode?: 'IN_PERSON' | 'ONLINE' | 'HYBRID'
  price?: 'free' | 'paid'
  priceType?: 'ALL' | 'FREE' | 'PAID'
  priceMin?: string
  priceMax?: string
  date?: 'upcoming' | 'past'
  dateFrom?: string
  dateTo?: string
  sort?: 'newest' | 'oldest' | 'name' | 'RELEVANCE'
  sortBy?: 'UPCOMING' | 'NEWEST' | 'RELEVANCE'
  domain?: string
  attendanceMode?: '' | 'IN_PERSON' | 'ONLINE' | 'HYBRID'
  language?: string
  timezone?: string
}

export interface SavedSearch {
  id: string
  name: string
  searchText: string | null
  domainSlug: string | null
  locationText: string | null
  startsFromUtc: string | null
  startsToUtc: string | null
  isFree: boolean | null
  priceMin: number | null
  priceMax: number | null
  sortBy: 'UPCOMING' | 'NEWEST' | 'RELEVANCE'
  attendanceMode: 'IN_PERSON' | 'ONLINE' | 'HYBRID' | null
  language: string | null
  timezone: string | null
  createdAtUtc: string
  updatedAtUtc: string
}
