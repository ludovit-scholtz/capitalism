import { afterEach, describe, expect, it, vi } from 'vitest'
import { gqlRequest } from '../graphql'

describe('gqlRequest ownership error handling', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('maps FORBIDDEN to friendly generic message', async () => {
    vi.stubGlobal('localStorage', { getItem: () => null })
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        json: async () => ({
          errors: [{ message: 'raw internal detail', extensions: { code: 'FORBIDDEN' } }],
        }),
      })),
    )

    await expect(gqlRequest('{ me { id } }')).rejects.toMatchObject({
      code: 'FORBIDDEN',
      message: "You don't have permission to perform this action.",
    })
  })

  it('maps legacy NOT_OWNED_OR_NOT_FOUND to the same friendly message', async () => {
    vi.stubGlobal('localStorage', { getItem: () => null })
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        json: async () => ({
          errors: [{ message: 'legacy detail', extensions: { code: 'NOT_OWNED_OR_NOT_FOUND' } }],
        }),
      })),
    )

    await expect(gqlRequest('{ me { id } }')).rejects.toMatchObject({
      code: 'NOT_OWNED_OR_NOT_FOUND',
      message: "You don't have permission to perform this action.",
    })
  })

  it('sends requests with cookie credentials and without bearer headers', async () => {
    const fetchMock = vi.fn(async () => ({
      json: async () => ({ data: { me: { id: 'player-1' } } }),
    }))
    vi.stubGlobal('fetch', fetchMock)

    await gqlRequest<{ me: { id: string } }>('{ me { id } }')

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [, options] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(options.credentials).toBe('include')
    expect((options.headers as Record<string, string>)['Authorization']).toBeUndefined()
  })
})
