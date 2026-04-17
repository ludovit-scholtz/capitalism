import { test, expect } from '@playwright/test'
import {
  setupMockApi,
  makePlayer,
  type MockLoanOffer,
  type MockLoan,
} from './helpers/mock-api'

/** Creates a player who owns a BANK building with id 'bank-building-1'. */
function makeBankOwnerPlayer() {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  player.companies.push({
    id: 'lender-company-1',
    playerId: player.id,
    name: 'Lending Corp',
    cash: 500_000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [
      {
        id: 'bank-building-1',
        companyId: 'lender-company-1',
        cityId: 'city-ba',
        name: 'City Bank',
        type: 'BANK',
        level: 1,
        units: [],
        isUnderConstruction: false,
        constructionCompletesAtTick: null,
        pendingConfigurationTick: null,
        hasPendingConfiguration: false,
        powerStatus: 'POWERED',
        mediaType: null,
      },
    ],
  })
  player.activeAccountType = 'COMPANY'
  player.activeCompanyId = 'lender-company-1'
  return player
}

function makeLoanOffer(overrides: Partial<MockLoanOffer> = {}): MockLoanOffer {
  return {
    id: 'offer-1',
    bankBuildingId: 'bank-building-1',
    bankBuildingName: 'City Bank',
    cityId: 'city-ba',
    cityName: 'Bratislava',
    lenderCompanyId: 'lender-company-1',
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
    borrowerCompanyId: 'borrower-company-1',
    borrowerCompanyName: 'Borrower Corp',
    lenderCompanyId: 'lender-company-1',
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

test.describe('Loan Marketplace (/loans)', () => {
  test('shows loan marketplace page with empty state when no offers', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/loans')
    await expect(page.getByRole('heading', { name: 'Loan Offers', level: 1 })).toBeVisible()
    await expect(page.getByText('No loan offers available at this time.')).toBeVisible()
  })

  test('shows available loan offers for unauthenticated user', async ({ page }) => {
    const offer = makeLoanOffer()
    setupMockApi(page, { loanOffers: [offer] })
    await page.goto('/loans')

    await expect(page.getByText('City Bank')).toBeVisible()
    await expect(page.getByText('Lending Corp')).toBeVisible()
    await expect(page.getByText('12.0%')).toBeVisible()
  })

  test('shows Login to Access button for unauthenticated user instead of Accept Loan', async ({
    page,
  }) => {
    const offer = makeLoanOffer()
    setupMockApi(page, { loanOffers: [offer] })
    await page.goto('/loans')

    // Should see login link, not accept button
    await expect(page.getByRole('link', { name: 'Log in to access' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Accept Loan' })).toBeHidden()
  })

  test('shows Accept Loan button for authenticated user', async ({ page }) => {
    const offer = makeLoanOffer()
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    await expect(page.getByRole('button', { name: 'Accept Loan' })).toBeVisible()
  })

  test('shows accept loan confirmation modal with financial summary', async ({ page }) => {
    const offer = makeLoanOffer()
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    await page.getByRole('button', { name: 'Accept Loan' }).click()

    const modal = page.locator('[role="dialog"]')
    await expect(modal).toBeVisible()
    await expect(modal.getByText('Confirm Loan Acceptance')).toBeVisible()
    await expect(modal.getByText('Lending Corp')).toBeVisible()
    await expect(modal.getByText('12.0%')).toBeVisible()
    await expect(modal.getByText('Total repayment')).toBeVisible()
    // Risk warning should be visible
    await expect(modal.getByText(/Missing payments results in penalties/)).toBeVisible()
  })

  test('closes confirm modal on cancel', async ({ page }) => {
    const offer = makeLoanOffer()
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    await page.getByRole('button', { name: 'Accept Loan' }).click()
    await expect(page.locator('[role="dialog"]')).toBeVisible()

    await page.locator('[role="dialog"]').getByRole('button', { name: 'Cancel' }).click()
    await expect(page.locator('[role="dialog"]')).toBeHidden()
  })

  test('shows active loans for authenticated borrower', async ({ page }) => {
    const offer = makeLoanOffer()
    const loan = makeActiveLoan({ status: 'ACTIVE', lenderCompanyName: 'Lending Corp' })
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer], myLoans: [loan] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    await expect(page.getByRole('heading', { name: 'My Loans' })).toBeVisible()
    await expect(page.getByText('$25,000').first()).toBeVisible()
  })

  test('shows overdue warning for overdue loan', async ({ page }) => {
    const offer = makeLoanOffer()
    const loan = makeActiveLoan({
      status: 'OVERDUE',
      missedPayments: 1,
      accumulatedPenalty: 500,
    })
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer], myLoans: [loan] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    // Should see overdue badge
    await expect(page.getByText('⚠ Overdue')).toBeVisible()
    // Should see missed payment warning
    await expect(page.getByText(/1 missed payment/)).toBeVisible()
    await expect(page.getByText(/\$500/)).toBeVisible()
  })

  test('happy path: borrow loan and see it appear in my loans', async ({ page }) => {
    const offer = makeLoanOffer()
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 5000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    // Click Accept Loan on the offer
    await page.getByRole('button', { name: 'Accept Loan' }).click()
    const modal = page.locator('[role="dialog"]')
    await expect(modal).toBeVisible()

    // The confirm button should be enabled
    const confirmBtn = modal.getByRole('button', { name: 'Accept Loan' })
    await expect(confirmBtn).toBeEnabled()
    await confirmBtn.click()

    // Modal should close
    await expect(modal).toBeHidden()

    // Should now show My Loans section
    await expect(page.getByRole('heading', { name: 'My Loans' })).toBeVisible()
  })

  test('shows multiple loan offers side by side for comparison', async ({ page }) => {
    const offer1 = makeLoanOffer({ id: 'offer-a', annualInterestRatePercent: 8, lenderCompanyName: 'Low Rate Bank' })
    const offer2 = makeLoanOffer({ id: 'offer-b', annualInterestRatePercent: 15, lenderCompanyName: 'High Rate Bank' })
    setupMockApi(page, { loanOffers: [offer1, offer2] })
    await page.goto('/loans')

    await expect(page.getByText('Low Rate Bank')).toBeVisible()
    await expect(page.getByText('High Rate Bank')).toBeVisible()
    await expect(page.getByText('8.0%')).toBeVisible()
    await expect(page.getByText('15.0%')).toBeVisible()
  })

  test('unauthenticated user sees login CTA in lender action panel', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/loans')

    const lenderPanel = page.locator('[aria-label="Lender action"]')
    await expect(lenderPanel).toBeVisible()
    await expect(lenderPanel.getByRole('heading', { name: 'Become a Lender', level: 2 })).toBeVisible()
    await expect(lenderPanel.getByRole('heading', { name: 'Log In to Start Lending', level: 3 })).toBeVisible()
    // Login-specific description (not the no-bank explanation)
    await expect(lenderPanel.getByText('Log in or create a free account to start offering loans')).toBeVisible()
    await expect(lenderPanel.getByRole('link', { name: 'Log in to offer loans' })).toBeVisible()
    // Must NOT show the no-bank description which is for authenticated users without a bank
    await expect(lenderPanel.getByText('you need to acquire a Bank building')).toBeHidden()
  })

  test('authenticated player without bank sees Acquire a Bank CTA', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 50000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [], // no bank
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    const lenderPanel = page.locator('[aria-label="Lender action"]')
    await expect(lenderPanel.getByText("You Don't Own a Bank Yet")).toBeVisible()
    await expect(lenderPanel.getByRole('button', { name: 'Acquire a Bank' })).toBeVisible()
  })

  test('clicking Acquire a Bank navigates to buy-building page', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 50000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    await page.locator('[aria-label="Lender action"]').getByRole('button', { name: 'Acquire a Bank' }).click()
    await expect(page).toHaveURL(/\/buy-building\/company-1/)
  })

  test('authenticated player with bank sees Manage My Bank CTA', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'lender-company-1',
      playerId: player.id,
      name: 'Lending Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        {
          id: 'bank-building-1',
          companyId: 'lender-company-1',
          cityId: 'city-ba',
          name: 'City Bank',
          type: 'BANK',
          level: 1,
          units: [],
          isUnderConstruction: false,
          constructionCompletesAtTick: null,
          pendingConfigurationTick: null,
          hasPendingConfiguration: false,
          powerStatus: 'POWERED',
          mediaType: null,
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    const lenderPanel = page.locator('[aria-label="Lender action"]')
    await expect(lenderPanel.getByText('You Own a Bank')).toBeVisible()
    await expect(lenderPanel.getByText('City Bank')).toBeVisible()
    await expect(lenderPanel.locator('button').filter({ hasText: 'Manage My Bank' })).toBeVisible()
  })

  test('clicking Manage My Bank navigates to bank management page', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'lender-company-1',
      playerId: player.id,
      name: 'Lending Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        {
          id: 'bank-building-1',
          companyId: 'lender-company-1',
          cityId: 'city-ba',
          name: 'City Bank',
          type: 'BANK',
          level: 1,
          units: [],
          isUnderConstruction: false,
          constructionCompletesAtTick: null,
          pendingConfigurationTick: null,
          hasPendingConfiguration: false,
          powerStatus: 'POWERED',
          mediaType: null,
        },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    await page.locator('[aria-label="Lender action"]').locator('button').filter({ hasText: 'Manage My Bank' }).click()
    await expect(page).toHaveURL(/\/bank\/bank-building-1/)
  })
})

test.describe('Bank Management (/bank/:buildingId)', () => {
  test('shows bank management page for authenticated bank owner', async ({ page }) => {
    const loanOffer = makeLoanOffer({ bankBuildingId: 'bank-building-1' })
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'lender-company-1',
      playerId: player.id,
      name: 'Lending Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        {
          id: 'bank-building-1',
          companyId: 'lender-company-1',
          cityId: 'city-ba',
          name: 'City Bank',
          type: 'BANK',
          level: 1,
          units: [],
          isUnderConstruction: false,
          constructionCompletesAtTick: null,
          pendingConfigurationTick: null,
          hasPendingConfiguration: false,
          powerStatus: 'POWERED',
          mediaType: null,
        },
      ],
    })
    const state = setupMockApi(page, { players: [player], loanOffers: [loanOffer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await expect(page.getByRole('heading', { name: 'Configure Bank' })).toBeVisible()
    await expect(page.getByText('Loan Offers')).toBeVisible()
  })

  test('shows bank stats overview', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player], loanOffers: [], myLoans: [] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await expect(page.getByText('Active Loans')).toBeVisible()
    await expect(page.getByText('Capital Outstanding')).toBeVisible()
    await expect(page.getByText('Overdue/Defaulted')).toBeVisible()
  })

  test('shows publish offer form when button clicked', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await page.getByRole('button', { name: 'Publish Loan Offer' }).click()
    await expect(page.getByRole('heading', { name: 'Publish Loan Offer' }).last()).toBeVisible()
    // Form fields should be visible
    await expect(page.getByLabel('Annual Interest Rate (%)')).toBeVisible()
    await expect(page.getByLabel('Max Principal Per Loan ($)')).toBeVisible()
    await expect(page.getByLabel('Total Lending Capacity ($)')).toBeVisible()
  })

  test('cancels publish form when Cancel clicked', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await page.getByRole('button', { name: 'Publish Loan Offer' }).click()
    await expect(page.getByLabel('Annual Interest Rate (%)')).toBeVisible()

    await page.getByRole('button', { name: 'Cancel' }).click()
    await expect(page.getByLabel('Annual Interest Rate (%)')).toBeHidden()
  })

  test('publishes offer and shows it in the offer list', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player], loanOffers: [] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    // Open form
    await page.getByRole('button', { name: 'Publish Loan Offer' }).click()

    // Submit form (default values)
    await page.getByRole('button', { name: 'Publish Loan Offer' }).last().click()

    // After publishing, form should hide and offers table should show
    await expect(page.getByLabel('Annual Interest Rate (%)')).toBeHidden()
  })

  test('shows issued loans in issued loans section', async ({ page }) => {
    const issuedLoan = makeActiveLoan({
      bankBuildingId: 'bank-building-1',
      borrowerCompanyName: 'Borrower Inc',
    })
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player], myLoans: [issuedLoan] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await expect(page.getByRole('heading', { name: 'Issued Loans' })).toBeVisible()
    await expect(page.getByText('Borrower Inc')).toBeVisible()
  })

  test('shows delinquency warning for overdue issued loan', async ({ page }) => {
    const overdueIssuedLoan = makeActiveLoan({
      bankBuildingId: 'bank-building-1',
      status: 'OVERDUE',
      missedPayments: 2,
    })
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player], myLoans: [overdueIssuedLoan] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    // Should see overdue badge in issued loans table
    await expect(page.getByText('⚠ Overdue')).toBeVisible()
    // Should show missed count
    await expect(page.getByText('2 missed')).toBeVisible()
  })
})

test.describe('Loans nav link', () => {
  test('shows Loans link in nav bar', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')
    // Use href selector to find nav link (icon-only nav items may be hidden in accessibility tree on desktop)
    await expect(page.locator('.nav-links a[href="/loans"]')).toBeVisible()
  })

  test('clicking Loans nav link navigates to /loans', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')
    await page.locator('.nav-links a[href="/loans"]').click()
    await expect(page).toHaveURL('/loans')
  })
})

test.describe('Loan offer display details', () => {
  test('shows formatted duration in days', async ({ page }) => {
    const offer = makeLoanOffer({ durationTicks: 720 }) // 30 days
    setupMockApi(page, { loanOffers: [offer] })
    await page.goto('/loans')
    // 720 ticks = 30 in-game days
    await expect(page.getByText('30 days')).toBeVisible()
  })

  test('shows city name on offer card', async ({ page }) => {
    const offer = makeLoanOffer({ cityName: 'Vienna' })
    setupMockApi(page, { loanOffers: [offer] })
    await page.goto('/loans')
    await expect(page.getByText('Vienna')).toBeVisible()
  })
})

test.describe('Loan Marketplace — tick-refresh stability', () => {
  test('background tick refresh does not show a loading spinner or blank the offers list', async ({
    page,
  }) => {
    const offer = makeLoanOffer({ lenderCompanyName: 'Tick Bank', annualInterestRatePercent: 8 })
    const state = setupMockApi(page, { loanOffers: [offer] })
    state.gameState.currentTick = 10
    state.gameState.tickIntervalSeconds = 1
    state.gameState.lastTickAtUtc = new Date(Date.now() - 500).toISOString()

    await page.goto('/loans')
    await expect(page.getByText('Tick Bank')).toBeVisible()

    // Simulate tick advancing
    state.gameState.currentTick = 11
    state.gameState.lastTickAtUtc = new Date().toISOString()

    // Offer must remain visible without a loading spinner blanking the page
    await expect(page.getByText('Tick Bank')).toBeVisible()
    await expect(page.locator('.loading-state')).toBeHidden()
  })

  test('active loan list is preserved after a background tick refresh', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    const activeLoan = makeActiveLoan({
      id: 'loan-refresh-1',
      borrowerCompanyName: 'Refresh Borrower Corp',
      lenderCompanyName: 'Tick Lender',
    })

    const state = setupMockApi(page, { players: [player], myLoans: [activeLoan] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameState.currentTick = 20
    state.gameState.tickIntervalSeconds = 1
    state.gameState.lastTickAtUtc = new Date(Date.now() - 400).toISOString()

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/loans')
    await expect(page.getByText('Tick Lender')).toBeVisible()

    // Simulate tick advancing
    state.gameState.currentTick = 21
    state.gameState.lastTickAtUtc = new Date().toISOString()

    // Loan entry must remain visible — context must not be lost
    await expect(page.getByText('Tick Lender')).toBeVisible()
    await expect(page.locator('.loading-state')).toBeHidden()
  })
})

test.describe('Bank Management — tick-refresh stability', () => {
  test('background tick refresh does not show a loading spinner or blank the bank management view', async ({
    page,
  }) => {
    const player = makeBankOwnerPlayer()
    const offer = makeLoanOffer({
      id: 'offer-bank-refresh',
      lenderCompanyName: 'Refresh Lender Corp',
      annualInterestRatePercent: 10,
    })

    const state = setupMockApi(page, { players: [player], loanOffers: [offer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameState.currentTick = 5
    state.gameState.tickIntervalSeconds = 1
    state.gameState.lastTickAtUtc = new Date(Date.now() - 300).toISOString()

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/bank/bank-building-1')
    await expect(page.getByRole('heading', { name: 'Configure Bank' })).toBeVisible()

    // Simulate tick advancing
    state.gameState.currentTick = 6
    state.gameState.lastTickAtUtc = new Date().toISOString()

    // Bank management heading must remain visible without a loading spinner
    await expect(page.getByRole('heading', { name: 'Configure Bank' })).toBeVisible()
    await expect(page.locator('.loading-state')).toBeHidden()
  })

  test('issued loans remain visible after a background tick refresh', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    const issuedLoan = makeActiveLoan({
      id: 'loan-bank-refresh',
      borrowerCompanyName: 'Borrower Co',
      lenderCompanyName: 'My Bank',
      bankBuildingId: 'bank-building-1',
    })

    const state = setupMockApi(page, { players: [player], myLoans: [issuedLoan] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.gameState.currentTick = 15
    state.gameState.tickIntervalSeconds = 1
    state.gameState.lastTickAtUtc = new Date(Date.now() - 400).toISOString()

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/bank/bank-building-1')
    await expect(page.getByText('Borrower Co')).toBeVisible()

    // Simulate tick advancing
    state.gameState.currentTick = 16
    state.gameState.lastTickAtUtc = new Date().toISOString()

    // Issued loan must remain visible after background refresh
    await expect(page.getByText('Borrower Co')).toBeVisible()
    await expect(page.locator('.loading-state')).toBeHidden()
  })
})

test.describe('Loan Marketplace — sort and filter banks', () => {
  function makeBankInfo(
    id: string,
    name: string,
    city: string,
    depositRate: number,
    lendingRate: number,
    available: number,
  ) {
    return {
      bankBuildingId: id,
      bankBuildingName: name,
      cityId: city === 'Bratislava' ? 'city-ba' : city === 'Prague' ? 'city-pr' : 'city-vi',
      cityName: city,
      lenderCompanyId: `co-${id}`,
      lenderCompanyName: `Company ${id}`,
      depositInterestRatePercent: depositRate,
      lendingInterestRatePercent: lendingRate,
      totalDeposits: available / 0.9 + 1_000_000,
      lendableCapacity: available + 500_000,
      outstandingLoanPrincipal: 500_000,
      availableLendingCapacity: available,
      baseCapitalDeposited: true,
      centralBankDebt: 0,
      centralBankInterestRatePercent: 2,
      reserveRequirement: Math.round((available / 0.9 + 1_000_000) * 0.1),
      availableCash: Math.round((available / 0.9 + 1_000_000) * 0.5),
      reserveShortfall: 0,
      liquidityStatus: 'HEALTHY' as const,
    }
  }

  test('displays bank list with rates and capacity', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.allBanks = [
      makeBankInfo('b1', 'Alpha Bank', 'Bratislava', 5, 10, 1_000_000),
      makeBankInfo('b2', 'Beta Bank', 'Prague', 3, 8, 2_000_000),
    ]
    await page.goto('/loans')
    // Banks list lives in the Deposit tab
    await page.getByRole('tab', { name: 'Deposit' }).click()

    await expect(page.getByText('Alpha Bank')).toBeVisible()
    await expect(page.getByText('Beta Bank')).toBeVisible()
    // Rate and capacity info should be visible on each card (formatPercent gives 1 decimal)
    await expect(page.locator('.bank-card').first().getByText('5.0%')).toBeVisible()
    await expect(page.locator('.bank-card').first().getByText('Bratislava')).toBeVisible()
  })

  test('sort by deposit rate changes ordering', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.allBanks = [
      makeBankInfo('b1', 'LowRate Bank', 'Bratislava', 3, 10, 1_000_000),
      makeBankInfo('b2', 'HighRate Bank', 'Prague', 8, 15, 2_000_000),
    ]
    await page.goto('/loans')
    // Banks list lives in the Deposit tab
    await page.getByRole('tab', { name: 'Deposit' }).click()

    // Default sort is by deposit rate desc — high rate first
    const firstCard = page.locator('.bank-card').first()
    await expect(firstCard.getByText('HighRate Bank')).toBeVisible()

    // Click deposit rate sort again to toggle ascending
    await page.getByRole('group', { name: 'Sort by' }).getByText('Deposit Rate').click()

    // Now ascending — low rate first
    await expect(page.locator('.bank-card').first().getByText('LowRate Bank')).toBeVisible()
  })

  test('sort by lending rate changes ordering', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.allBanks = [
      makeBankInfo('b1', 'Cheap Loans', 'Bratislava', 5, 7, 1_000_000),
      makeBankInfo('b2', 'Expensive Loans', 'Prague', 4, 14, 2_000_000),
    ]
    await page.goto('/loans')
    // Banks list lives in the Deposit tab
    await page.getByRole('tab', { name: 'Deposit' }).click()

    // Click Lending Rate sort button
    await page.getByRole('group', { name: 'Sort by' }).getByText('Lending Rate').click()

    // Default sort dir is desc on first click — expensive loans first
    await expect(page.locator('.bank-card').first().getByText('Expensive Loans')).toBeVisible()
  })

  test('city filter shows only banks in selected city', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.allBanks = [
      makeBankInfo('b1', 'Bratislava Bank', 'Bratislava', 5, 10, 1_000_000),
      makeBankInfo('b2', 'Prague Bank', 'Prague', 5, 10, 2_000_000),
    ]
    await page.goto('/loans')
    // Banks list lives in the Deposit tab
    await page.getByRole('tab', { name: 'Deposit' }).click()

    await expect(page.getByText('Bratislava Bank')).toBeVisible()
    await expect(page.getByText('Prague Bank')).toBeVisible()

    // Filter to Prague only
    await page.locator('#city-filter').selectOption('Prague')

    await expect(page.getByText('Prague Bank')).toBeVisible()
    await expect(page.getByText('Bratislava Bank')).toBeHidden()
  })

  test('available capacity filter hides banks with no capacity', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.allBanks = [
      makeBankInfo('b1', 'Has Capacity', 'Bratislava', 5, 10, 1_000_000),
      makeBankInfo('b2', 'No Capacity', 'Prague', 5, 10, 0),
    ]
    await page.goto('/loans')
    // Banks list lives in the Deposit tab
    await page.getByRole('tab', { name: 'Deposit' }).click()

    await expect(page.getByText('No Capacity')).toBeVisible()

    await page.locator('label.filter-check input[type="checkbox"]').check()

    await expect(page.getByText('Has Capacity')).toBeVisible()
    await expect(page.getByText('No Capacity')).toBeHidden()
  })
})

test.describe('Bank Management — customer view', () => {
  test('customer sees rate/capacity profile panel for a bank they do not own', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'customer-co-1',
      playerId: player.id,
      name: 'Customer Corp',
      cash: 200_000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [], // no bank building
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.allBanks = [
      {
        bankBuildingId: 'ext-bank-1',
        bankBuildingName: 'External Bank',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        lenderCompanyId: 'other-co-1',
        lenderCompanyName: 'Other Corp',
        depositInterestRatePercent: 4,
        lendingInterestRatePercent: 9,
        totalDeposits: 10_000_000,
        lendableCapacity: 9_000_000,
        outstandingLoanPrincipal: 0,
        availableLendingCapacity: 9_000_000,
        baseCapitalDeposited: true,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/bank/ext-bank-1')

    // Customer view heading shows the bank building name (not generic 'Banking Services')
    await expect(page.getByRole('heading', { name: 'External Bank' })).toBeVisible()

    // Rate cards must be shown (formatPercent gives 1 decimal place)
    await expect(page.locator('.customer-rate-card.deposit').getByText('4.0%')).toBeVisible()
    await expect(page.locator('.customer-rate-card.lending').getByText('9.0%')).toBeVisible()
    await expect(page.locator('.customer-rate-card.capacity')).toBeVisible()
  })

  test('authenticated customer can make a deposit and see it listed', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'depositor-co-1',
      playerId: player.id,
      name: 'Depositor Corp',
      cash: 500_000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.allBanks = [
      {
        bankBuildingId: 'dep-bank-1',
        bankBuildingName: 'Deposit Bank',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        lenderCompanyId: 'other-co-2',
        lenderCompanyName: 'Bank Owner Corp',
        depositInterestRatePercent: 5,
        lendingInterestRatePercent: 10,
        totalDeposits: 5_000_000,
        lendableCapacity: 4_500_000,
        outstandingLoanPrincipal: 0,
        availableLendingCapacity: 4_500_000,
        baseCapitalDeposited: true,
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/bank/dep-bank-1')

    // The deposit form section must be visible
    await expect(page.getByRole('heading', { name: 'Make a Deposit' })).toBeVisible()

    // Fill in the amount and submit
    await page.locator('#customer-deposit-amount').fill('25000')

    // Rate preview is shown (formatPercent gives 1 decimal place) — scope to preview section
    await expect(page.locator('.repayment-preview').getByText('5.0%')).toBeVisible()

    await page.getByRole('button', { name: 'Confirm Deposit' }).click()

    // Success message
    await expect(page.getByText('Deposit created successfully.')).toBeVisible()
  })

  test('unauthenticated visitor sees login prompt in deposit form', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.allBanks = [
      {
        bankBuildingId: 'guest-bank-1',
        bankBuildingName: 'Guest Bank',
        cityId: 'city-ba',
        cityName: 'Bratislava',
        lenderCompanyId: 'other-co-3',
        lenderCompanyName: 'Guest Owner Corp',
        depositInterestRatePercent: 6,
        lendingInterestRatePercent: 11,
        totalDeposits: 3_000_000,
        lendableCapacity: 2_700_000,
        outstandingLoanPrincipal: 0,
        availableLendingCapacity: 2_700_000,
        baseCapitalDeposited: true,
      },
    ]

    await page.goto('/bank/guest-bank-1')

    // Rate cards should be visible (public info)
    await expect(page.locator('.customer-rate-card.deposit')).toBeVisible()

    // Deposit form should show login prompt, not the form
    await expect(page.getByRole('heading', { name: 'Make a Deposit' })).toBeVisible()
    // auth.login key maps to 'Login' — scope to the auth-prompt div to avoid matching the navbar link
    await expect(page.locator('.auth-prompt').getByRole('link', { name: 'Login' })).toBeVisible()
  })
})

test.describe('Bank Management — owner rate configuration', () => {
  test('owner can open rate configuration form and see current rates', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    // Owner sees the rates section heading
    await expect(page.getByRole('heading', { name: 'Bank Rates Configuration' })).toBeVisible()

    // Opening the rate form shows deposit and lending inputs
    await page.getByRole('button', { name: 'Update Rates' }).click()
    await expect(page.locator('#deposit-rate')).toBeVisible()
    await expect(page.locator('#lending-rate')).toBeVisible()
  })

  test('owner can cancel rate form without saving', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await page.getByRole('button', { name: 'Update Rates' }).click()
    await expect(page.locator('#deposit-rate')).toBeVisible()

    // Clicking Cancel hides the form again
    await page.getByRole('button', { name: 'Cancel' }).click()
    await expect(page.locator('#deposit-rate')).toBeHidden()
  })

  test('owner can submit updated rates', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await page.getByRole('button', { name: 'Update Rates' }).click()
    await page.locator('#deposit-rate').fill('6')
    await page.locator('#lending-rate').fill('14')

    await page.getByRole('button', { name: 'Update Rates' }).last().click()

    // Success message appears and form hides
    await expect(page.getByText('Rates updated')).toBeVisible()
  })
})
