// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'

import { trackEvent, trackPageView } from '../analytics'

interface TestWindow extends Window {
  gtag?: (...args: unknown[]) => void
}

const testWindow = window as TestWindow

afterEach(() => {
  delete testWindow.gtag
  vi.restoreAllMocks()
})

describe('trackPageView', () => {
  it('no-ops when gtag is not available', () => {
    expect(() => trackPageView('/dashboard')).not.toThrow()
  })

  it('sends a page_view event with path and title', () => {
    const gtag = vi.fn()
    testWindow.gtag = gtag

    trackPageView('/dashboard', 'dashboard')

    expect(gtag).toHaveBeenCalledTimes(1)
    const [event, name, params] = gtag.mock.calls[0]
    expect(event).toBe('event')
    expect(name).toBe('page_view')
    expect((params as Record<string, unknown>).page_path).toBe('/dashboard')
    expect((params as Record<string, unknown>).page_title).toBe('dashboard')
  })

  it('omits the page_title when not provided', () => {
    const gtag = vi.fn()
    testWindow.gtag = gtag

    trackPageView('/news')

    const params = gtag.mock.calls[0][2] as Record<string, unknown>
    expect(params.page_path).toBe('/news')
    expect('page_title' in params).toBe(false)
  })
})

describe('trackEvent', () => {
  it('no-ops when gtag is not available', () => {
    expect(() => trackEvent('custom_event')).not.toThrow()
  })

  it('forwards the event name and params to gtag', () => {
    const gtag = vi.fn()
    testWindow.gtag = gtag

    trackEvent('deposit_gold_submit', { network: 'ALGORAND' })

    expect(gtag).toHaveBeenCalledWith('event', 'deposit_gold_submit', { network: 'ALGORAND' })
  })

  it('passes an empty object when params are omitted', () => {
    const gtag = vi.fn()
    testWindow.gtag = gtag

    trackEvent('custom_event')

    expect(gtag).toHaveBeenCalledWith('event', 'custom_event', {})
  })
})
