import { gqlRequest } from './graphql'

export interface GameServerSummary {
  id: string
  serverKey: string
  displayName: string
  description: string
  region: string
  environment: string
  backendUrl: string
  graphqlUrl: string
  frontendUrl: string
  version: string
  playerCount: number
  companyCount: number
  currentTick: number
  registeredAtUtc: string
  lastHeartbeatAtUtc: string
  isOnline: boolean
}

export interface MasterPlayerProfile {
  id: string
  email: string
  displayName: string
  personalAccountName?: string
  gender?: 'MALE' | 'FEMALE' | 'UNSPECIFIED'
  preferredLocale?: 'en' | 'sk' | 'de'
  createdAtUtc: string
  startupPackClaimedAtUtc: string | null
  canClaimStartupPack: boolean
  hasReferralDiscount: boolean
  deletionRequestedAtUtc?: string | null
  deletionScheduledAtUtc?: string | null
  isPendingDeletion?: boolean
  weeklyReportEmailSubscribed?: boolean
}

export interface AccountDeletionStatus {
  isPendingDeletion: boolean
  deletionRequestedAtUtc: string | null
  deletionScheduledAtUtc: string | null
}

export type LegalDocumentKind = 'TERMS' | 'PRIVACY'

export interface LegalDocumentSection {
  heading: string
  paragraphs: string[]
}

export interface LegalDocument {
  kind: LegalDocumentKind
  locale: string
  title: string
  version: string
  effectiveDate: string
  intro: string
  sections: LegalDocumentSection[]
}

export interface MasterAuthPayload {
  token: string
  expiresAtUtc: string
  player: MasterPlayerProfile
}

export interface SubscriptionInfo {
  tier: 'FREE' | 'PRO'
  status: 'NONE' | 'ACTIVE' | 'EXPIRED'
  isActive: boolean
  daysRemaining: number | null
  canProlong: boolean
  expiresAtUtc: string | null
  startsAtUtc: string | null
}

interface GameServersPayload {
  gameServers: GameServerSummary[]
}

const GAME_SERVERS_QUERY = `
  query GetGameServers {
    gameServers {
      id
      serverKey
      displayName
      description
      region
      environment
      backendUrl
      graphqlUrl
      frontendUrl
      version
      playerCount
      companyCount
      currentTick
      registeredAtUtc
      lastHeartbeatAtUtc
      isOnline
    }
  }
`

const REGISTER_MUTATION = `
  mutation Register($input: RegisterInput!) {
    register(input: $input) {
      token
      expiresAtUtc
      player {
        id
        email
        displayName
        personalAccountName
        gender
        preferredLocale
        createdAtUtc
        startupPackClaimedAtUtc
        canClaimStartupPack
        hasReferralDiscount
        deletionRequestedAtUtc
        deletionScheduledAtUtc
        isPendingDeletion
        weeklyReportEmailSubscribed
      }
    }
  }
`

const LOGIN_MUTATION = `
  mutation Login($input: LoginInput!) {
    login(input: $input) {
      token
      expiresAtUtc
      player {
        id
        email
        displayName
        personalAccountName
        gender
        preferredLocale
        createdAtUtc
        startupPackClaimedAtUtc
        canClaimStartupPack
        hasReferralDiscount
        deletionRequestedAtUtc
        deletionScheduledAtUtc
        isPendingDeletion
        weeklyReportEmailSubscribed
      }
    }
  }
`

const ME_QUERY = `
  query {
    me {
      id
      email
      displayName
      personalAccountName
      gender
      preferredLocale
      createdAtUtc
      startupPackClaimedAtUtc
      canClaimStartupPack
      hasReferralDiscount
      deletionRequestedAtUtc
      deletionScheduledAtUtc
      isPendingDeletion
      weeklyReportEmailSubscribed
    }
  }
`

const LEGAL_DOCUMENTS_QUERY = `
  query LegalDocuments($locale: String) {
    legalDocuments(locale: $locale) {
      kind
      locale
      title
      version
      effectiveDate
      intro
      sections {
        heading
        paragraphs
      }
    }
  }
`

const MY_SUBSCRIPTION_QUERY = `
  query {
    mySubscription {
      tier
      status
      isActive
      daysRemaining
      canProlong
      expiresAtUtc
      startsAtUtc
    }
  }
`

const PROLONG_SUBSCRIPTION_MUTATION = `
  mutation ProlongSubscription($input: ProlongSubscriptionInput!) {
    prolongSubscription(input: $input) {
      tier
      status
      isActive
      daysRemaining
      canProlong
      expiresAtUtc
      startsAtUtc
    }
  }
`

const CLAIM_STARTUP_PACK_MUTATION = `
  mutation ClaimStartupPack {
    claimStartupPack {
      tier
      status
      isActive
      daysRemaining
      canProlong
      expiresAtUtc
      startsAtUtc
    }
  }
`

const UPDATE_PERSONAL_ACCOUNT_NAME_MUTATION = `
  mutation UpdatePersonalAccountName($input: UpdatePersonalAccountNameInput!) {
    updatePersonalAccountName(input: $input) {
      personalAccountName
      gender
    }
  }
`

const REQUEST_ACCOUNT_DELETION_MUTATION = `
  mutation RequestAccountDeletion($input: RequestAccountDeletionInput!) {
    requestAccountDeletion(input: $input) {
      isPendingDeletion
      deletionRequestedAtUtc
      deletionScheduledAtUtc
    }
  }
`

const CANCEL_ACCOUNT_DELETION_MUTATION = `
  mutation CancelAccountDeletion {
    cancelAccountDeletion {
      isPendingDeletion
      deletionRequestedAtUtc
      deletionScheduledAtUtc
    }
  }
`

const SET_WEEKLY_REPORT_EMAIL_SUBSCRIPTION_MUTATION = `
  mutation SetWeeklyReportEmailSubscription($subscribed: Boolean!) {
    setWeeklyReportEmailSubscription(subscribed: $subscribed)
  }
`

const UNSUBSCRIBE_FROM_WEEKLY_REPORT_EMAIL_MUTATION = `
  mutation UnsubscribeFromWeeklyReportEmail($token: UUID!) {
    unsubscribeFromWeeklyReportEmail(token: $token)
  }
`

export async function fetchGameServers(): Promise<GameServerSummary[]> {
  const data = await gqlRequest<GameServersPayload>(GAME_SERVERS_QUERY)
  return data.gameServers
}

export async function fetchLegalDocuments(locale?: string): Promise<LegalDocument[]> {
  const data = await gqlRequest<{ legalDocuments: LegalDocument[] }>(LEGAL_DOCUMENTS_QUERY, {
    locale: locale ?? null,
  })
  return data.legalDocuments
}

export async function registerAccount(
  email: string,
  displayName: string,
  password: string,
  locale?: string,
  currentUrl?: string,
): Promise<MasterAuthPayload> {
  const data = await gqlRequest<{ register: MasterAuthPayload }>(REGISTER_MUTATION, {
    input: { email, displayName, password, locale, currentUrl },
  })
  return data.register
}

export async function loginAccount(
  email: string,
  password: string,
  locale?: string,
  currentUrl?: string,
): Promise<MasterAuthPayload> {
  const data = await gqlRequest<{ login: MasterAuthPayload }>(LOGIN_MUTATION, {
    input: { email, password, locale, currentUrl },
  })
  return data.login
}

export async function fetchMe(token: string): Promise<MasterPlayerProfile> {
  const data = await gqlRequest<{ me: MasterPlayerProfile }>(ME_QUERY, undefined, token)
  return data.me
}

export async function fetchMySubscription(token: string): Promise<SubscriptionInfo> {
  const data = await gqlRequest<{ mySubscription: SubscriptionInfo }>(
    MY_SUBSCRIPTION_QUERY,
    undefined,
    token,
  )
  return data.mySubscription
}

export async function prolongSubscription(
  token: string,
  months: number,
): Promise<SubscriptionInfo> {
  const data = await gqlRequest<{ prolongSubscription: SubscriptionInfo }>(
    PROLONG_SUBSCRIPTION_MUTATION,
    { input: { months } },
    token,
  )
  return data.prolongSubscription
}

export async function claimStartupPack(token: string): Promise<SubscriptionInfo> {
  const data = await gqlRequest<{ claimStartupPack: SubscriptionInfo }>(
    CLAIM_STARTUP_PACK_MUTATION,
    undefined,
    token,
  )
  return data.claimStartupPack
}

export async function updatePersonalAccountName(
  token: string,
  personalAccountName: string,
  gender?: 'MALE' | 'FEMALE' | 'UNSPECIFIED',
): Promise<string> {
  const data = await gqlRequest<{
    updatePersonalAccountName: {
      personalAccountName: string
      gender: string
    }
  }>(
    UPDATE_PERSONAL_ACCOUNT_NAME_MUTATION,
    {
      input: {
        personalAccountName,
        gender,
      },
    },
    token,
  )

  return data.updatePersonalAccountName.personalAccountName
}

export async function requestAccountDeletion(
  token: string,
  confirmationEmail: string,
): Promise<AccountDeletionStatus> {
  const data = await gqlRequest<{ requestAccountDeletion: AccountDeletionStatus }>(
    REQUEST_ACCOUNT_DELETION_MUTATION,
    { input: { confirmationEmail } },
    token,
  )
  return data.requestAccountDeletion
}

export async function cancelAccountDeletion(token: string): Promise<AccountDeletionStatus> {
  const data = await gqlRequest<{ cancelAccountDeletion: AccountDeletionStatus }>(
    CANCEL_ACCOUNT_DELETION_MUTATION,
    undefined,
    token,
  )
  return data.cancelAccountDeletion
}

export async function setWeeklyReportEmailSubscription(
  token: string,
  subscribed: boolean,
): Promise<boolean> {
  const data = await gqlRequest<{ setWeeklyReportEmailSubscription: boolean }>(
    SET_WEEKLY_REPORT_EMAIL_SUBSCRIPTION_MUTATION,
    { subscribed },
    token,
  )
  return data.setWeeklyReportEmailSubscription
}

export async function unsubscribeFromWeeklyReportEmail(unsubscribeToken: string): Promise<boolean> {
  const data = await gqlRequest<{ unsubscribeFromWeeklyReportEmail: boolean }>(
    UNSUBSCRIBE_FROM_WEEKLY_REPORT_EMAIL_MUTATION,
    { token: unsubscribeToken },
  )
  return data.unsubscribeFromWeeklyReportEmail
}

export * from './masterApi/gold'
export * from './masterApi/goldAdmin'
export * from './masterApi/support'
export * from './masterApi/rankingTypes'
export * from './masterApi/ranking'
export * from './masterApi/apiKeys'
