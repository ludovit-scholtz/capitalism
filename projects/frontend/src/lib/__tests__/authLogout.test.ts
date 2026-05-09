import { describe, expect, it } from 'vitest'
import { resolvePostLogoutRedirectUri } from '../authLogout'

describe('resolvePostLogoutRedirectUri', () => {
  it('returns home route for configured origin', () => {
    expect(resolvePostLogoutRedirectUri('https://game.example.com')).toBe('https://game.example.com/')
  })

  it('falls back to local home route when origin is missing', () => {
    expect(resolvePostLogoutRedirectUri(undefined)).toBe('http://localhost:5173/')
  })
})
