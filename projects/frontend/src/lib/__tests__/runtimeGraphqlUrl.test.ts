import { describe, expect, it } from 'vitest'

import {
  deriveGameGraphqlUrl,
  deriveMasterGraphqlUrl,
  resolveApiBaseUrl,
  resolveGameGraphqlUrl,
  resolveMasterGraphqlUrl,
  resolveOptionalMasterApiBaseUrl,
} from '../runtimeGraphqlUrl'

describe('runtimeGraphqlUrl', () => {
  it('prefers an explicit game graphql url', () => {
    expect(resolveGameGraphqlUrl('https://override.example.com/graphql')).toBe(
      'https://override.example.com/graphql',
    )
  })

  it('derives the shard api host for a stage shard frontend', () => {
    expect(
      deriveGameGraphqlUrl({
        hostname: 'berlin-2026.stage.capitalism5.com',
        protocol: 'https:',
      }),
    ).toBe('https://berlin-2026-api.stage.capitalism5.com/graphql')
  })

  it('derives the master api host for a stage shard frontend', () => {
    expect(
      deriveMasterGraphqlUrl({
        hostname: 'berlin-2026.stage.capitalism5.com',
        protocol: 'https:',
      }),
    ).toBe('https://api.stage.capitalism5.com/graphql')
  })

  it('derives the master api host for the production master frontend domain', () => {
    expect(
      deriveMasterGraphqlUrl({
        hostname: 'www.capitalism5.com',
        protocol: 'https:',
      }),
    ).toBe('https://api.capitalism5.com/graphql')
  })

  it('falls back to the local game endpoint for localhost', () => {
    expect(
      resolveGameGraphqlUrl(undefined, {
        hostname: 'localhost',
        protocol: 'http:',
      }),
    ).toBe('http://localhost:44356/graphql')
  })

  it('keeps optional master session disabled for localhost without configuration', () => {
    expect(
      resolveOptionalMasterApiBaseUrl(undefined, {
        hostname: 'localhost',
        protocol: 'http:',
      }),
    ).toBe('')
  })

  it('resolves an optional master session base url for deployed shards', () => {
    expect(
      resolveOptionalMasterApiBaseUrl(undefined, {
        hostname: 'berlin-2026.capitalism5.com',
        protocol: 'https:',
      }),
    ).toBe('https://api.capitalism5.com')
  })

  it('uses the local master fallback when a direct master graphql url is needed', () => {
    expect(
      resolveMasterGraphqlUrl(undefined, {
        hostname: 'localhost',
        protocol: 'http:',
      }),
    ).toBe('https://localhost:44364/graphql')
  })

  it('builds a rest api base from a graphql endpoint', () => {
    expect(resolveApiBaseUrl('https://api.stage.capitalism5.com/graphql')).toBe(
      'https://api.stage.capitalism5.com',
    )
  })
})