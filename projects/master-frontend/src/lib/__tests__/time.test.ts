import { describe, expect, it } from 'vitest'

import { formatHeartbeatDistance } from '../time'

describe('formatHeartbeatDistance', () => {
  const now = new Date('2026-04-02T12:00:00.000Z')

  it('returns just now for very fresh heartbeats', () => {
    expect(formatHeartbeatDistance('2026-04-02T11:59:55.000Z', now)).toBe('just now')
  })

  it('returns seconds for recent heartbeats', () => {
    expect(formatHeartbeatDistance('2026-04-02T11:59:20.000Z', now)).toBe('40s ago')
  })

  it('returns minutes for older heartbeats', () => {
    expect(formatHeartbeatDistance('2026-04-02T11:15:00.000Z', now)).toBe('45m ago')
  })

  it('returns unknown for invalid timestamps', () => {
    expect(formatHeartbeatDistance('not-a-date', now)).toBe('unknown')
  })
})