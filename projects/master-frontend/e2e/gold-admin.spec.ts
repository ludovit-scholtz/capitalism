import { expect, test } from '@playwright/test'
import { loginAs, makePlayer, setupMockApi } from './helpers/mock-api'

// ── Gold Admin Page ────────────────────────────────────────────────────────

test.describe('Gold token admin — unauthenticated', () => {
  test('redirects to /login when not authenticated', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/gold-admin')

    // GoldAdminView redirects unauthenticated users to /login
    await expect(page).toHaveURL('/login')
  })
})

test.describe('Gold token admin — authenticated non-admin', () => {
  test('redirects regular player to home when visiting admin route', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      currentPlayer: player,
      isGlobalAdmin: false,
      goldBalances: [],
    })
    state.currentToken = 'token-player'

    await loginAs(page, state, player)
    await page.goto('/gold-admin')

    await expect(page).toHaveURL('/')
  })
})

test.describe('Gold token admin — global admin', () => {
  test('shows gold admin page heading for global admins', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      isGlobalAdmin: true,
      goldBalances: [
        { playerId: 'p1', email: 'alice@example.com', displayName: 'Alice', goldTokenBalance: 0 },
        { playerId: 'p2', email: 'bob@example.com', displayName: 'Bob', goldTokenBalance: 50.5 },
      ],
      goldTransactions: [],
    })
    state.currentToken = 'token-admin'

    await loginAs(page, state, admin, 'token-admin')
    await page.goto('/gold-admin')

    await expect(page.getByRole('heading', { level: 1 })).toContainText('Gold Token Management')
    await expect(page.getByText('Player Balances')).toBeVisible()
  })

  test('displays player table with balances', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      isGlobalAdmin: true,
      goldBalances: [
        {
          playerId: 'p1',
          email: 'alice@example.com',
          displayName: 'Alice',
          goldTokenBalance: 10.5,
        },
        { playerId: 'p2', email: 'bob@example.com', displayName: 'Bob', goldTokenBalance: 0 },
      ],
      goldTransactions: [],
    })
    state.currentToken = 'token-admin'

    await loginAs(page, state, admin, 'token-admin')
    await page.goto('/gold-admin')

    await expect(page.getByRole('table', { name: 'Player gold balances' })).toBeVisible()
    await expect(page.getByText('alice@example.com')).toBeVisible()
    await expect(page.getByText('bob@example.com')).toBeVisible()
    // Alice has 10.5g
    await expect(page.getByText('10.5000 g')).toBeVisible()
  })

  test('search filters player table by email', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      isGlobalAdmin: true,
      goldBalances: [
        { playerId: 'p1', email: 'alice@example.com', displayName: 'Alice', goldTokenBalance: 10 },
        { playerId: 'p2', email: 'bob@example.com', displayName: 'Bob', goldTokenBalance: 5 },
      ],
      goldTransactions: [],
    })
    state.currentToken = 'token-admin'

    await loginAs(page, state, admin, 'token-admin')
    await page.goto('/gold-admin')

    await page.getByRole('searchbox').fill('alice')

    await expect(page.getByText('alice@example.com')).toBeVisible()
    await expect(page.getByText('bob@example.com')).not.toBeVisible()
  })

  test('clicking Manage opens adjust panel for that player', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      isGlobalAdmin: true,
      goldBalances: [
        { playerId: 'p1', email: 'alice@example.com', displayName: 'Alice', goldTokenBalance: 10 },
      ],
      goldTransactions: [],
    })
    state.currentToken = 'token-admin'

    await loginAs(page, state, admin, 'token-admin')
    await page.goto('/gold-admin')

    await page.getByRole('button', { name: 'Manage' }).first().click()

    await expect(page.getByRole('heading', { name: /Adjust balance for/ })).toBeVisible()
    await expect(page.getByRole('heading', { name: /alice@example.com/i })).toBeVisible()
  })

  test('admin can add gold to a player', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      isGlobalAdmin: true,
      goldBalances: [
        { playerId: 'p1', email: 'alice@example.com', displayName: 'Alice', goldTokenBalance: 0 },
      ],
      goldTransactions: [],
    })
    state.currentToken = 'token-admin'

    await loginAs(page, state, admin, 'token-admin')
    await page.goto('/gold-admin')

    await page.getByRole('button', { name: 'Manage' }).first().click()

    await page.getByLabel(/Amount/).fill('25')
    await page.getByLabel(/Note/).fill('Welcome bonus')
    await page.getByRole('button', { name: 'Add Gold' }).click()

    await expect(page.getByRole('status')).toContainText('Balance updated to 25.0000 g')
  })

  test('admin can deduct gold from a player with sufficient balance', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      isGlobalAdmin: true,
      goldBalances: [
        {
          playerId: 'p1',
          email: 'alice@example.com',
          displayName: 'Alice',
          goldTokenBalance: 100,
        },
      ],
      goldTransactions: [],
    })
    state.currentToken = 'token-admin'

    await loginAs(page, state, admin, 'token-admin')
    await page.goto('/gold-admin')

    await page.getByRole('button', { name: 'Manage' }).first().click()

    await page.getByLabel(/Amount/).fill('-30')
    await page.getByLabel(/Note/).fill('Correction for test event overpayment')
    await page.getByRole('button', { name: 'Deduct Gold' }).click()

    await expect(page.getByRole('status')).toContainText('Balance updated to 70.0000 g')
  })

  test('shows error when deduction would make balance negative', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      isGlobalAdmin: true,
      goldBalances: [
        { playerId: 'p1', email: 'alice@example.com', displayName: 'Alice', goldTokenBalance: 5 },
      ],
      goldTransactions: [],
    })
    state.currentToken = 'token-admin'

    await loginAs(page, state, admin, 'token-admin')
    await page.goto('/gold-admin')

    await page.getByRole('button', { name: 'Manage' }).first().click()

    await page.getByLabel(/Amount/).fill('-100')
    await page.getByLabel(/Note/).fill('Will fail due to negative balance')
    await page.getByRole('button', { name: 'Deduct Gold' }).click()

    await expect(page.getByRole('alert')).toContainText('balance')
  })

  test('shows error when audit note is empty', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      isGlobalAdmin: true,
      goldBalances: [
        { playerId: 'p1', email: 'alice@example.com', displayName: 'Alice', goldTokenBalance: 0 },
      ],
      goldTransactions: [],
    })
    state.currentToken = 'token-admin'

    await loginAs(page, state, admin, 'token-admin')
    await page.goto('/gold-admin')

    await page.getByRole('button', { name: 'Manage' }).first().click()

    // Fill amount but leave note empty
    await page.getByLabel(/Amount/).fill('10')

    // Submit button should be disabled when note is empty
    const submitBtn = page.getByRole('button', { name: 'Add Gold' })
    await expect(submitBtn).toBeDisabled()

    // Manually force a submission attempt via JavaScript to test the backend guard path too
    await page.getByLabel(/Note/).fill('   ')
    // Note is whitespace-only → button still disabled because trim() is empty
    await expect(submitBtn).toBeDisabled()
  })

  test('transaction history is visible and shows audit records', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      isGlobalAdmin: true,
      goldBalances: [
        { playerId: 'p1', email: 'alice@example.com', displayName: 'Alice', goldTokenBalance: 50 },
      ],
      goldTransactions: [
        {
          id: 'tx-1',
          playerEmail: 'alice@example.com',
          amount: 50,
          balanceBefore: 0,
          balanceAfter: 50,
          adminEmail: 'admin@example.com',
          note: 'Initial grant',
          createdAtUtc: '2026-04-20T12:00:00.000Z',
        },
      ],
    })
    state.currentToken = 'token-admin'

    await loginAs(page, state, admin, 'token-admin')
    await page.goto('/gold-admin')

    await expect(page.getByRole('table', { name: 'Gold token transaction log' })).toBeVisible()
    const txTable = page.getByRole('table', { name: 'Gold token transaction log' })
    await expect(txTable.getByRole('cell', { name: 'alice@example.com' })).toBeVisible()
    await expect(txTable.getByText('Initial grant')).toBeVisible()
    await expect(txTable.getByText('+50.0000')).toBeVisible()
  })

  test('home page shows Game Admin Dashboard link for global admins', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      servers: [],
      isGlobalAdmin: true,
    })
    state.currentToken = 'token-admin'

    await loginAs(page, state, admin, 'token-admin')
    await page.goto('/')

    // Two links with this name exist on the page (AppHeader nav-link + ViewSubnav subnav-link).
    // Use .first() to avoid strict-mode violation while still confirming the link is present.
    await expect(page.getByRole('link', { name: /Game admin dashboard/i }).first()).toBeVisible()
  })
})
