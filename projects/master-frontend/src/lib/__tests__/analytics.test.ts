import { afterEach, describe, expect, it, vi } from 'vitest'

import { trackEvent, trackPageView } from '../analytics'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('trackPageView', () => {
  it('no-ops during server-side rendering', () => {
    expect(() => trackPageView('/account/deposit')).not.toThrow()
  })

  it('no-ops when gtag is not available', () => {
    vi.stubGlobal('window', {})
    expect(() => trackPageView('/account/deposit')).not.toThrow()
  })

  it('sends a page_view event with path and title', () => {
    const gtag = vi.fn()
    vi.stubGlobal('window', { gtag, location: { href: 'https://example.com/account/deposit' } })

    trackPageView('/account/deposit', 'gold-deposit')

    expect(gtag).toHaveBeenCalledTimes(1)
    const [event, name, params] = gtag.mock.calls[0]
    expect(event).toBe('event')
    expect(name).toBe('page_view')
    expect((params as Record<string, unknown>).page_path).toBe('/account/deposit')
    expect((params as Record<string, unknown>).page_title).toBe('gold-deposit')
  })

  it('omits the page_title when not provided', () => {
    const gtag = vi.fn()
    vi.stubGlobal('window', { gtag, location: { href: 'https://example.com/game-servers' } })

    trackPageView('/game-servers')

    const params = gtag.mock.calls[0][2] as Record<string, unknown>
    expect(params.page_path).toBe('/game-servers')
    expect('page_title' in params).toBe(false)
  })
})

describe('trackEvent', () => {
  it('no-ops when gtag is not available', () => {
    vi.stubGlobal('window', {})
    expect(() => trackEvent('custom_event')).not.toThrow()
  })

  it('forwards the event name and params to gtag', () => {
    const gtag = vi.fn()
    vi.stubGlobal('window', { gtag })

    trackEvent('deposit_gold_submit', { network: 'ALGORAND' })

    expect(gtag).toHaveBeenCalledWith('event', 'deposit_gold_submit', { network: 'ALGORAND' })
  })

  it('passes an empty object when params are omitted', () => {
    const gtag = vi.fn()
    vi.stubGlobal('window', { gtag })

    trackEvent('custom_event')

    expect(gtag).toHaveBeenCalledWith('event', 'custom_event', {})
  })
})
