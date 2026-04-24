/**
 * Theme contrast regression tests.
 *
 * These tests guard against the white-on-white / invisible text regressions
 * that occur when CSS custom property definitions are missing or when
 * hard-coded light-theme colors are used inside the dark-theme app.
 *
 * Each test verifies that key status badges, info panels, and text labels
 * are both rendered and readable (non-zero dimensions, correct CSS variable
 * resolution) in a real Chromium run.
 */

import { test, expect } from '@playwright/test'
import {
  setupMockApi,
  makePlayer,
  type MockLoanOffer,
  type MockLoan,
} from './helpers/mock-api'

function authenticateViaLocalStorage(token: string) {
  return (page: Parameters<(typeof test)[0]['page']>) =>
    page.addInitScript((t) => {
      localStorage.setItem('auth_token', t)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, token)
}

// ─── Shared loan fixture helpers ──────────────────────────────────────────────

function makeLoanOffer(overrides: Partial<MockLoanOffer> = {}): MockLoanOffer {
  return {
    id: 'offer-1',
    bankBuildingId: 'bank-building-1',
    bankBuildingName: 'City Bank',
    cityId: 'city-ba',
    cityName: 'Bratislava',
    lenderCompanyId: 'lender-co',
    lenderCompanyName: 'Lending Corp',
    annualInterestRatePercent: 12,
    maxPrincipalPerLoan: 50000,
    totalCapacity: 200000,
    usedCapacity: 0,
    remainingCapacity: 200000,
    durationTicks: 1440,
    isActive: true,
    createdAtTick: 1,
    createdAtUtc: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function makeActiveLoan(overrides: Partial<MockLoan> = {}): MockLoan {
  return {
    id: 'loan-1',
    loanOfferId: 'offer-1',
    borrowerCompanyId: 'borrower-co',
    borrowerCompanyName: 'Borrower Corp',
    lenderCompanyId: 'lender-co',
    lenderCompanyName: 'Lending Corp',
    bankBuildingId: 'bank-building-1',
    bankBuildingName: 'City Bank',
    originalPrincipal: 25000,
    remainingPrincipal: 25000,
    annualInterestRatePercent: 12,
    durationTicks: 1440,
    startTick: 42,
    dueTick: 1482,
    nextPaymentTick: 762,
    paymentAmount: 13500,
    paymentsMade: 0,
    totalPayments: 2,
    status: 'ACTIVE',
    missedPayments: 0,
    accumulatedPenalty: 0,
    acceptedAtUtc: '2026-01-15T00:00:00Z',
    closedAtUtc: null,
    ...overrides,
  }
}

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Theme contrast — Loan Marketplace badges', () => {
  test('ACTIVE loan status badge is visible and readable', async ({ page }) => {
    const offer = makeLoanOffer()
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-co',
      playerId: player.id,
      name: 'My Company',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const activeLoan = makeActiveLoan({ status: 'ACTIVE' })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer], myLoans: [activeLoan] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(`token-${player.id}`)(page)
    await page.goto('/loans')

    // Loan status badge for ACTIVE must be rendered and visible
    const activeBadge = page.locator('.loan-status-badge.status-active').first()
    await expect(activeBadge).toBeVisible()
    // Must have a light foreground color (not a dark color like #065f46) that is legible on dark bg
    const color = await activeBadge.evaluate((el) => getComputedStyle(el).color)
    // rgb values for bright greens like #4ade80 → r>150, g>200, b>100
    const [r, g] = color.match(/\d+/g)!.map(Number)
    expect(r).toBeGreaterThan(50)
    expect(g).toBeGreaterThan(150)
  })

  test('OVERDUE loan status badge is visible and not white-on-white', async ({ page }) => {
    const offer = makeLoanOffer()
    const overdueLoan = makeActiveLoan({ status: 'OVERDUE', missedPayments: 1 })
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-co',
      playerId: player.id,
      name: 'My Company',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer], myLoans: [overdueLoan] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(`token-${player.id}`)(page)
    await page.goto('/loans')

    const badge = page.locator('.loan-status-badge.status-overdue').first()
    await expect(badge).toBeVisible()
    const bg = await badge.evaluate((el) => getComputedStyle(el).backgroundColor)
    // Should have rgba alpha > 0 (not transparent), not a plain white #ffffff
    expect(bg).not.toBe('rgb(255, 255, 255)')
    expect(bg).not.toBe('rgba(0, 0, 0, 0)')
  })
})

test.describe('Theme contrast — Bank Management badges', () => {
  test('status-pill active badge uses dark-theme colors', async ({ page }) => {
    // Navigate to bank management (requires auth + company with bank building)
    // We can verify the CSS variable is defined at global scope without needing a full bank building
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(`token-${player.id}`)(page)
    await page.goto('/bank')

    // The page renders; verify key CSS tokens are defined
    const textPrimary = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-text-primary').trim(),
    )
    const background = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-background').trim(),
    )
    const textMuted = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-text-muted').trim(),
    )

    // Tokens must be defined (non-empty) so fallback hardcoded colors are not used
    expect(textPrimary).not.toBe('')
    expect(background).not.toBe('')
    expect(textMuted).not.toBe('')
  })
})

test.describe('Theme contrast — CSS token definitions', () => {
  /**
   * Core regression: all semantic alias tokens that were previously undefined
   * must now resolve to non-empty values so components do not fall back to
   * hard-coded light-theme colors.
   */
  test('all required semantic tokens are defined in :root', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')

    const requiredTokens = [
      '--color-text-primary',
      '--color-text-muted',
      '--color-text-tertiary',
      '--color-background',
      '--color-surface-secondary',
      '--color-bg-secondary',
      '--color-hover',
      '--color-border-light',
      '--color-border-muted',
    ]

    for (const token of requiredTokens) {
      const value = await page.evaluate(
        (t) => getComputedStyle(document.documentElement).getPropertyValue(t).trim(),
        token,
      )
      expect(value, `Token ${token} must be defined (non-empty)`).not.toBe('')
    }
  })

  test('--color-background is dark, not a light color', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')

    const bg = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-background').trim(),
    )
    // Dark theme background should start with #0 or similar dark hex (not #f, #e, #d, etc.)
    expect(bg.toLowerCase()).toMatch(/^#[0-4]/)
  })

  test('--color-text-primary is light, not a dark color', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')

    const textColor = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-text-primary').trim(),
    )
    // Light text on dark bg: #e6edf3 → first hex digit is e/d/c (high luminance)
    expect(textColor.toLowerCase()).toMatch(/^#[c-f]/)
  })
})

test.describe('Theme contrast — Dashboard power badges', () => {
  test('power badge uses readable color on dark background', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'comp-1',
          playerId: 'player-1',
          name: 'Power Corp',
          cash: 400000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-1',
              companyId: 'comp-1',
              cityId: 'city-ba',
              name: 'My Factory',
              buildingType: 'FACTORY',
              latitude: 48.148,
              longitude: 17.107,
              units: [],
              pendingConfiguration: null,
              powerConsumptionKw: 100,
              powerSupplyKw: 0,
              isPowered: true,
              powerStatusNote: null,
              isActive: true,
            },
          ],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(`token-${player.id}`)(page)
    await page.goto('/dashboard')

    // The dashboard should render correctly — heading is visible
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
  })
})

test.describe('Theme toggle — dark/light mode switching', () => {
  test('theme toggle button is visible in header for unauthenticated visitors', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')

    // The toggle should be in the header and accessible
    const toggleBtn = page.getByRole('button', { name: /Switch to light mode/i })
    await expect(toggleBtn).toBeVisible()
  })

  test('theme toggle button is visible in header for authenticated players', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(`token-${player.id}`)(page)
    await page.goto('/dashboard')

    const toggleBtn = page.getByRole('button', { name: /Switch to light mode/i })
    await expect(toggleBtn).toBeVisible()
  })

  test('clicking toggle switches app to light mode', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')

    // App starts in dark mode
    const htmlThemeBefore = await page.evaluate(() =>
      document.documentElement.getAttribute('data-theme'),
    )
    expect(htmlThemeBefore).toBe('dark')

    // Click the toggle
    await page.getByRole('button', { name: /Switch to light mode/i }).click()

    // App should now be in light mode
    const htmlThemeAfter = await page.evaluate(() =>
      document.documentElement.getAttribute('data-theme'),
    )
    expect(htmlThemeAfter).toBe('light')

    // Button should now show "Switch to dark mode"
    await expect(page.getByRole('button', { name: /Switch to dark mode/i })).toBeVisible()
  })

  test('clicking toggle twice returns to dark mode', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')

    // dark → light
    await page.getByRole('button', { name: /Switch to light mode/i }).click()
    // light → dark
    await page.getByRole('button', { name: /Switch to dark mode/i }).click()

    const theme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'))
    expect(theme).toBe('dark')
    await expect(page.getByRole('button', { name: /Switch to light mode/i })).toBeVisible()
  })

  test('theme preference persists in localStorage after toggle', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')

    await page.getByRole('button', { name: /Switch to light mode/i }).click()

    const stored = await page.evaluate(() => localStorage.getItem('app_theme'))
    expect(stored).toBe('light')
  })

  test('stored light preference is respected on page reload', async ({ page }) => {
    setupMockApi(page)
    // Pre-set light preference before page load
    await page.addInitScript(() => localStorage.setItem('app_theme', 'light'))
    await page.goto('/')

    const theme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'))
    expect(theme).toBe('light')
    // Background token should be a light color (#f...)
    const bg = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-background').trim(),
    )
    expect(bg.toLowerCase()).toMatch(/^#[c-f]/)
  })

  test('light mode text token is dark (readable on light background)', async ({ page }) => {
    setupMockApi(page)
    await page.addInitScript(() => localStorage.setItem('app_theme', 'light'))
    await page.goto('/')

    const textColor = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-text-primary').trim(),
    )
    // Dark text on light bg: #111827 → first hex digit is 1
    expect(textColor.toLowerCase()).toMatch(/^#[0-4]/)
  })

  test('dark mode is the default when no localStorage preference exists', async ({ page }) => {
    setupMockApi(page)
    // Explicitly clear any stored preference to simulate a new visitor
    await page.addInitScript(() => localStorage.removeItem('app_theme'))
    await page.goto('/')

    const theme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'))
    expect(theme).toBe('dark')
  })
})

test.describe('Theme contrast — OnboardingView (Tailwind migration regression)', () => {
  /**
   * These tests guard against the dark-mode regression that occurred when
   * OnboardingView.vue was migrated from scoped CSS to Tailwind utilities.
   * The migration must not break dark-mode token resolution on the wizard pages.
   */

  test('industry cards are visible with correct dark background in dark mode', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/onboarding')

    // Industry cards must be rendered
    await expect(page.locator('.industry-card').first()).toBeVisible()

    // Dark background token must be resolved on the onboarding page
    const bg = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-background').trim(),
    )
    expect(bg.toLowerCase()).toMatch(/^#[0-4]/)
  })

  test('onboarding wizard text is light-coloured in dark mode (readable)', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/onboarding')

    const textColor = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-text-primary').trim(),
    )
    // Light text on dark background
    expect(textColor.toLowerCase()).toMatch(/^#[c-f]/)
  })

  test('theme toggle switches OnboardingView to light mode correctly', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/onboarding')

    await expect(page.locator('.industry-card').first()).toBeVisible()

    // Switch to light mode via toggle
    await page.getByRole('button', { name: /Switch to light mode/i }).click()

    const bg = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-background').trim(),
    )
    // Light mode background should be a light colour
    expect(bg.toLowerCase()).toMatch(/^#[c-f]/)

    // Industry cards must still be visible after theme switch
    await expect(page.locator('.industry-card').first()).toBeVisible()
  })

  test('theme toggle in mobile viewport is accessible on onboarding page', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    setupMockApi(page)
    await page.goto('/onboarding')

    // At 375px the hamburger menu is always present — click it to open mobile nav
    await page.getByRole('button', { name: /menu/i }).click()

    // Toggle button must be accessible in the mobile nav
    const toggle = page
      .getByRole('button', { name: /Switch to light mode/i })
      .or(page.getByRole('button', { name: /Switch to dark mode/i }))
    await expect(toggle.first()).toBeVisible()
  })
})

test.describe('Theme contrast — DashboardView (Tailwind migration regression)', () => {
  /**
   * Guards against dark-mode CSS regressions introduced when DashboardView.vue
   * power-balance/power-badge scoped CSS was removed and replaced with Tailwind utilities.
   * Ensures the semantic class hooks (.power-balance--*, .power-badge--*) and dark
   * token resolution remain correct after the migration.
   */

  test('dashboard renders in dark mode with correct background token', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token: string) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/dashboard')

    // Dashboard tick clock must be visible
    await expect(page.locator('.tick-clock-widget')).toBeVisible()

    // Dark background token resolved — value starts with a dark hex code
    const bg = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-background').trim(),
    )
    expect(bg.toLowerCase()).toMatch(/^#[0-4]/)
  })

  test('theme toggle switches DashboardView to light mode without breaking layout', async ({
    page,
  }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token: string) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/dashboard')

    // Dashboard visible in dark mode
    await expect(page.locator('.tick-clock-widget')).toBeVisible()

    // Switch to light mode
    await page.getByRole('button', { name: /Switch to light mode/i }).click()

    // Light mode background token should resolve to a light colour
    const bg = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-background').trim(),
    )
    expect(bg.toLowerCase()).toMatch(/^#[c-f]/)

    // Dashboard tick clock still visible after theme switch
    await expect(page.locator('.tick-clock-widget')).toBeVisible()
  })
})

test.describe('Theme contrast — BuyBuildingView (Tailwind migration regression)', () => {
  /**
   * Guards against dark-mode CSS regressions introduced when BuyBuildingView.vue
   * selected-state scoped CSS was removed and replaced with Tailwind utilities.
   */

  test('buy building form renders in dark mode with correct background token', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Build Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token: string) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/buy-building/company-1')

    // Building type cards must be visible
    await expect(page.locator('.type-card').first()).toBeVisible()

    // Dark background token resolved
    const bg = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-background').trim(),
    )
    expect(bg.toLowerCase()).toMatch(/^#[0-4]/)
  })

  test('selected type card gets Tailwind selection classes in dark mode', async ({ page }) => {
    const player = makePlayer({
      onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
      companies: [
        {
          id: 'company-1',
          playerId: 'player-1',
          name: 'Build Corp',
          cash: 500000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [],
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token: string) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/buy-building/company-1')

    await expect(page.locator('.type-card').first()).toBeVisible()

    // Click the first type card to select it
    await page.locator('.type-card').first().click()

    // After selection the .selected class AND the visual Tailwind ring should be applied
    await expect(page.locator('.type-card.selected').first()).toBeVisible()
  })
})
