import { test, expect, type Page } from '@playwright/test'
import { setupMockApi, makePlayer, type MockLoanOffer, type MockLoan, type MockCollateralBuilding, type MockBankInfo } from '../../helpers/mock-api'

const STARTER_FACTORY_LOT_NAME = /Factory Site B1/i
const STARTER_SHOP_LOT_NAME = /High Street Retail Space/i

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

/** Creates a bank info entry for the borrow/deposit tab. */
function makeBankInfoEntry(overrides: Partial<MockBankInfo> = {}): MockBankInfo {
  return {
    bankBuildingId: 'bank-building-1',
    bankBuildingName: 'City Bank',
    cityId: 'city-ba',
    cityName: 'Bratislava',
    lenderCompanyId: 'lender-company-1',
    lenderCompanyName: 'Lending Corp',
    depositInterestRatePercent: 3,
    lendingInterestRatePercent: 12,
    totalDeposits: 10_000_000,
    lendableCapacity: 9_000_000,
    outstandingLoanPrincipal: 0,
    availableLendingCapacity: 9_000_000,
    baseCapitalDeposited: true,
    centralBankDebt: 0,
    centralBankInterestRatePercent: 2,
    reserveRequirement: 1_000_000,
    availableCash: 5_000_000,
    reserveShortfall: 0,
    liquidityStatus: 'HEALTHY',
    cityCurrencyCode: 'EUR',
    cityCurrencySymbol: '€',
    baseCapitalRequirement: 10_000_000,
    ...overrides,
  }
}

async function authenticateViaLocalStorage(page: Page, token: string) {
  await page.addInitScript((storedToken) => {
    localStorage.setItem('auth_token', storedToken)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

async function completeAuthenticatedOnboarding(page: Page, companyName: string) {
  await page.locator('.city-card', { hasText: 'Bratislava' }).click()
  await expect(page.getByRole('heading', { name: 'Choose Your Industry' })).toBeVisible()
  await page.locator('.industry-card', { hasText: 'Furniture' }).click()
  await expect(page.getByRole('heading', { name: 'Choose Your First Product' })).toBeVisible()
  await page.locator('.product-card', { hasText: 'Wooden Chair' }).click()
  await expect(page.getByRole('heading', { name: 'Choose Your IPO Plan' })).toBeVisible()
  await page.locator('.ipo-card', { hasText: companyName ? 'Starter IPO' : 'Starter IPO' }).click()
  await page.getByRole('button', { name: 'List View' }).click()
  await page.getByRole('button', { name: STARTER_FACTORY_LOT_NAME }).click()
  await page.getByRole('button', { name: 'Purchase First Factory' }).click()
  await page.getByRole('button', { name: 'List View' }).click()
  await page.getByRole('button', { name: STARTER_SHOP_LOT_NAME }).click()
  await page.getByRole('button', { name: 'Purchase First Sales Shop' }).click()
  await expect(page.getByRole('heading', { name: /Your Empire Has Launched/i })).toBeVisible()
}

function computeAmortizedTickPayment(principal: number, annualInterestRatePercent: number, totalTicks: number) {
  const periodicRate = annualInterestRatePercent / 100 / 8760
  if (periodicRate <= 0) {
    return principal / totalTicks
  }

  return (principal * periodicRate) / (1 - (1 + periodicRate) ** -totalTicks)
}

function appendBankStatementRow(
  rows: Array<{
    id: string
    recordedAtTick: number
    recordedAtUtc: string
    description: string
    category: string
    amount: number
    runningBalance: number
    buildingId: string | null
    buildingName: string | null
  }>,
  row: {
    id: string
    recordedAtTick: number
    recordedAtUtc: string
    description: string
    category: string
    amount: number
    buildingId?: string | null
    buildingName?: string | null
  },
) {
  const previousBalance = rows.at(-1)?.runningBalance ?? 0
  rows.push({
    ...row,
    runningBalance: Number((previousBalance + row.amount).toFixed(2)),
    buildingId: row.buildingId ?? null,
    buildingName: row.buildingName ?? null,
  })
}

function applyMockLoanTickPayment(state: ReturnType<typeof setupMockApi>, loanId: string, accountId: string, companyId: string, buildingName: string) {
  const loan = state.myLoans.find((candidate) => candidate.id === loanId)
  const account = state.myBankAccounts.find((candidate) => candidate.id === accountId)
  if (!loan || !account) {
    throw new Error('Loan or bank account not found for amortization step.')
  }

  const periodicRate = loan.annualInterestRatePercent / 100 / 8760
  const interestAmount = Number((loan.remainingPrincipal * periodicRate).toFixed(2))
  const paymentAmount = Number(loan.paymentAmount.toFixed(2))
  const principalAmount = Number(Math.min(loan.remainingPrincipal, Number((paymentAmount - interestAmount).toFixed(2))).toFixed(2))
  const actualPayment = Number((principalAmount + interestAmount).toFixed(2))

  account.balance = Number((account.balance - actualPayment).toFixed(2))
  loan.remainingPrincipal = Number(Math.max(0, loan.remainingPrincipal - principalAmount).toFixed(2))
  loan.paymentsMade += 1
  loan.nextPaymentTick = state.gameState.currentTick + 1

  if (loan.remainingPrincipal <= 0.009 || loan.paymentsMade >= loan.totalPayments) {
    loan.remainingPrincipal = 0
    loan.status = 'REPAID'
    loan.closedAtUtc = new Date().toISOString()
    loan.nextPaymentTick = loan.dueTick
  }

  const rows = state.bankStatementRows[companyId] ?? []
  appendBankStatementRow(rows, {
    id: `${loan.id}-interest-${loan.paymentsMade}`,
    recordedAtTick: state.gameState.currentTick,
    recordedAtUtc: new Date().toISOString(),
    description: `Loan interest payment ${loan.paymentsMade}`,
    category: 'LOAN_INTEREST_EXPENSE',
    amount: -interestAmount,
    buildingName,
  })
  appendBankStatementRow(rows, {
    id: `${loan.id}-principal-${loan.paymentsMade}`,
    recordedAtTick: state.gameState.currentTick,
    recordedAtUtc: new Date().toISOString(),
    description: `Loan principal payment ${loan.paymentsMade}`,
    category: 'LOAN_REPAYMENT_PRINCIPAL',
    amount: -principalAmount,
    buildingName,
  })
  state.bankStatementRows[companyId] = rows

  state.gameState.currentTick += 1
  state.gameState.lastTickAtUtc = new Date().toISOString()

  return {
    interestAmount,
    principalAmount,
    paymentAmount: actualPayment,
    remainingPrincipal: loan.remainingPrincipal,
    status: loan.status,
  }
}

test.describe('Loan Marketplace (/loans)', () => {
  test('shows loan marketplace page with empty borrow state when no banks', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/loans')
    await expect(page.getByRole('heading', { name: 'Banks', level: 1 })).toBeVisible()
    // Borrow tab is active by default; with no banks, shows "no banks" empty state
    await expect(page.getByText('No banks are currently open for business.')).toBeVisible()
  })

  test('accounts tab shows player bank accounts including onboarding default account', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'starter-co-1',
      playerId: player.id,
      name: 'Starter Corp',
      cash: 50_000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.myBankAccounts = [
      {
        id: 'starter-bank-account-1',
        accountNumber: '1234567890123456',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 12_500,
        companyId: 'starter-co-1',
        companyName: 'Starter Corp',
        cityId: 'city-ba',
      },
    ]

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/loans')
    await page.getByRole('tab', { name: 'Accounts' }).click()

    await expect(page.getByRole('heading', { name: 'My Company Bank Accounts' })).toBeVisible()
    const accountRow = page.getByTestId('bank-account-row')
    await expect(accountRow.getByText('1234567890123456')).toBeVisible()
    await expect(accountRow.getByText('Starter Corp')).toBeVisible()
  })

  test('shows banks in borrow tab for unauthenticated user', async ({ page }) => {
    const state = setupMockApi(page)
    state.allBanks = [makeBankInfoEntry()]
    await page.goto('/loans')

    // Bank name, lender, and lending rate visible in borrow bank cards
    await expect(page.getByText('City Bank')).toBeVisible()
    await expect(page.getByText('Lending Corp')).toBeVisible()
    // lending rate shown as percentage in borrow card
    await expect(page.locator('.bank-borrow-card').getByText('12.0%')).toBeVisible()
  })

  test('shows login-to-lend CTA for unauthenticated user; Request Loan is not present', async ({ page }) => {
    const state = setupMockApi(page)
    state.allBanks = [makeBankInfoEntry()]
    await page.goto('/loans')

    // Lender CTA prompts login to offer loans
    await expect(page.getByRole('link', { name: 'Log in to offer loans' })).toBeVisible()
    // No Request Loan button — borrowing goes through individual bank pages
    await expect(page.getByRole('button', { name: 'Request Loan' })).toBeHidden()
  })

  test('authenticated user sees Visit Bank to Borrow link for each open bank', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.allBanks = [makeBankInfoEntry()]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    // "Visit Bank to Borrow" links visible in borrow section
    await expect(page.getByRole('link', { name: 'Visit Bank to Borrow' })).toBeVisible()
  })

  test('borrow section shows bank lending rates for easy comparison', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.allBanks = [makeBankInfoEntry()]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    // Bank card shows lending rate, available capacity, and city
    const card = page.locator('.bank-borrow-card')
    await expect(card.getByText('12.0%')).toBeVisible()
    await expect(card.getByText('Bratislava')).toBeVisible()
  })

  test('Visit Bank to Borrow navigates to bank management page', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 10000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.allBanks = [makeBankInfoEntry()]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    await page.getByRole('link', { name: 'Visit Bank to Borrow' }).click()
    await expect(page).toHaveURL(/\/bank\/bank-building-1/)
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

  test('happy path: visit bank page from marketplace to create loan contract', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 5000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.allBanks = [makeBankInfoEntry()]
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    // Borrow tab shows bank selection grid sorted by lowest lending rate
    await expect(page.getByText('Choose a Bank to Borrow From')).toBeVisible()
    await expect(page.locator('.bank-borrow-card')).toBeVisible()

    // Click "Visit Bank to Borrow" to navigate to the bank's own page
    await page.getByRole('link', { name: 'Visit Bank to Borrow' }).click()
    await expect(page).toHaveURL(/\/bank\/bank-building-1/)
  })

  test('borrow tab shows multiple banks sorted by lending rate', async ({ page }) => {
    const state = setupMockApi(page)
    state.allBanks = [
      makeBankInfoEntry({ bankBuildingId: 'b-low', bankBuildingName: 'Low Rate Bank', lendingInterestRatePercent: 8, lenderCompanyName: 'Low Rate Bank' }),
      makeBankInfoEntry({ bankBuildingId: 'b-high', bankBuildingName: 'High Rate Bank', lendingInterestRatePercent: 15, lenderCompanyName: 'High Rate Bank' }),
    ]
    await page.goto('/loans')

    // Both banks visible
    await expect(page.getByText('Low Rate Bank').first()).toBeVisible()
    await expect(page.getByText('High Rate Bank').first()).toBeVisible()
    // Lending rates visible
    await expect(page.locator('.bank-borrow-card').first().getByText('8.0%')).toBeVisible()
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
    const state = setupMockApi(page, {
      players: [player],
      allBanks: [makeBankInfoEntry({ bankBuildingId: 'bank-building-1', lenderCompanyId: 'lender-company-1', lenderCompanyName: 'Lending Corp' })],
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await expect(page.getByRole('heading', { name: 'Configure Bank' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Bank Rates Configuration' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Publish Loan Offer' })).toBeHidden()
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

  test('owner no longer sees publish-loan-offer controls', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await expect(page.getByRole('button', { name: 'Publish Loan Offer' })).toBeHidden()
    await expect(page.getByText('Loan Offers')).toBeHidden()
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
  test('shows Banking link in nav bar', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')
    // Nav now uses /banking (renamed from /loans); /loans is kept as alias
    await expect(page.locator('.nav-links a[href="/banking"]')).toBeVisible()
  })

  test('clicking Banking nav link navigates to /banking', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/')
    await page.locator('.nav-links a[href="/banking"]').click()
    await expect(page).toHaveURL('/banking')
  })

  test('legacy /loans path still works (alias)', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/loans')
    // /loans is aliased to /banking — the page content should load
    await expect(page.locator('.loan-marketplace-view')).toBeVisible()
  })
})

test.describe('Bank borrowing display details', () => {
  test('shows formatted default duration on the direct borrowing card', async ({ page }) => {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-company-1',
      playerId: player.id,
      name: 'My Company',
      cash: 200000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.allBanks = [makeBankInfoEntry({ bankBuildingId: 'bank-building-1' })]
    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await expect(page.getByRole('heading', { name: 'Borrow from This Bank' })).toBeVisible()
    await expect(page.getByText('8760 hours')).toBeVisible()
  })

  test('shows city name on borrow tab bank card', async ({ page }) => {
    const state = setupMockApi(page)
    state.allBanks = [makeBankInfoEntry({ cityName: 'Vienna', cityId: 'city-vi' })]
    await page.goto('/loans')
    await expect(page.getByText('Vienna')).toBeVisible()
  })
})

test.describe('Loan Marketplace — tick-refresh stability', () => {
  test('background tick refresh does not show a loading spinner or blank the bank borrow list', async ({ page }) => {
    const state = setupMockApi(page)
    state.allBanks = [makeBankInfoEntry({ bankBuildingName: 'Tick Bank', lenderCompanyName: 'Tick Bank' })]
    state.gameState.currentTick = 10
    state.gameState.tickIntervalSeconds = 1
    state.gameState.lastTickAtUtc = new Date(Date.now() - 500).toISOString()

    await page.goto('/loans')
    await expect(page.locator('.bank-borrow-card .bank-borrow-name')).toBeVisible()

    // Simulate tick advancing
    state.gameState.currentTick = 11
    state.gameState.lastTickAtUtc = new Date().toISOString()

    // Bank entry must remain visible — no loading spinner blanking the page
    await expect(page.locator('.bank-borrow-card .bank-borrow-name')).toBeVisible()
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
  test('background tick refresh does not show a loading spinner or blank the bank management view', async ({ page }) => {
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
  function makeBankInfo(id: string, name: string, city: string, depositRate: number, lendingRate: number, available: number) {
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
    state.allBanks = [makeBankInfo('b1', 'Alpha Bank', 'Bratislava', 5, 10, 1_000_000), makeBankInfo('b2', 'Beta Bank', 'Prague', 3, 8, 2_000_000)]
    await page.goto('/loans')
    // Banks list lives in the Deposit tab
    await page.getByRole('tab', { name: 'Accounts' }).click()

    await expect(page.getByText('Alpha Bank')).toBeVisible()
    await expect(page.getByText('Beta Bank')).toBeVisible()
    // Rate and capacity info should be visible on each card (formatPercent gives 1 decimal)
    await expect(page.locator('.bank-card').first().getByText('5.0%')).toBeVisible()
    await expect(page.locator('.bank-card').first().getByText('Bratislava')).toBeVisible()
  })

  test('sort by deposit rate changes ordering', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.allBanks = [makeBankInfo('b1', 'LowRate Bank', 'Bratislava', 3, 10, 1_000_000), makeBankInfo('b2', 'HighRate Bank', 'Prague', 8, 15, 2_000_000)]
    await page.goto('/loans')
    // Banks list lives in the Deposit tab
    await page.getByRole('tab', { name: 'Accounts' }).click()

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
    state.allBanks = [makeBankInfo('b1', 'Cheap Loans', 'Bratislava', 5, 7, 1_000_000), makeBankInfo('b2', 'Expensive Loans', 'Prague', 4, 14, 2_000_000)]
    await page.goto('/loans')
    // Banks list lives in the Deposit tab
    await page.getByRole('tab', { name: 'Accounts' }).click()

    // Click Lending Rate sort button
    await page.getByRole('group', { name: 'Sort by' }).getByText('Lending Rate').click()

    // Default sort dir is desc on first click — expensive loans first
    await expect(page.locator('.bank-card').first().getByText('Expensive Loans')).toBeVisible()
  })

  test('city filter shows only banks in selected city', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.allBanks = [makeBankInfo('b1', 'Bratislava Bank', 'Bratislava', 5, 10, 1_000_000), makeBankInfo('b2', 'Prague Bank', 'Prague', 5, 10, 2_000_000)]
    await page.goto('/loans')
    // Banks list lives in the Deposit tab
    await page.getByRole('tab', { name: 'Accounts' }).click()

    await expect(page.getByText('Bratislava Bank')).toBeVisible()
    await expect(page.getByText('Prague Bank')).toBeVisible()

    // Filter to Prague only
    await page.locator('#city-filter').selectOption('Prague')

    await expect(page.getByText('Prague Bank')).toBeVisible()
    await expect(page.getByText('Bratislava Bank')).toBeHidden()
  })

  test('available capacity filter hides banks with no capacity', async ({ page }) => {
    const state = setupMockApi(page, {})
    state.allBanks = [makeBankInfo('b1', 'Has Capacity', 'Bratislava', 5, 10, 1_000_000), makeBankInfo('b2', 'No Capacity', 'Prague', 5, 10, 0)]
    await page.goto('/loans')
    // Banks list lives in the Deposit tab
    await page.getByRole('tab', { name: 'Accounts' }).click()

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

  test('authenticated customer can open a zero-balance bank account and see the forex funding guidance', async ({ page }) => {
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

    // New account-style UI: shows "My Bank Account" heading with empty state
    await expect(page.getByRole('heading', { name: 'My Bank Account' })).toBeVisible()

    // The empty-state open-account action is shown (no existing deposits)
    await expect(page.getByRole('button', { name: 'Open Account' })).toBeVisible()

    // Rate preview is shown (formatPercent gives 1 decimal place) — scope to preview section
    await expect(page.locator('.repayment-preview').getByText('5.0%')).toBeVisible()
    await expect(page.getByText('Open the account with 0 balance for a new currency, then fund it from the Forex page.')).toBeVisible()

    await page.getByRole('button', { name: 'Open Account' }).click()

    // Success message
    await expect(page.getByText('Bank account opened successfully.')).toBeVisible()
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
    await expect(page.getByRole('heading', { name: 'Open Bank Account' })).toBeVisible()
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

// ── Collateral selection ──────────────────────────────────────────────────────

test.describe('Loan collateral selection', () => {
  function makeCompanyPlayer() {
    const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
    player.companies.push({
      id: 'borrower-co-1',
      playerId: player.id,
      name: 'My Company',
      cash: 15000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [],
    })
    return player
  }

  function addBorrowerSettlementAccount(state: ReturnType<typeof setupMockApi>) {
    state.myBankAccounts = [
      {
        id: 'borrower-settlement-acc-1',
        accountNumber: '5555000011112222',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 250000,
        companyId: 'borrower-co-1',
        companyName: 'My Company',
      },
    ]
  }

  const eligibleBuilding: MockCollateralBuilding = {
    buildingId: 'factory-1',
    buildingName: 'Main Factory',
    buildingType: 'FACTORY',
    level: 2,
    appraisedValue: 400000,
    maxBorrowable: 280000,
    existingSecuredExposure: 0,
    remainingBorrowingCapacity: 280000,
    currencyCode: 'EUR',
    isEligible: true,
    ineligibilityReason: null,
  }

  const alreadyPledgedBuilding: MockCollateralBuilding = {
    buildingId: 'shop-1',
    buildingName: 'Old Shop',
    buildingType: 'SALES_SHOP',
    level: 1,
    appraisedValue: 120000,
    maxBorrowable: 84000,
    existingSecuredExposure: 84000,
    remainingBorrowingCapacity: 0,
    currencyCode: 'EUR',
    isEligible: false,
    ineligibilityReason: 'Building is already pledged',
  }

  test('shows collateral section on loan request page', async ({ page }) => {
    const player = makeCompanyPlayer()
    const offer = makeLoanOffer({ id: 'offer-col-1', maxPrincipalPerLoan: 200000, remainingCapacity: 200000 })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.collateralBuildings = [eligibleBuilding]
    state.allBanks = [makeBankInfoEntry()]
    player.activeAccountType = 'COMPANY'

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await page.getByRole('button', { name: 'Request Loan' }).click()
    await expect(page).toHaveURL(/\/bank\/bank-building-1\/request-loan/)
    const form = page.locator('.loan-request-form-card')
    await expect(form).toBeVisible()

    // Collateral section should be visible
    await expect(form.locator('label', { hasText: 'Collateral' }).first()).toBeVisible()
    // Eligible building should be listed and stats visible
    await expect(form.getByText('Main Factory')).toBeVisible({ timeout: 10000 })
    // Stats: check appraised value appears in the collateral-stats area
    await expect(form.locator('.collateral-option', { hasText: 'Main Factory' })).toContainText('€400,000')
    await expect(form.locator('.collateral-option', { hasText: 'Main Factory' })).toContainText('€280,000')
  })

  test('selecting collateral shows LTV summary bar and capacity info', async ({ page }) => {
    const player = makeCompanyPlayer()
    const offer = makeLoanOffer({ id: 'offer-col-2', maxPrincipalPerLoan: 200000, remainingCapacity: 200000 })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.collateralBuildings = [eligibleBuilding]
    state.allBanks = [makeBankInfoEntry()]
    player.activeAccountType = 'COMPANY'

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await page.getByRole('button', { name: 'Request Loan' }).click()
    await expect(page).toHaveURL(/\/bank\/bank-building-1\/request-loan/)
    const form = page.locator('.loan-request-form-card')
    await expect(form).toBeVisible()

    // Select the eligible building as collateral
    await form.locator('.collateral-option', { hasText: 'Main Factory' }).click()

    // Should show collateral selected summary with building name
    await expect(form.locator('.collateral-selected-summary')).toBeVisible()
    await expect(form.locator('.collateral-selected-summary').getByText('Main Factory')).toBeVisible()
    // LTV bar should be visible
    await expect(form.locator('.capacity-bar-wrap')).toBeVisible()
  })

  test('warning shown when principal exceeds collateral cap', async ({ page }) => {
    const player = makeCompanyPlayer()
    // Offer allows up to 200000 but collateral cap is 280000 for factory
    // We'll set principal higher than remaining capacity
    const offer = makeLoanOffer({ id: 'offer-col-3', maxPrincipalPerLoan: 500000, remainingCapacity: 500000 })
    const smallCapBuilding: MockCollateralBuilding = {
      buildingId: 'small-1',
      buildingName: 'Small Shop',
      buildingType: 'SALES_SHOP',
      level: 1,
      appraisedValue: 100000,
      maxBorrowable: 70000,
      existingSecuredExposure: 0,
      remainingBorrowingCapacity: 70000,
      isEligible: true,
      ineligibilityReason: null,
    }
    const state = setupMockApi(page, { players: [player], loanOffers: [offer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.collateralBuildings = [smallCapBuilding]
    state.allBanks = [makeBankInfoEntry()]
    addBorrowerSettlementAccount(state)
    player.activeAccountType = 'COMPANY'

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await page.getByRole('button', { name: 'Request Loan' }).click()
    await expect(page).toHaveURL(/\/bank\/bank-building-1\/request-loan/)
    const form = page.locator('.loan-request-form-card')
    await expect(form).toBeVisible()

    // Select the small building — then set principal above its cap
    await form.locator('.collateral-option', { hasText: 'Small Shop' }).click()
    await form.getByRole('button', { name: 'Next' }).click()

    // Set principal above the 70% cap (70000)
    const principalInput = form.locator('#principal-amount')
    await principalInput.fill('90000')
    await principalInput.blur()

    // Warning should appear
    await expect(form.getByText('The requested amount exceeds 70% of the building')).toBeVisible()

    // While still on step 2, moving forward must be blocked
    await expect(form.getByRole('button', { name: 'Next' })).toBeDisabled()
  })

  test('borrower can choose duration ticks when requesting collateralized loan', async ({ page }) => {
    const player = makeCompanyPlayer()
    const offer = makeLoanOffer({ id: 'offer-col-duration', maxPrincipalPerLoan: 200000, remainingCapacity: 200000, durationTicks: 8760 })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer], myLoans: [] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.collateralBuildings = [eligibleBuilding]
    state.allBanks = [makeBankInfoEntry()]
    addBorrowerSettlementAccount(state)
    player.activeAccountType = 'COMPANY'

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await page.getByRole('button', { name: 'Request Loan' }).click()
    await expect(page).toHaveURL(/\/bank\/bank-building-1\/request-loan/)
    const form = page.locator('.loan-request-form-card')
    await expect(form).toBeVisible()

    await form.locator('.collateral-option', { hasText: 'Main Factory' }).click()
    await form.getByRole('button', { name: 'Next' }).click()
    await form.locator('#principal-amount').fill('60000')
    await form.getByRole('button', { name: 'Next' }).click()
    await form.locator('#duration-ticks').fill('240')
    await form.getByRole('button', { name: 'Next' }).click()
    await form.getByRole('button', { name: 'Request Loan' }).click()

    await expect(page).toHaveURL(/\/bank\/bank-building-1/)
    await expect.poll(() => state.myLoans.length).toBe(1)
    await expect.poll(() => state.myLoans[0]?.durationTicks ?? 0).toBe(240)
    await expect.poll(() => state.myLoans[0]?.totalPayments ?? 0).toBe(240)
  })

  test('ineligible building (already pledged) is shown as disabled', async ({ page }) => {
    const player = makeCompanyPlayer()
    const offer = makeLoanOffer({ id: 'offer-col-4', maxPrincipalPerLoan: 200000, remainingCapacity: 200000 })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.collateralBuildings = [eligibleBuilding, alreadyPledgedBuilding]
    state.allBanks = [makeBankInfoEntry()]
    player.activeAccountType = 'COMPANY'

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await page.getByRole('button', { name: 'Request Loan' }).click()
    await expect(page).toHaveURL(/\/bank\/bank-building-1\/request-loan/)
    const form = page.locator('.loan-request-form-card')
    await expect(form).toBeVisible()

    // The ineligible building should show in the list
    await expect(form.getByText('Old Shop')).toBeVisible()
    // The ineligible tag should be visible
    await expect(form.locator('.collateral-option.ineligible')).toBeVisible()
    await expect(form.getByText('Already pledged')).toBeVisible()
    // Its radio input should be disabled
    const disabledRadio = form.locator('.collateral-option.ineligible input[type="radio"]')
    await expect(disabledRadio).toBeDisabled()
  })

  test('secured loan shows pledged building badge in my loans list', async ({ page }) => {
    const player = makeCompanyPlayer()
    const offer = makeLoanOffer()
    const securedLoan: MockLoan = {
      id: 'secured-loan-1',
      loanOfferId: offer.id,
      borrowerCompanyId: 'borrower-co-1',
      borrowerCompanyName: 'My Company',
      lenderCompanyId: offer.lenderCompanyId,
      lenderCompanyName: offer.lenderCompanyName,
      bankBuildingId: offer.bankBuildingId,
      bankBuildingName: offer.bankBuildingName,
      originalPrincipal: 120000,
      remainingPrincipal: 120000,
      annualInterestRatePercent: offer.annualInterestRatePercent,
      durationTicks: offer.durationTicks,
      startTick: 1,
      dueTick: 1441,
      nextPaymentTick: 721,
      paymentAmount: 65000,
      paymentsMade: 0,
      totalPayments: 2,
      status: 'ACTIVE',
      missedPayments: 0,
      accumulatedPenalty: 0,
      acceptedAtUtc: '2026-01-01T00:00:00Z',
      closedAtUtc: null,
      collateralBuildingId: 'factory-1',
      collateralBuildingName: 'Main Factory',
      collateralAppraisedValue: 400000,
    }
    const state = setupMockApi(page, { players: [player], loanOffers: [offer], myLoans: [securedLoan] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    player.activeAccountType = 'COMPANY'

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/loans')

    await expect(page.getByRole('heading', { name: 'My Loans' })).toBeVisible()

    // Collateral badge should show the pledged building name
    await expect(page.locator('.collateral-badge')).toBeVisible()
    await expect(page.locator('.collateral-badge')).toContainText('Main Factory')
    await expect(page.locator('.collateral-badge')).toContainText('Secured Loan')
    // Appraised value should also appear
    await expect(page.locator('.collateral-badge')).toContainText('$400,000')
  })

  test('accepting secured loan and it appears with collateral badge', async ({ page }) => {
    const player = makeCompanyPlayer()
    const offer = makeLoanOffer({ id: 'offer-col-5', maxPrincipalPerLoan: 200000, remainingCapacity: 200000 })
    const state = setupMockApi(page, { players: [player], loanOffers: [offer] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.collateralBuildings = [eligibleBuilding]
    state.allBanks = [makeBankInfoEntry()]
    addBorrowerSettlementAccount(state)
    player.activeAccountType = 'COMPANY'

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    await page.getByRole('button', { name: 'Request Loan' }).click()
    await expect(page).toHaveURL(/\/bank\/bank-building-1\/request-loan/)
    const form = page.locator('.loan-request-form-card')
    await expect(form).toBeVisible()

    // Select Main Factory as collateral
    await form.locator('.collateral-option', { hasText: 'Main Factory' }).click()
    await form.getByRole('button', { name: 'Next' }).click()
    await form.locator('#principal-amount').fill('120000')
    await form.getByRole('button', { name: 'Next' }).click()
    await form.getByRole('button', { name: 'Next' }).click()

    // Accept the loan
    await form.getByRole('button', { name: 'Request Loan' }).click()
    await expect(page).toHaveURL(/\/bank\/bank-building-1/)

    // Secured loan badge should appear on the created loan
    await expect(page.getByRole('heading', { name: 'My Loans' })).toBeVisible()
    await expect(page.locator('.collateral-badge')).toBeVisible()
    await expect(page.locator('.collateral-badge')).toContainText('Main Factory')
  })

  test('fresh onboarding company can borrow from the government bank and repay over 10 ticks', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.allBanks = [
      makeBankInfoEntry({
        bankBuildingId: 'government-bank-1',
        bankBuildingName: 'Government Bank',
        lenderCompanyId: 'government-company',
        lenderCompanyName: 'Government',
        annualInterestRatePercent: undefined,
        lendingInterestRatePercent: 12,
        availableLendingCapacity: 500000,
        lendableCapacity: 500000,
        totalDeposits: 555556,
        outstandingLoanPrincipal: 0,
      }),
    ]

    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/onboarding')
    await completeAuthenticatedOnboarding(page, 'Mortgage Works')

    const company = player.companies[0]
    expect(company).toBeTruthy()
    const factory = company!.buildings.find((building) => building.type === 'FACTORY')
    expect(factory).toBeTruthy()

    state.collateralBuildings = [
      {
        buildingId: factory.id,
        buildingName: factory.name,
        buildingType: factory.type,
        level: factory.level,
        appraisedValue: 180000,
        maxBorrowable: 126000,
        existingSecuredExposure: 0,
        remainingBorrowingCapacity: 126000,
        isEligible: true,
        ineligibilityReason: null,
      },
    ]
    state.myBankAccounts = [
      {
        id: 'mortgage-company-account-1',
        accountNumber: '1234567890123456',
        currencyCode: 'EUR',
        currencySymbol: '€',
        balance: 0,
        companyId: company!.id,
        companyName: company!.name,
        ownerType: 'COMPANY',
        ownerDisplayName: company!.name,
        cityId: 'city-ba',
      },
    ]
    state.bankStatementRows[company!.id] = []

    await page.goto('/banking')
    await expect(page.getByText('Government Bank')).toBeVisible()
    await page.locator('.bank-borrow-card', { hasText: 'Government Bank' }).getByRole('link', { name: 'Visit Bank to Borrow' }).click()
    await expect(page).toHaveURL(/\/bank\/government-bank-1/)

    await page.getByRole('button', { name: 'Request Loan' }).click()
    await expect(page).toHaveURL(/\/bank\/government-bank-1\/request-loan/)
    const form = page.locator('.loan-request-form-card')
    await expect(form).toBeVisible()
    await form.locator('.collateral-option', { hasText: factory.name }).click()
    await form.getByRole('button', { name: 'Next' }).click()
    await form.locator('#principal-amount').fill('100000')
    await form.getByRole('button', { name: 'Next' }).click()
    await form.getByRole('button', { name: 'Next' }).click()
    await form.getByRole('button', { name: 'Request Loan' }).click()
    await expect(page).toHaveURL(/\/bank\/government-bank-1/)

    const createdLoan = state.myLoans[0]
    expect(createdLoan).toBeTruthy()

    createdLoan!.durationTicks = 10
    createdLoan!.totalPayments = 10
    createdLoan!.paymentAmount = Number(computeAmortizedTickPayment(createdLoan!.originalPrincipal, createdLoan!.annualInterestRatePercent, 10).toFixed(2))
    createdLoan!.dueTick = createdLoan!.startTick + 10
    createdLoan!.nextPaymentTick = createdLoan!.startTick + 1

    appendBankStatementRow(state.bankStatementRows[company!.id]!, {
      id: `${createdLoan!.id}-origination`,
      recordedAtTick: createdLoan!.startTick,
      recordedAtUtc: createdLoan!.acceptedAtUtc,
      description: 'Loan origination from Government Bank',
      category: 'LOAN_ORIGINATION',
      amount: createdLoan!.originalPrincipal,
      buildingId: factory!.id,
      buildingName: factory!.name,
    })
    state.myBankAccounts[0]!.balance = createdLoan!.originalPrincipal

    const firstPayment = applyMockLoanTickPayment(state, createdLoan!.id, 'mortgage-company-account-1', company!.id, factory!.name)
    await page.reload()
    await expect(page.locator('.loan-row', { hasText: 'REPAID' })).toHaveCount(0)
    await expect.poll(() => state.myLoans[0]?.remainingPrincipal ?? -1).toBe(firstPayment.remainingPrincipal)

    await page.goto('/bank-statement/mortgage-company-account-1')
    await expect(page.getByRole('heading', { name: /Bank Statement Review/i })).toBeVisible()
    await expect(page.getByText('Loan interest payment 1')).toBeVisible()
    await expect(page.getByText(`€${new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(firstPayment.interestAmount)}`)).toBeVisible()
    await expect(page.getByText('Loan principal payment 1')).toBeVisible()
    await expect(page.getByText(`€${new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(firstPayment.principalAmount)}`)).toBeVisible()

    for (let paymentIndex = createdLoan!.paymentsMade; paymentIndex < 10; paymentIndex += 1) {
      applyMockLoanTickPayment(state, createdLoan!.id, 'mortgage-company-account-1', company!.id, factory!.name)
    }

    await page.goto('/bank/government-bank-1')
    expect(state.myLoans[0]?.remainingPrincipal).toBe(0)
    await expect(page.locator('.loan-row', { hasText: 'REPAID' })).toBeVisible()
    await expect(page.locator('.loan-row', { hasText: factory!.name })).toContainText('REPAID')

    await page.goto('/bank-statement/mortgage-company-account-1')
    await expect(page.getByText('Loan principal payment 10')).toBeVisible()
    await expect(page.getByText('Loan interest payment 10')).toBeVisible()
  })
})

test.describe('Banking ownership — dashboard link and activation flow', () => {
  test('dashboard routes bank building link to /bank/:id instead of /building/:id', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/dashboard')

    // Click the Buildings tab so the buildings list becomes visible
    await page.getByRole('tab', { name: 'Buildings' }).click()

    // The bank building card in the dashboard should link to /bank/:id
    const companyCard = page.locator('.company-card').first()
    const bankCard = companyCard.locator('.building-card', { hasText: 'City Bank' })
    await expect(bankCard).toBeVisible()
    const href = await bankCard.getAttribute('href')
    expect(href).toMatch(/\/bank\/bank-building-1/)
    expect(href).not.toMatch(/\/building\/bank-building-1/)
  })

  test('bank management page shows base deposit required UI when not yet activated', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    // Add the bank to allBanks with baseCapitalDeposited = false to simulate unactivated state
    const state = setupMockApi(page, {
      players: [player],
      allBanks: [
        {
          bankBuildingId: 'bank-building-1',
          bankBuildingName: 'City Bank',
          cityId: 'city-ba',
          cityName: 'Bratislava',
          lenderCompanyId: 'lender-company-1',
          lenderCompanyName: 'Lending Corp',
          depositInterestRatePercent: 5,
          lendingInterestRatePercent: 12,
          totalDeposits: 0,
          lendableCapacity: 0,
          outstandingLoanPrincipal: 0,
          availableLendingCapacity: 0,
          baseCapitalDeposited: false,
          centralBankDebt: 0,
          centralBankInterestRatePercent: 2,
          reserveRequirement: 0,
          availableCash: 500_000,
          reserveShortfall: 0,
          liquidityStatus: 'HEALTHY' as const,
          cityCurrencyCode: 'EUR',
          cityCurrencySymbol: '€',
          baseCapitalRequirement: 10_000_000,
        },
      ],
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    // Should show base deposit required UI
    await expect(page.getByRole('heading', { name: 'Base Capital Deposit Required' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Make Base Deposit' })).toBeVisible()

    // Rates section is now visible even before activation so the owner can pre-configure
    await expect(page.getByText('Bank Rates Configuration')).toBeVisible()
    // Legacy loan-offer controls are gone even before activation.
    await expect(page.getByRole('button', { name: 'Publish Loan Offer' })).toBeHidden()
  })

  test('base deposit button activates the bank and shows management view', async ({ page }) => {
    const player = makeBankOwnerPlayer()
    // Set high cash so the deposit can succeed (company is at index 0)
    player.companies[0]!.cash = 15_000_000
    const state = setupMockApi(page, {
      players: [player],
      allBanks: [
        {
          bankBuildingId: 'bank-building-1',
          bankBuildingName: 'City Bank',
          cityId: 'city-ba',
          cityName: 'Bratislava',
          lenderCompanyId: 'lender-company-1',
          lenderCompanyName: 'Lending Corp',
          depositInterestRatePercent: 5,
          lendingInterestRatePercent: 12,
          totalDeposits: 0,
          lendableCapacity: 0,
          outstandingLoanPrincipal: 0,
          availableLendingCapacity: 0,
          baseCapitalDeposited: false,
          centralBankDebt: 0,
          centralBankInterestRatePercent: 2,
          reserveRequirement: 0,
          availableCash: 15_000_000,
          reserveShortfall: 0,
          liquidityStatus: 'HEALTHY' as const,
          cityCurrencyCode: 'EUR',
          cityCurrencySymbol: '€',
          baseCapitalRequirement: 10_000_000,
        },
      ],
    })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    await page.goto('/bank/bank-building-1')

    // Should show the base deposit button with dynamic label (no hardcoded amount)
    const depositBtn = page.getByRole('button', { name: 'Make Base Deposit' })
    await expect(depositBtn).toBeVisible()

    // Click the button
    await depositBtn.click()

    // After the mutation, the page should show the activated bank management view
    await expect(page.getByRole('heading', { name: 'Configure Bank' })).toBeVisible()
    await expect(page.getByText('Bank Rates Configuration')).toBeVisible()
  })
})
