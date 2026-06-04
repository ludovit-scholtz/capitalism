// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'

const sentryInit = vi.fn()
vi.mock('@sentry/vue', () => ({
  init: (...args: unknown[]) => sentryInit(...args),
}))

import {
  initGoogleAnalytics,
  initSentry,
  resolveGoogleAnalyticsId,
  resolveSentryDsn,
} from '../monitoring'
import type { RuntimeAppConfig } from '../runtimeConfig'

interface TestWindow extends Window {
  __capitalismRuntimeConfig__?: RuntimeAppConfig
  dataLayer?: unknown[]
  gtag?: (...args: unknown[]) => void
}

const testWindow = window as TestWindow

afterEach(() => {
  sentryInit.mockClear()
  document.head.innerHTML = ''
  delete testWindow.__capitalismRuntimeConfig__
  delete testWindow.dataLayer
  delete testWindow.gtag
})

describe('resolveSentryDsn', () => {
  it('falls back to the default Sentry dsn', () => {
    expect(resolveSentryDsn()).toContain('ingest.de.sentry.io')
  })

  it('prefers a runtime-configured Sentry dsn', () => {
    testWindow.__capitalismRuntimeConfig__ = { sentryDsn: 'https://abc@runtime.example.com/9' }
    expect(resolveSentryDsn()).toBe('https://abc@runtime.example.com/9')
  })
})

describe('resolveGoogleAnalyticsId', () => {
  it('falls back to the default measurement id', () => {
    expect(resolveGoogleAnalyticsId()).toBe('G-QQWP6LQH27')
  })

  it('prefers a runtime-configured measurement id', () => {
    testWindow.__capitalismRuntimeConfig__ = { googleAnalyticsId: 'G-RUNTIME01' }
    expect(resolveGoogleAnalyticsId()).toBe('G-RUNTIME01')
  })
})

describe('initSentry', () => {
  it('initialises Sentry with the resolved dsn and PII enabled', () => {
    const app = {} as never
    initSentry(app)

    expect(sentryInit).toHaveBeenCalledTimes(1)
    const config = sentryInit.mock.calls[0][0]
    expect(config.app).toBe(app)
    expect(config.dsn).toContain('ingest.de.sentry.io')
    expect(config.sendDefaultPii).toBe(true)
  })
})

describe('initGoogleAnalytics', () => {
  it('injects the gtag.js tag and configures the measurement id', () => {
    initGoogleAnalytics()

    const script = document.querySelector<HTMLScriptElement>(
      'script[data-google-analytics-id="G-QQWP6LQH27"]',
    )
    expect(script).not.toBeNull()
    expect(script?.src).toBe('https://www.googletagmanager.com/gtag/js?id=G-QQWP6LQH27')
    expect(script?.async).toBe(true)
    expect(Array.isArray(testWindow.dataLayer)).toBe(true)
    expect(typeof testWindow.gtag).toBe('function')
  })

  it('does not inject the tag more than once', () => {
    initGoogleAnalytics()
    initGoogleAnalytics()

    expect(
      document.querySelectorAll('script[data-google-analytics-id="G-QQWP6LQH27"]'),
    ).toHaveLength(1)
  })

  it('does not inject the tag when the static gtag snippet already loaded it', () => {
    testWindow.gtag = () => {}

    initGoogleAnalytics()

    expect(
      document.querySelectorAll('script[data-google-analytics-id="G-QQWP6LQH27"]'),
    ).toHaveLength(0)
  })
})
