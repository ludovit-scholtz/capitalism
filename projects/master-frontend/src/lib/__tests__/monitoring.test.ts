import { afterEach, describe, expect, it, vi } from 'vitest'

const sentryInit = vi.fn()
vi.mock('@sentry/vue', () => ({
  init: (...args: unknown[]) => sentryInit(...args),
}))

import { initGoogleAnalytics, initSentry } from '../monitoring'

interface FakeScript {
  async?: boolean
  src?: string
  attributes: Record<string, string>
  setAttribute(name: string, value: string): void
}

function createDomMocks() {
  const created: FakeScript[] = []
  const appended: FakeScript[] = []

  const document = {
    querySelector(selector: string) {
      const match = selector.match(/data-google-analytics-id="([^"]+)"/)
      const id = match?.[1]
      return (
        appended.find((script) => script.attributes['data-google-analytics-id'] === id) ?? null
      )
    },
    createElement(): FakeScript {
      const script: FakeScript = {
        attributes: {},
        setAttribute(name: string, value: string) {
          this.attributes[name] = value
        },
      }
      created.push(script)
      return script
    },
    head: {
      appendChild(script: FakeScript) {
        appended.push(script)
        return script
      },
    },
  }

  return { document, created, appended }
}

afterEach(() => {
  vi.unstubAllGlobals()
  sentryInit.mockClear()
})

describe('initSentry', () => {
  it('initialises Sentry with the configured dsn and PII enabled', () => {
    vi.stubGlobal('window', {})
    const app = {} as never
    initSentry(app)

    expect(sentryInit).toHaveBeenCalledTimes(1)
    const config = sentryInit.mock.calls[0][0]
    expect(config.app).toBe(app)
    expect(config.dsn).toContain('ingest.de.sentry.io')
    expect(config.sendDefaultPii).toBe(true)
  })

  it('does nothing during server-side rendering', () => {
    initSentry({} as never)
    expect(sentryInit).not.toHaveBeenCalled()
  })
})

describe('initGoogleAnalytics', () => {
  it('injects the gtag.js tag and configures the measurement id', () => {
    const fakeWindow = {
      dataLayer: undefined as unknown[] | undefined,
      gtag: undefined as ((...args: unknown[]) => void) | undefined,
    }
    const { document } = createDomMocks()
    vi.stubGlobal('window', fakeWindow)
    vi.stubGlobal('document', document)

    initGoogleAnalytics()

    const script = document.querySelector('script[data-google-analytics-id="G-QQWP6LQH27"]')
    expect(script).not.toBeNull()
    expect(script?.src).toBe('https://www.googletagmanager.com/gtag/js?id=G-QQWP6LQH27')
    expect(script?.async).toBe(true)
    expect(Array.isArray(fakeWindow.dataLayer)).toBe(true)
    expect(typeof fakeWindow.gtag).toBe('function')
    expect(fakeWindow.dataLayer?.length).toBe(2)
  })

  it('does not inject the tag more than once', () => {
    const { document, appended } = createDomMocks()
    vi.stubGlobal('window', {})
    vi.stubGlobal('document', document)

    initGoogleAnalytics()
    initGoogleAnalytics()

    expect(appended).toHaveLength(1)
  })

  it('does not inject the tag when the static gtag snippet already loaded it', () => {
    const { document, appended } = createDomMocks()
    vi.stubGlobal('window', { gtag: () => {} })
    vi.stubGlobal('document', document)

    initGoogleAnalytics()

    expect(appended).toHaveLength(0)
  })
})
