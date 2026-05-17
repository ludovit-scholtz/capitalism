import { afterEach, describe, expect, it, vi } from 'vitest'

import {
  deriveGameGraphqlUrl,
  deriveMasterGraphqlUrl,
  deriveMasterWebUrl,
  resolveApiBaseUrl,
  resolveGameGraphqlUrl,
  resolveMasterGraphqlUrl,
  resolveMasterWebUrl,
  resolveOptionalMasterApiBaseUrl,
} from '../runtimeGraphqlUrl'

describe('runtimeGraphqlUrl', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('prefers a runtime-configured game graphql url', () => {
    vi.stubGlobal('window', {
      location: {
        hostname: 'localhost',
        protocol: 'http:',
      },
      __capitalismRuntimeConfig__: {
        graphqlUrl: 'https://runtime.example.com/graphql',
      },
    })

    expect(resolveGameGraphqlUrl(undefined)).toBe('https://runtime.example.com/graphql')
  })

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

  it('derives the master web host for a stage shard frontend', () => {
    expect(
      deriveMasterWebUrl({
        hostname: 'berlin-2026.stage.capitalism5.com',
        protocol: 'https:',
      }),
    ).toBe('https://www.stage.capitalism5.com')
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

  it('prefers a runtime-configured master graphql url', () => {
    vi.stubGlobal('window', {
      location: {
        hostname: 'localhost',
        protocol: 'http:',
      },
      __capitalismRuntimeConfig__: {
        masterGraphqlUrl: 'https://api.stage.capitalism5.com/graphql',
      },
    })

    expect(resolveMasterGraphqlUrl(undefined)).toBe('https://api.stage.capitalism5.com/graphql')
  })

  it('prefers a runtime-configured master web url', () => {
    vi.stubGlobal('window', {
      location: {
        hostname: 'localhost',
        protocol: 'http:',
      },
      __capitalismRuntimeConfig__: {
        masterWebUrl: 'https://www.stage.capitalism5.com',
      },
    })

    expect(resolveMasterWebUrl(undefined)).toBe('https://www.stage.capitalism5.com')
  })

  it('builds a rest api base from a graphql endpoint', () => {
    expect(resolveApiBaseUrl('https://api.stage.capitalism5.com/graphql')).toBe(
      'https://api.stage.capitalism5.com',
    )
  })
})