import { describe, expect, it } from 'vitest'
import { OBJECT_ACCESS_DENIALS_QUERY, denialReasonLabelKey, shortId } from '../objectDenials'

describe('denialReasonLabelKey', () => {
  it.each(['not_found', 'not_owned', 'wrong_account_context', 'not_available'])('maps %s to its own label', (reason) => {
    expect(denialReasonLabelKey(reason)).toBe(`operations.objectDenials.reasons.${reason}`)
  })

  it('falls back to a generic label for unknown reasons', () => {
    expect(denialReasonLabelKey('some_future_reason')).toBe('operations.objectDenials.reasons.other')
  })
})

describe('shortId', () => {
  it('keeps the first 8 characters', () => {
    expect(shortId('0f8fad5b-d9cb-469f-a165-70867728950e')).toBe('0f8fad5b')
  })
})

describe('OBJECT_ACCESS_DENIALS_QUERY', () => {
  it('is a well-formed query that selects every field the panel renders', () => {
    expect(OBJECT_ACCESS_DENIALS_QUERY.startsWith('query ObjectAccessDenials($limit: Int) {')).toBe(true)
    expect(OBJECT_ACCESS_DENIALS_QUERY.split('{').length).toBe(OBJECT_ACCESS_DENIALS_QUERY.split('}').length)
    for (const field of ['totalRecorded', 'probeSignals', 'byObjectType', 'byReason', 'occurredAtUtc', 'objectType', 'objectId', 'reason', 'isProbeSignal']) {
      expect(OBJECT_ACCESS_DENIALS_QUERY).toContain(field)
    }
  })
})
