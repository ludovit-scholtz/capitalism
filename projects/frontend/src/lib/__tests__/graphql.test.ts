import { afterEach, describe, expect, it, vi } from 'vitest'
import { gqlRequest } from '../graphql'

describe('gqlRequest ownership error handling', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('maps NOT_FOUND_OR_NOT_OWNED to friendly generic message', async () => {
    vi.stubGlobal('localStorage', { getItem: () => null })
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        json: async () => ({
          errors: [{ message: 'raw internal detail', extensions: { code: 'NOT_FOUND_OR_NOT_OWNED' } }],
        }),
      })),
    )

    await expect(gqlRequest('{ me { id } }')).rejects.toMatchObject({
      code: 'NOT_FOUND_OR_NOT_OWNED',
      message: "This item could not be found or you don't have permission to access it.",
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
      message: "This item could not be found or you don't have permission to access it.",
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
