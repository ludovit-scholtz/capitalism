import { expect, test } from '@playwright/test'
import { loginAs, makePlayer, setupMockApi } from './helpers/mock-api'

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const MOCK_REPORT_WITH_FINDINGS = {
  generatedAt: '2026-05-11T08:00:00.000Z',
  totalFindings: 3,
  gateStatus: 'fail',
  failingCount: 2,
  findings: [
    {
      slug: '2026-W19-audit/token-confusion',
      fileStem: '2026-W19-audit',
      filePath: '/audits/2026-W19-audit.md',
      number: 8,
      title: 'Token boundary confusion',
      severity: 'Critical',
      status: 'Open',
      issues: [],
      owner: '@alice',
    },
    {
      slug: '2026-W19-audit/api-key-scope',
      fileStem: '2026-W19-audit',
      filePath: '/audits/2026-W19-audit.md',
      number: 4,
      title: 'API key full-account scope',
      severity: 'High',
      status: 'Open',
      issues: [],
      owner: '@bob',
    },
    {
      slug: '2026-W19-audit/info-leak',
      fileStem: '2026-W19-audit',
      filePath: '/audits/2026-W19-audit.md',
      number: 1,
      title: 'Loan offer info leak',
      severity: 'High',
      status: 'Resolved',
      issues: [101],
      owner: '@charlie',
    },
  ],
}

const MOCK_REPORT_ALL_CLEAR = {
  generatedAt: '2026-05-11T08:00:00.000Z',
  totalFindings: 2,
  gateStatus: 'pass',
  failingCount: 0,
  findings: [
    {
      slug: '2026-W19-audit/info-leak',
      fileStem: '2026-W19-audit',
      filePath: '/audits/2026-W19-audit.md',
      number: 1,
      title: 'Loan offer info leak',
      severity: 'High',
      status: 'Resolved',
      issues: [101],
      owner: '',
    },
    {
      slug: '2026-W19-audit/telemetry',
      fileStem: '2026-W19-audit',
      filePath: '/audits/2026-W19-audit.md',
      number: 3,
      title: 'Telemetry shard ID gap',
      severity: 'High',
      status: 'Resolved',
      issues: [102],
      owner: '',
    },
  ],
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function setupAdminPage(
  page: Parameters<typeof setupMockApi>[0],
  reportJson: object | null = MOCK_REPORT_WITH_FINDINGS,
) {
  const admin = makePlayer({ id: 'admin-001', email: 'admin@example.com', displayName: 'Admin' })
  const state = setupMockApi(page, {
    currentPlayer: admin,
    isGlobalAdmin: true,
  })
  state.currentToken = 'token-admin'

  // Intercept the JSON report file fetch
  if (reportJson !== null) {
    page.route('**/security-board-report.json', async (route) => {
      await route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify(reportJson),
      })
    })
  } else {
    page.route('**/security-board-report.json', async (route) => {
      await route.fulfill({ status: 404, body: 'Not found' })
    })
  }

  return { admin, state }
}

// ---------------------------------------------------------------------------
// Unauthenticated access
// ---------------------------------------------------------------------------

test.describe('Security board — unauthenticated', () => {
  test('redirects to /login when not authenticated', async ({ page }) => {
    setupMockApi(page, { servers: [] })

    await page.goto('/admin/security-board')
    await expect(page).toHaveURL('/login')
  })
})

// ---------------------------------------------------------------------------
// Non-admin authenticated access
// ---------------------------------------------------------------------------

test.describe('Security board — authenticated non-admin', () => {
  test('redirects non-admin to home', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, {
      currentPlayer: player,
      isGlobalAdmin: false,
    })
    state.currentToken = 'token-player'

    await loginAs(page, state, player)
    await page.goto('/admin/security-board')

    await expect(page).toHaveURL('/')
  })
})

// ---------------------------------------------------------------------------
// Admin with findings
// ---------------------------------------------------------------------------

test.describe('Security board — admin with open findings', () => {
  test('renders board table with findings', async ({ page }) => {
    const { admin, state } = setupAdminPage(page)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/admin/security-board')

    // Heading
    await expect(page.getByRole('heading', { name: /Security Action Board/ })).toBeVisible()

    // Gate warning visible
    await expect(page.getByText(/unlinked.*High\/Critical/i)).toBeVisible()

    // Table is present
    await expect(page.locator('[aria-label="Security findings"]')).toBeVisible()

    // Critical badge visible
    await expect(page.locator('.severity-critical').first()).toBeVisible()
    await expect(page.locator('.severity-high').first()).toBeVisible()
  })

  test('shows finding titles and owners', async ({ page }) => {
    const { admin, state } = setupAdminPage(page)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/admin/security-board')

    await expect(page.getByText('Token boundary confusion')).toBeVisible()
    await expect(page.getByText('@alice')).toBeVisible()
    await expect(page.getByText('@bob')).toBeVisible()
  })

  test('shows linked issue numbers without exposing repository links', async ({ page }) => {
    const { admin, state } = setupAdminPage(page)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/admin/security-board')

    await expect(page.locator('.issue-ref')).toContainText('#101')
    await expect(page.locator('a[href*="github.com/ludovit-scholtz/capitalism"]')).toHaveCount(0)
  })

  test('shows dash for finding with no linked issue', async ({ page }) => {
    const { admin, state } = setupAdminPage(page)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/admin/security-board')

    // There should be at least one '—' placeholder (for unlinked findings)
    await expect(page.locator('.no-issue').first()).toBeVisible()
  })

  test('severity filter narrows the table', async ({ page }) => {
    const { admin, state } = setupAdminPage(page)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/admin/security-board')

    // Select Critical filter
    const severitySelect = page.locator('.filter-select').first()
    await severitySelect.selectOption('Critical')

    // Only Critical row should remain
    const rows = page.locator('.finding-row')
    await expect(rows).toHaveCount(1)
    await expect(rows.first().locator('.severity-critical')).toBeVisible()
  })

  test('status filter narrows the table', async ({ page }) => {
    const { admin, state } = setupAdminPage(page)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/admin/security-board')

    // Select Resolved filter
    const statusSelect = page.locator('.filter-select').nth(1)
    await statusSelect.selectOption('Resolved')

    // Only the Resolved finding should show
    const rows = page.locator('.finding-row')
    await expect(rows).toHaveCount(1)
    await expect(rows.first()).toContainText('Loan offer info leak')
  })

  test('shows audit source text without exposing repository links', async ({ page }) => {
    const { admin, state } = setupAdminPage(page)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/admin/security-board')

    const auditSource = page.locator('.audit-source').first()
    await expect(auditSource).toBeVisible()
    await expect(auditSource).toContainText('2026-W19-audit')
    await expect(page.locator('a[href*="github.com/ludovit-scholtz/capitalism"]')).toHaveCount(0)
  })
})

test.describe('Game admin dashboard — private repository privacy', () => {
  test('does not expose repository links on the admin dashboard', async ({ page }) => {
    const { admin, state } = setupAdminPage(page)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/game-admin')

    await expect(page.locator('.dep-audit-card')).toBeVisible()
    await expect(page.locator('a[href*="github.com/ludovit-scholtz/capitalism"]')).toHaveCount(0)
  })
})

// ---------------------------------------------------------------------------
// All-clear empty state
// ---------------------------------------------------------------------------

test.describe('Security board — all clear empty state', () => {
  test('shows all-clear banner when gate passes', async ({ page }) => {
    const { admin, state } = setupAdminPage(page, MOCK_REPORT_ALL_CLEAR)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/admin/security-board')

    await expect(page.locator('.all-clear-banner')).toBeVisible()
    await expect(page.getByText(/all clear/i)).toBeVisible()
  })

  test('shows last-updated date in all-clear state', async ({ page }) => {
    const { admin, state } = setupAdminPage(page, MOCK_REPORT_ALL_CLEAR)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/admin/security-board')

    await expect(page.getByText(/2026-05-11/)).toBeVisible()
  })
})

// ---------------------------------------------------------------------------
// Load error / 404 state
// ---------------------------------------------------------------------------

test.describe('Security board — report not generated yet', () => {
  test('shows error message when report file is not found', async ({ page }) => {
    const { admin, state } = setupAdminPage(page, null)
    await loginAs(page, state, admin, 'token-admin')

    await page.goto('/admin/security-board')

    await expect(page.locator('.state-error')).toBeVisible()
  })
})
