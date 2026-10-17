export interface ObjectDenialCount {
  key: string
  count: number
}

export interface ObjectAccessDenial {
  occurredAtUtc: string
  actorUserId: string
  apiKeyId: string | null
  objectType: string
  objectId: string
  reason: string
  isProbeSignal: boolean
}

export interface ObjectAccessDenials {
  totalRecorded: number
  windowSize: number
  probeSignals: number
  byObjectType: ObjectDenialCount[]
  byReason: ObjectDenialCount[]
  recent: ObjectAccessDenial[]
}

export const OBJECT_ACCESS_DENIALS_QUERY = `query ObjectAccessDenials($limit: Int) {
  objectAccessDenials(limit: $limit) {
    totalRecorded
    windowSize
    probeSignals
    byObjectType { key count }
    byReason { key count }
    recent {
      occurredAtUtc
      actorUserId
      apiKeyId
      objectType
      objectId
      reason
      isProbeSignal
    }
  }
}`

const KNOWN_REASONS = new Set(['not_found', 'not_owned', 'wrong_account_context', 'not_available'])

/** i18n key for a denial reason; unknown (future) reasons fall back to a generic label instead of showing the raw code. */
export function denialReasonLabelKey(reason: string): string {
  return KNOWN_REASONS.has(reason) ? `operations.objectDenials.reasons.${reason}` : 'operations.objectDenials.reasons.other'
}

/** First 8 characters of an id, enough to correlate rows without filling the table. */
export function shortId(id: string): string {
  return id.slice(0, 8)
}
