import * as Sentry from '@sentry/vue'
import type { App } from 'vue'

import { readRuntimeAppConfigValue } from './runtimeConfig'

/**
 * Default Sentry DSN and Google Analytics measurement id. These can be
 * overridden at runtime through the deployed runtime-config.js so that staging
 * and production environments can report to their own projects.
 *
 * The Sentry DSN is a public client key (safe to ship to browsers); it only
 * allows submitting events, not reading them.
 */
const DEFAULT_SENTRY_DSN =
  'https://4a2e8303a6a395550e7c5b11b648c230@o4511479034413056.ingest.de.sentry.io/4511479036903504'
const DEFAULT_GOOGLE_ANALYTICS_ID = 'G-QQWP6LQH27'

interface GtagWindow extends Window {
  dataLayer?: unknown[]
  gtag?: (...args: unknown[]) => void
}

export function resolveSentryDsn(): string | null {
  return readRuntimeAppConfigValue('sentryDsn') ?? DEFAULT_SENTRY_DSN
}

export function resolveGoogleAnalyticsId(): string | null {
  return readRuntimeAppConfigValue('googleAnalyticsId') ?? DEFAULT_GOOGLE_ANALYTICS_ID
}

/**
 * Initialise Sentry error monitoring for the Vue application. Must be called on
 * the client before the app is mounted. No-ops during server-side rendering.
 */
export function initSentry(app: App): void {
  if (typeof window === 'undefined') {
    return
  }

  const dsn = resolveSentryDsn()
  if (!dsn) {
    return
  }

  Sentry.init({
    app,
    dsn,
    // Setting this option to true will send default PII data to Sentry.
    // For example, automatic IP address collection on events.
    sendDefaultPii: true,
  })
}

/**
 * Load the Google Analytics (gtag.js) tag and configure the measurement id.
 * Must be called on the client; no-ops during server-side rendering and avoids
 * injecting the tag more than once.
 */
export function initGoogleAnalytics(): void {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return
  }

  const measurementId = resolveGoogleAnalyticsId()
  if (!measurementId) {
    return
  }

  if (document.querySelector(`script[data-google-analytics-id="${measurementId}"]`)) {
    return
  }

  const script = document.createElement('script')
  script.async = true
  script.src = `https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(measurementId)}`
  script.setAttribute('data-google-analytics-id', measurementId)
  document.head.appendChild(script)

  const gtagWindow = window as GtagWindow
  gtagWindow.dataLayer = gtagWindow.dataLayer || []
  function gtag(...args: unknown[]) {
    gtagWindow.dataLayer?.push(args)
  }
  gtagWindow.gtag = gtagWindow.gtag || gtag
  gtagWindow.gtag('js', new Date())
  gtagWindow.gtag('config', measurementId)
}

/**
 * Initialise all third-party monitoring/analytics integrations for the client.
 */
export function initMonitoring(app: App): void {
  initSentry(app)
  initGoogleAnalytics()
}
