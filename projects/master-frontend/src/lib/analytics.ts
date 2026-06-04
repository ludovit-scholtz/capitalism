interface GtagWindow extends Window {
  gtag?: (...args: unknown[]) => void
}

function resolveGtag(): ((...args: unknown[]) => void) | null {
  if (typeof window === 'undefined') {
    return null
  }

  const gtagWindow = window as GtagWindow
  return typeof gtagWindow.gtag === 'function' ? gtagWindow.gtag : null
}

/**
 * Reports a single-page-application navigation to Google Analytics.
 * No-ops during SSR or when the gtag.js tag has not loaded (e.g. tests, ad blockers).
 */
export function trackPageView(pagePath: string, pageTitle?: string): void {
  const gtag = resolveGtag()
  if (!gtag) {
    return
  }

  gtag('event', 'page_view', {
    page_path: pagePath,
    page_location: typeof window !== 'undefined' ? window.location.href : undefined,
    ...(pageTitle ? { page_title: pageTitle } : {}),
  })
}

/**
 * Reports a custom event to Google Analytics.
 * No-ops during SSR or when the gtag.js tag has not loaded.
 */
export function trackEvent(eventName: string, params?: Record<string, unknown>): void {
  const gtag = resolveGtag()
  if (!gtag) {
    return
  }

  gtag('event', eventName, params ?? {})
}
