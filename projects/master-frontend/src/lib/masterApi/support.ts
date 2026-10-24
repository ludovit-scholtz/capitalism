import { gqlRequest } from '../graphql'

export interface SupportTicketAuditEventInfo {
  id: string
  eventType: string
  actorEmail: string
  actorDisplayName: string
  note: string
  metadataJson: string
  createdAtUtc: string
}

export interface SupportTicketInfo {
  id: string
  ticketType: 'SUGGESTION' | 'BUG' | 'OTHER'
  status: 'SUBMITTED' | 'IN_PROGRESS' | 'FINISHED'
  title: string
  markdownSource: string
  sanitizedPreviewHtml: string | null
  containsUnsafeContent: boolean
  moderationState: 'PENDING' | 'APPROVED' | 'REJECTED'
  moderationReason: string | null
  moderatedByEmail: string | null
  moderatedAtUtc: string | null
  createdByEmail: string
  createdByDisplayName: string
  createdAtUtc: string
  updatedAtUtc: string
  statusUpdatedAtUtc: string
  extractedUrls: string[]
  extractedImages: string[]
  activity: SupportTicketAuditEventInfo[]
}

export interface SupportTicketListInput {
  ticketType?: string | null
  status?: string | null
  searchTitle?: string | null
  createdFromUtc?: string | null
  createdToUtc?: string | null
  sortBy?: 'CREATED_AT' | 'UPDATED_AT' | 'TITLE'
  sortDirection?: 'ASC' | 'DESC'
  limit?: number
  offset?: number
  unsafeOnly?: boolean
}

const SUPPORT_FIELDS = `
  id
  ticketType
  status
  title
  markdownSource
  sanitizedPreviewHtml
  containsUnsafeContent
  moderationState
  moderationReason
  moderatedByEmail
  moderatedAtUtc
  createdByEmail
  createdByDisplayName
  createdAtUtc
  updatedAtUtc
  statusUpdatedAtUtc
  extractedUrls
  extractedImages
  activity {
    id
    eventType
    actorEmail
    actorDisplayName
    note
    metadataJson
    createdAtUtc
  }
`

const MY_SUPPORT_TICKETS_QUERY = `
  query MySupportTickets($input: ListSupportTicketsInput) {
    mySupportTickets(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

const SUPPORT_TICKETS_ADMIN_QUERY = `
  query SupportTicketsAdmin($input: ListSupportTicketsInput) {
    supportTicketsAdmin(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

const CREATE_SUPPORT_TICKET_MUTATION = `
  mutation CreateSupportTicket($input: CreateSupportTicketInput!) {
    createSupportTicket(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

const UPDATE_SUPPORT_TICKET_CONTENT_MUTATION = `
  mutation UpdateSupportTicketContent($input: UpdateSupportTicketContentInput!) {
    updateSupportTicketContent(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

const UPDATE_SUPPORT_TICKET_STATUS_MUTATION = `
  mutation UpdateSupportTicketStatus($input: UpdateSupportTicketStatusInput!) {
    updateSupportTicketStatus(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

const MODERATE_SUPPORT_TICKET_MUTATION = `
  mutation ModerateSupportTicket($input: ModerateSupportTicketInput!) {
    moderateSupportTicket(input: $input) {
      ${SUPPORT_FIELDS}
    }
  }
`

export async function fetchMySupportTickets(
  token: string,
  input: SupportTicketListInput = {},
): Promise<SupportTicketInfo[]> {
  const data = await gqlRequest<{ mySupportTickets: SupportTicketInfo[] }>(
    MY_SUPPORT_TICKETS_QUERY,
    { input },
    token,
  )
  return data.mySupportTickets
}

export async function fetchSupportTicketsAdmin(
  token: string,
  input: SupportTicketListInput = {},
): Promise<SupportTicketInfo[]> {
  const data = await gqlRequest<{ supportTicketsAdmin: SupportTicketInfo[] }>(
    SUPPORT_TICKETS_ADMIN_QUERY,
    { input },
    token,
  )
  return data.supportTicketsAdmin
}

export async function createSupportTicket(
  token: string,
  input: { ticketType: string; title: string; markdownSource: string },
): Promise<SupportTicketInfo> {
  const data = await gqlRequest<{ createSupportTicket: SupportTicketInfo }>(
    CREATE_SUPPORT_TICKET_MUTATION,
    { input },
    token,
  )
  return data.createSupportTicket
}

export async function updateSupportTicketContent(
  token: string,
  input: { ticketId: string; title: string; markdownSource: string },
): Promise<SupportTicketInfo> {
  const data = await gqlRequest<{ updateSupportTicketContent: SupportTicketInfo }>(
    UPDATE_SUPPORT_TICKET_CONTENT_MUTATION,
    { input },
    token,
  )
  return data.updateSupportTicketContent
}

export async function updateSupportTicketStatus(
  token: string,
  input: { ticketId: string; status: string; note?: string },
): Promise<SupportTicketInfo> {
  const data = await gqlRequest<{ updateSupportTicketStatus: SupportTicketInfo }>(
    UPDATE_SUPPORT_TICKET_STATUS_MUTATION,
    { input },
    token,
  )
  return data.updateSupportTicketStatus
}

export async function moderateSupportTicket(
  token: string,
  input: { ticketId: string; approve: boolean; note?: string },
): Promise<SupportTicketInfo> {
  const data = await gqlRequest<{ moderateSupportTicket: SupportTicketInfo }>(
    MODERATE_SUPPORT_TICKET_MUTATION,
    { input },
    token,
  )
  return data.moderateSupportTicket
}
