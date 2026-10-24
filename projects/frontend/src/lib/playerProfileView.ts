import { resolveApiBaseUrl, resolveGameGraphqlUrl } from '@/lib/runtimeGraphqlUrl'

export const API_BASE_URL = resolveApiBaseUrl(resolveGameGraphqlUrl(import.meta.env.VITE_GRAPHQL_URL))

export const passwordAuthEnabled = import.meta.env.VITE_AUTH_PASSWORD_ENABLED === 'true'

export const PLAYER_PROFILE_QUERY = `
  query GetPlayerProfile($playerId: UUID!) {
    playerProfile(playerId: $playerId) {
      playerId
      displayName
      gender
      bio
      createdAtUtc
      joinGameYear
      hasProSubscription
      totalWealthUsd
      totalCompanyEquityUsd
      companyCount
      leaderboardRank
      activeBuildingTypes
      citiesWithBuildings
      totalProductsSold
      hallOfFame {
        highestSingleTickRevenue
        highestSingleTickRevenueTick
        largestBuildingAcquisitionPrice
        largestBuildingAcquisitionName
        highestBrandQuality
        highestBrandQualityName
        accountAgeTicks
      }
    }
  }
`

export const UPDATE_BIO_MUTATION = `
  mutation UpdatePlayerBio($bio: String) {
    updatePlayerBio(input: { bio: $bio }) {
      playerId
      bio
    }
  }
`

export const UPDATE_DISPLAY_NAME_MUTATION = `
  mutation UpdateDisplayName($displayName: String!, $gender: String) {
    updateDisplayName(input: { displayName: $displayName, gender: $gender }) {
      playerId
      displayName
      gender
    }
  }
`

export const UPDATE_PERSONAL_ACCOUNT_NAME_MASTER_MUTATION = `
  mutation UpdatePersonalAccountName($input: UpdatePersonalAccountNameInput!) {
    updatePersonalAccountName(input: $input) {
      playerId
      personalAccountName
      gender
    }
  }
`

export function rankBadge(rank: number): string {
  if (rank === 1) return '🥇'
  if (rank === 2) return '🥈'
  if (rank === 3) return '🥉'
  return `#${rank}`
}

export function copyProfileUrl() {
  const url = window.location.href
  navigator.clipboard.writeText(url).catch(() => {
    // Fallback: no-op
  })
}
