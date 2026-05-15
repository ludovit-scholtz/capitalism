import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useNotificationsStore } from '@/stores/notifications'

const gqlRequestMock = vi.fn()

vi.mock('@/lib/graphql', () => ({
  gqlRequest: (...args: unknown[]) => gqlRequestMock(...args),
}))

function hasBalancedCurlyBraces(query: string) {
  let depth = 0

  for (const char of query) {
    if (char === '{') {
      depth += 1
      continue
    }

    if (char === '}') {
      depth -= 1
      if (depth < 0) {
        return false
      }
    }
  }

  return depth === 0
}

describe('useNotificationsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    gqlRequestMock.mockReset()
  })

  it('fetchInbox sends a balanced MyNotifications query and stores the returned items', async () => {
    gqlRequestMock
      .mockResolvedValueOnce({
        myNotifications: {
          edges: [
            {
              node: {
                id: 'notif-1',
                type: 'CITY_EXPANSION_UNLOCKED',
                severity: 'INFO',
                title: '',
                message: '',
                titleKey: 'cityExpansion.notificationTitle',
                bodyKey: 'cityExpansion.notificationMessage',
                bodyParamsJson: JSON.stringify({ city: 'Berlin', company: 'Northwind Holdings' }),
                isRead: false,
                createdAtTick: 120,
                createdAtUtc: '2026-05-15T00:00:00Z',
                expiresAtUtc: null,
                companyId: 'company-1',
                buildingId: null,
                buildingUnitId: null,
                bankAccountId: null,
                loanId: null,
                relatedEntityType: 'CITY',
                relatedEntityId: 'city-ber',
              },
            },
          ],
        },
      })
      .mockResolvedValueOnce({
        notificationCount: 1,
      })

    const store = useNotificationsStore()
    const inbox = await store.fetchInbox(20)

    const notificationsQuery = gqlRequestMock.mock.calls[0]?.[0]

    expect(typeof notificationsQuery).toBe('string')
    expect(notificationsQuery).toContain('query MyNotifications($first: Int!, $onlyUnread: Boolean!)')
    expect(notificationsQuery).toContain('myNotifications(first: $first, onlyUnread: $onlyUnread)')
    expect(hasBalancedCurlyBraces(notificationsQuery as string)).toBe(true)
    expect(gqlRequestMock).toHaveBeenNthCalledWith(1, expect.any(String), { first: 20, onlyUnread: false })
    expect(inbox).toEqual({
      unreadCount: 1,
      items: [
        expect.objectContaining({
          id: 'notif-1',
          titleKey: 'cityExpansion.notificationTitle',
          bodyKey: 'cityExpansion.notificationMessage',
        }),
      ],
    })
    expect(store.unreadCount).toBe(1)
  })
})