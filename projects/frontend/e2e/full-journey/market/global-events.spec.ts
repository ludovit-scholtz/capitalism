import { test, expect, type Page } from '@playwright/test'
import { setupMockApi, makePlayer, makeAdminPlayer, type MockActiveGlobalEvent } from '../../helpers/mock-api'

function makeSupplyChainEvent(overrides?: Partial<MockActiveGlobalEvent>): MockActiveGlobalEvent {
  return {
    id: 'evt-supply-chain-1',
    eventType: 'SUPPLY_CHAIN_DISRUPTION',
    severity: 'MAJOR',
    title: 'Supply Chain Disruption',
    description: 'Global shipping delays are increasing operating costs across all industries.',
    isActive: true,
    startTick: 10,
    durationTicks: 50,
    affectedCityId: null,
    affectedCity: null,
    operatingCostMultiplier: 1.25,
    tradeRouteMultiplier: 0.9,
    rdMultiplier: 1.0,
    mineEfficiencyMultiplier: 1.0,
    createdAtUtc: '2026-01-01T12:00:00Z',
    resolvedAtUtc: null,
    triggeredByAdminId: null,
    ...overrides,
  }
}

function makeTechBoomEvent(overrides?: Partial<MockActiveGlobalEvent>): MockActiveGlobalEvent {
  return {
    id: 'evt-tech-boom-1',
    eventType: 'TECH_BOOM',
    severity: 'MODERATE',
    title: 'Technology Boom',
    description: 'A wave of technological innovation is boosting research & development output.',
    isActive: true,
    startTick: 5,
    durationTicks: 100,
    affectedCityId: null,
    affectedCity: null,
    operatingCostMultiplier: 1.0,
    tradeRouteMultiplier: 1.0,
    rdMultiplier: 1.35,
    mineEfficiencyMultiplier: 1.0,
    createdAtUtc: '2026-01-01T08:00:00Z',
    resolvedAtUtc: null,
    triggeredByAdminId: null,
    ...overrides,
  }
}

test.describe('Global Events — unauthenticated visitor', () => {
  test('no global event banner when no active events', async ({ page }) => {
    setupMockApi(page, {})
    await page.goto('/')
    // The global event banner should not be visible when there are no active events
    await expect(page.locator('.global-event-banner')).toBeHidden()
  })

  test('shows global event banner for active event', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.activeGlobalEvents = [makeSupplyChainEvent()]
    await page.goto('/')
    await expect(page.locator('.global-event-banner')).toBeVisible()
    await expect(page.locator('.global-event-banner')).toContainText('Supply Chain Disruption')
  })

  test('shows multiple events count in banner when more than one event', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.activeGlobalEvents = [makeSupplyChainEvent(), makeTechBoomEvent()]
    await page.goto('/')
    await expect(page.locator('.global-event-banner')).toBeVisible()
    // Banner should indicate there are 2 active events
    await expect(page.locator('.global-event-banner')).toContainText('2')
  })

  test('banner can be dismissed', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.activeGlobalEvents = [makeSupplyChainEvent()]
    await page.goto('/')
    await expect(page.locator('.global-event-banner')).toBeVisible()
    // Click the dismiss button
    const dismissBtn = page.locator('.global-event-banner').getByRole('button')
    await dismissBtn.click()
    await expect(page.locator('.global-event-banner')).toBeHidden()
  })
})

test.describe('Global Events Panel — authenticated player', () => {
  async function setupAuth(page: Page, overrides?: Partial<{ activeGlobalEvents: MockActiveGlobalEvent[] }>) {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    if (overrides?.activeGlobalEvents) {
      state.activeGlobalEvents = overrides.activeGlobalEvents
    }
    await page.addInitScript((token: string) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    return { player, state }
  }

  test('shows empty state when no events are active', async ({ page }) => {
    await setupAuth(page, { activeGlobalEvents: [] })
    await page.goto('/market/events')
    await expect(
      page.getByText(/no active/i).or(page.getByText(/calm/i)).or(page.locator('.ge-empty-state')),
    ).toBeVisible()
  })

  test('shows active event card with severity badge and multiplier chips', async ({ page }) => {
    await setupAuth(page, { activeGlobalEvents: [makeSupplyChainEvent()] })
    await page.goto('/market/events')
    await expect(page.getByText('Supply Chain Disruption')).toBeVisible()
    // Severity badge
    await expect(page.locator('.ge-severity-badge, .event-severity').first()).toBeVisible()
    // Multiplier chip showing operating cost increase (+25%)
    await expect(page.locator('.ge-multiplier-chip, .multiplier-chip').first()).toBeVisible()
  })

  test('shows tech boom event with R&D multiplier', async ({ page }) => {
    await setupAuth(page, { activeGlobalEvents: [makeTechBoomEvent()] })
    await page.goto('/market/events')
    await expect(page.getByText('Technology Boom')).toBeVisible()
    await expect(page.getByText(/research|R.D|R&D/i).first()).toBeVisible()
  })

  test('shows multiple events listed', async ({ page }) => {
    await setupAuth(page, { activeGlobalEvents: [makeSupplyChainEvent(), makeTechBoomEvent()] })
    await page.goto('/market/events')
    await expect(page.getByText('Supply Chain Disruption')).toBeVisible()
    await expect(page.getByText('Technology Boom')).toBeVisible()
  })

  test('history tab shows resolved events', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const resolvedEvent = makeSupplyChainEvent({
      isActive: false,
      resolvedAtUtc: '2026-01-02T00:00:00Z',
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.activeGlobalEvents = [resolvedEvent]
    await page.addInitScript((token: string) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/market/events')
    // Switch to history tab
    await page.getByRole('button', { name: /history/i }).click()
    await expect(page.getByText('Supply Chain Disruption')).toBeVisible()
  })
})

test.describe('Global Events — admin operations', () => {
  test('admin can see trigger event option', async ({ page }) => {
    const admin = makeAdminPlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [admin] })
    state.currentUserId = admin.id
    state.currentToken = `token-${admin.id}`
    await page.addInitScript((token: string) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${admin.id}`)

    await page.goto('/market/events')
    // Admin should see a trigger/create event section or button
    await expect(
      page.getByRole('button', { name: /trigger|create event/i }).or(page.locator('.ge-trigger-btn, .trigger-event-btn, .ge-admin-section')).first()
    ).toBeVisible()
  })
})
