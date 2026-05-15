import { describe, expect, it } from 'vitest'

import type { PlayerNotificationItem } from '@/types'
import { resolveNotificationCopy } from '../notificationText'

const makeNotification = (overrides: Partial<PlayerNotificationItem> = {}): PlayerNotificationItem => ({
  id: 'notif-1',
  type: 'CITY_EXPANSION_UNLOCKED',
  severity: 'INFO',
  title: 'Fallback title',
  message: 'Fallback message',
  titleKey: null,
  bodyKey: null,
  bodyParamsJson: null,
  isRead: false,
  createdAtTick: 1,
  createdAtUtc: '2026-05-15T00:00:00Z',
  expiresAtUtc: null,
  companyId: null,
  buildingId: null,
  buildingUnitId: null,
  bankAccountId: null,
  loanId: null,
  relatedEntityType: null,
  relatedEntityId: null,
  ...overrides,
})

describe('resolveNotificationCopy', () => {
  it('uses localized keys with parsed params when available', () => {
    const item = makeNotification({
      title: '',
      message: '',
      titleKey: 'cityExpansion.notificationTitle',
      bodyKey: 'cityExpansion.notificationMessage',
      bodyParamsJson: JSON.stringify({ city: 'Berlin', company: 'Northwind' }),
    })

    const result = resolveNotificationCopy(
      item,
      (key, params) => `${key}:${params?.city ?? ''}:${params?.company ?? ''}`,
      (key) => key.startsWith('cityExpansion.'),
    )

    expect(result).toEqual({
      title: 'cityExpansion.notificationTitle:Berlin:Northwind',
      message: 'cityExpansion.notificationMessage:Berlin:Northwind',
    })
  })

  it('falls back to stored text when keys are missing', () => {
    const item = makeNotification()

    const result = resolveNotificationCopy(
      item,
      () => 'translated',
      () => false,
    )

    expect(result).toEqual({
      title: 'Fallback title',
      message: 'Fallback message',
    })
  })

  it('falls back to stored text when params json is invalid', () => {
    const item = makeNotification({
      titleKey: 'cityExpansion.notificationTitle',
      bodyKey: 'cityExpansion.notificationMessage',
      bodyParamsJson: '{bad json',
    })

    const result = resolveNotificationCopy(
      item,
      () => 'translated',
      () => true,
    )

    expect(result).toEqual({
      title: 'Fallback title',
      message: 'Fallback message',
    })
  })
})
