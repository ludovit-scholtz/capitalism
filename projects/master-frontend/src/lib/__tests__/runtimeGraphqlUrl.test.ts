import { describe, expect, it } from 'vitest'

import { deriveMasterGraphqlUrl, resolveApiBaseUrl, resolveMasterGraphqlUrl } from '../runtimeGraphqlUrl'

describe('runtimeGraphqlUrl', () => {
  it('prefers an explicit configured url', () => {
    expect(resolveMasterGraphqlUrl('https://override.example.com/graphql')).toBe(
      'https://override.example.com/graphql',
    )
  })

  it('derives the master api host for the stage master frontend', () => {
    expect(
      deriveMasterGraphqlUrl({
        hostname: 'www.stage.capitalism5.com',
        protocol: 'https:',
      }),
    ).toBe('https://api.stage.capitalism5.com/graphql')
  })

  it('derives the master api host for a production shard host', () => {
    expect(
      deriveMasterGraphqlUrl({
        hostname: 'berlin-2026.capitalism5.com',
        protocol: 'https:',
      }),
    ).toBe('https://api.capitalism5.com/graphql')
  })

  it('falls back to the local master endpoint for localhost', () => {
    expect(
      resolveMasterGraphqlUrl(undefined, {
        hostname: 'localhost',
        protocol: 'http:',
      }),
    ).toBe('http://localhost:44364/graphql')
  })

  it('builds the rest api base from the graphql url', () => {
    expect(resolveApiBaseUrl('https://api.stage.capitalism5.com/graphql')).toBe(
      'https://api.stage.capitalism5.com',
    )
  })
})