import * as Sentry from '@sentry/vue'
import type { App } from 'vue'

/**
 * Sentry DSN and Google Analytics measurement id used by the master frontend.
 *
 * The Sentry DSN is a public client key (safe to ship to browsers); it only
 * allows submitting events, not reading them.
 */
const SENTRY_DSN =
  'https://4a2e8303a6a395550e7c5b11b648c230@o4511479034413056.ingest.de.sentry.io/4511479036903504'
const GOOGLE_ANALYTICS_ID = 'G-QQWP6LQH27'

interface GtagWindow extends Window {
  dataLayer?: unknown[]
  gtag?: (...args: unknown[]) => void
}

/**
 * Initialise Sentry error monitoring for the Vue application. Must be called on
 * the client before the app is mounted.
 */
export function initSentry(app: App): void {
  if (typeof window === 'undefined') {
    return
  }

  Sentry.init({
    app,
    dsn: SENTRY_DSN,
    // Setting this option to true will send default PII data to Sentry.
    // For example, automatic IP address collection on events.
    sendDefaultPii: true,
  })
}

/**
 * Load the Google Analytics (gtag.js) tag and configure the measurement id.
 * Avoids injecting the tag more than once.
 */
export function initGoogleAnalytics(): void {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return
  }

  const gtagWindow = window as GtagWindow
  // The gtag.js snippet in index.html normally loads and configures Google
  // Analytics before the app boots. When it has already run, gtag is defined
  // and we must not inject a second tag (which would double-count page views).
  if (typeof gtagWindow.gtag === 'function') {
    return
  }

  if (document.querySelector(`script[data-google-analytics-id="${GOOGLE_ANALYTICS_ID}"]`)) {
    return
  }

  const script = document.createElement('script')
  script.async = true
  script.src = `https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(GOOGLE_ANALYTICS_ID)}`
  script.setAttribute('data-google-analytics-id', GOOGLE_ANALYTICS_ID)
  document.head.appendChild(script)

  gtagWindow.dataLayer = gtagWindow.dataLayer || []
  function gtag(...args: unknown[]) {
    gtagWindow.dataLayer?.push(args)
  }
  gtagWindow.gtag = gtagWindow.gtag || gtag
  gtagWindow.gtag('js', new Date())
  gtagWindow.gtag('config', GOOGLE_ANALYTICS_ID)
}

/**
 * Initialise all third-party monitoring/analytics integrations for the client.
 */
export function initMonitoring(app: App): void {
  initSentry(app)
  initGoogleAnalytics()
}
