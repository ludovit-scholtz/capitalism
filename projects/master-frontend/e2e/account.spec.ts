import { expect, test } from '@playwright/test'
import { loginAs, makePlayer, setupMockApi } from './helpers/mock-api'

// ── Account page — unauthenticated ─────────────────────────────────────────

test.describe('Account page — unauthenticated', () => {
  test('redirects to /login when not authenticated', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/account')

    await expect(page).toHaveURL('/login')
  })
})

// ── Account page — zero gold balance ──────────────────────────────────────

test.describe('Account page — zero gold balance', () => {
  test('shows zero balance and empty state message', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      currentPlayer: player,
      playerGoldAccount: {
        goldTokenBalance: 0,
        lastUpdatedAtUtc: null,
        recentTransactions: [],
      },
    })
    state.currentToken = 'token-player'

    await loginAs(page, state, player, 'token-player')
    await page.goto('/account')

    // Heading and kicker
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Gold Balance')
    await expect(page.getByText('My Account')).toBeVisible()

    // Balance shows 0.00 g
    await expect(page.locator('.balance-number')).toContainText('0')
    await expect(page.locator('.balance-unit')).toContainText('g')

    // 1 token = 1 gram copy is visible
    await expect(page.getByText('1 gold token = 1 gram of real-world gold')).toBeVisible()

    // Zero-balance empty state
    await expect(page.getByText("You don't have any gold yet")).toBeVisible()

    // No transactions
    await expect(
      page.getByText('No transactions yet. Transactions will appear here once your balance changes.'),
    ).toBeVisible()
  })
})

// ── Account page — positive gold balance ──────────────────────────────────

test.describe('Account page — with gold balance', () => {
  test('shows balance and recent transactions', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      currentPlayer: player,
      playerGoldAccount: {
        goldTokenBalance: 42.5,
        lastUpdatedAtUtc: '2026-04-20T10:00:00.000Z',
        recentTransactions: [
          {
            id: 'tx-001',
            amount: 42.5,
            balanceBefore: 0,
            balanceAfter: 42.5,
            note: 'Initial top-up',
            createdAtUtc: '2026-04-20T10:00:00.000Z',
          },
        ],
      },
    })
    state.currentToken = 'token-player'

    await loginAs(page, state, player, 'token-player')
    await page.goto('/account')

    // Balance shows the amount
    await expect(page.locator('.balance-number')).toContainText('42')

    // 1 token = 1 gram explanation present
    await expect(page.getByText('1 gold token = 1 gram of real-world gold')).toBeVisible()

    // Zero-state should NOT be visible when balance > 0
    await expect(page.getByText("You don't have any gold yet")).not.toBeVisible()

    // Transaction table is visible
    await expect(page.getByRole('table', { name: 'Recent gold transactions' })).toBeVisible()

    // Transaction row is visible
    await expect(page.getByText('Initial top-up')).toBeVisible()
    await expect(page.locator('.amount-positive').first()).toContainText('+')
  })

  test('shows negative transaction amounts in red class', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      currentPlayer: player,
      playerGoldAccount: {
        goldTokenBalance: 7.5,
        lastUpdatedAtUtc: '2026-04-20T11:00:00.000Z',
        recentTransactions: [
          {
            id: 'tx-002',
            amount: -2.5,
            balanceBefore: 10,
            balanceAfter: 7.5,
            note: 'Fee deduction',
            createdAtUtc: '2026-04-20T11:00:00.000Z',
          },
        ],
      },
    })
    state.currentToken = 'token-player'

    await loginAs(page, state, player, 'token-player')
    await page.goto('/account')

    await expect(page.locator('.amount-negative').first()).toContainText('-')
  })

  test('shows what-is-gold information section', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      currentPlayer: player,
      playerGoldAccount: {
        goldTokenBalance: 10,
        lastUpdatedAtUtc: null,
        recentTransactions: [],
      },
    })
    state.currentToken = 'token-player'

    await loginAs(page, state, player, 'token-player')
    await page.goto('/account')

    await expect(page.getByRole('heading', { name: 'What is tokenized gold?' })).toBeVisible()
    await expect(page.getByText('Cross-server asset.')).toBeVisible()
    await expect(page.getByText('Trade on the FX exchange.')).toBeVisible()
  })
})

// ── Account page — loading and error states ───────────────────────────────

test.describe('Account page — error state', () => {
  test('shows error message when API fails', async ({ page }) => {
    const player = makePlayer()
    setupMockApi(page, {
      currentPlayer: player,
    })

    await page.addInitScript(
      ({ tok, exp }: { tok: string; exp: string }) => {
        localStorage.setItem('master_auth_token', tok)
        localStorage.setItem('master_auth_expires', exp)
      },
      {
        tok: 'token-player',
        exp: new Date(Date.now() + 7200000).toISOString(),
      },
    )

    // Override to return an error for myGoldAccount
    await page.route('**/graphql', async (route) => {
      const body = route.request().postDataJSON() as { query: string }
      if (body.query?.includes('myGoldAccount')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            errors: [{ message: 'Service unavailable.', extensions: { code: 'SERVER_ERROR' } }],
          }),
        })
        return
      }
      // For all other queries (me, etc.) return ok
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            me: player,
            mySubscription: {
              tier: 'FREE',
              status: 'NONE',
              isActive: false,
              daysRemaining: null,
              canProlong: true,
              expiresAtUtc: null,
              startsAtUtc: null,
            },
          },
        }),
      })
    })

    await page.goto('/account')

    await expect(page.locator('.state-error')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Retry' })).toBeVisible()
  })
})

// ── Home page nav — account link ──────────────────────────────────────────

test.describe('Home page — account link for authenticated users', () => {
  test('shows My Gold link in nav for authenticated player', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      currentPlayer: player,
      servers: [],
    })
    state.currentToken = 'token-player'

    await loginAs(page, state, player, 'token-player')
    await page.goto('/')

    await expect(page.getByRole('link', { name: /My Gold/ })).toBeVisible()
    const link = page.getByRole('link', { name: /My Gold/ })
    await expect(link).toHaveAttribute('href', '/account')
  })
})
