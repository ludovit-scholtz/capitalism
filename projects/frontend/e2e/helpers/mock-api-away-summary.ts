import type { Page } from '@playwright/test'

export interface MockAwaySummary {
  sinceTick: number
  currentTick: number
  ticksAway: number
  revenueUsd: number
  costsUsd: number
  netUsd: number
  loanPaymentsCount: number
}

export interface AwaySummaryMock {
  requests: number[]
}

/**
 * Serves the `awaySummary` query on top of `setupMockApi` (the most recently registered Playwright route runs first) and records
 * the `sinceTick` it was asked for. Matches the full operation, not a substring of other operations. Call after `setupMockApi`.
 */
export async function mockAwaySummary(page: Page, summary: MockAwaySummary): Promise<AwaySummaryMock> {
  const mock: AwaySummaryMock = { requests: [] }
  await page.route('**/graphql', async (route) => {
    const body = route.request().postDataJSON() as { query?: string; variables?: { sinceTick?: number } } | null
    if (!/\bawaySummary\s*\(/.test(body?.query ?? '')) {
      await route.fallback()
      return
    }
    mock.requests.push(body?.variables?.sinceTick ?? -1)
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: { awaySummary: summary } }),
    })
  })
  return mock
}
