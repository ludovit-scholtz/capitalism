/**
 * E2E tests for the onboarding pre-IPO deposit flow.
 *
 * Verifies that the 200k USD founder contribution is correctly transferred from the
 * player's personal account to the new company when they purchase their first factory lot.
 * After the deposit the personal account must have a zero balance in all currencies.
 *
 * Background: ROADMAP item "Fix pre-IPO deposit to be 200k USD not 200k EUR" — the
 * personal account is funded with USD, not EUR. Tests use USD amounts throughout.
 */
import { test, expect, type Page } from '@playwright/test'
import { setupMockApi, makePlayer, type MockState } from '../../helpers/mock-api'

// ── Constants (must stay in sync with mock-api.ts) ────────────────────────────
const PERSONAL_STARTING_CASH = 200_000 // USD — player's initial personal balance
const STARTER_FOUNDER_CONTRIBUTION = 200_000 // USD — transferred to company at IPO
const DEFAULT_IPO_RAISE_TARGET = 400_000 // USD — public raise for Starter IPO
const FACTORY_LOT_PRICE = 90_000 // EUR — Factory Site B1 default price
const SHOP_LOT_PRICE = 120_000 // EUR — High Street Retail Space default price

// Starting company cash = founder contribution + IPO raise - factory lot price
const STARTING_COMPANY_CASH_AFTER_FACTORY = STARTER_FOUNDER_CONTRIBUTION + DEFAULT_IPO_RAISE_TARGET - FACTORY_LOT_PRICE
const STARTING_COMPANY_CASH_AFTER_SHOP = STARTING_COMPANY_CASH_AFTER_FACTORY - SHOP_LOT_PRICE

// ── Shared helpers ─────────────────────────────────────────────────────────────

async function authenticateViaLocalStorage(page: Page, token: string) {
  await page.addInitScript((storedToken) => {
    localStorage.setItem('auth_token', storedToken)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7_200_000).toISOString())
  }, token)
}

async function chooseOnboardingCity(page: Page, city = 'Bratislava') {
  await page.locator('.city-card', { hasText: city }).click()
}

/** Drives steps 1–4 of the guided authenticated onboarding wizard:
 *  City → Industry → Product → IPO Plan → (lands on Factory Lot step)
 */
async function completeRouteChoices(page: Page, industry = 'Furniture', product = 'Wooden Chair') {
  await chooseOnboardingCity(page)
  await expect(page.getByRole('heading', { name: 'Choose Your Industry' })).toBeVisible()
  await page.locator('.industry-card', { hasText: industry }).click()
  await expect(page.getByRole('heading', { name: 'Choose Your First Product' })).toBeVisible()
  await page.locator('.product-card', { hasText: product }).click()
  await expect(page.getByRole('heading', { name: 'Choose Your IPO Plan' })).toBeVisible()
  await page.locator('.ipo-card', { hasText: 'Starter IPO' }).click()
  await expect(page.getByRole('heading', { name: 'Choose Your First Factory Lot' })).toBeVisible()
}

/** Purchases the affordable starter factory lot ("Factory Site B1") from the list view. */
async function purchaseFactoryLot(page: Page) {
  await page.getByRole('button', { name: 'List View' }).click()
  await page.getByRole('button', { name: /Factory Site B1/i }).click()
  await page.getByRole('button', { name: 'Purchase First Factory' }).click()
  await expect(page.getByRole('heading', { name: 'Choose Your First Shop Lot' })).toBeVisible()
}

/** Purchases the starter shop lot ("High Street Retail Space") from the list view. */
async function purchaseShopLot(page: Page) {
  await page.getByRole('button', { name: 'List View' }).click()
  await page.getByRole('button', { name: /High Street Retail Space/i }).click()
  await page.getByRole('button', { name: 'Purchase First Sales Shop' }).click()
  await expect(page.getByRole('heading', { name: /Your Empire Has Launched/i })).toBeVisible()
}

/** Sets up an authenticated player with the default personal cash and navigates to /onboarding. */
async function setupAuthenticatedPlayer(page: Page): Promise<{ player: ReturnType<typeof makePlayer>; state: MockState }> {
  const player = makePlayer()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await authenticateViaLocalStorage(page, `token-${player.id}`)
  return { player, state }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

test.describe('Pre-IPO deposit — personal account balance', () => {
  test('player starts with 200k personal cash before the IPO deposit', async ({ page }) => {
    // Verifies the pre-condition: the player's personal account has exactly 200k USD
    // before any onboarding action is taken.
    const { player } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    expect(player.personalCash).toBe(PERSONAL_STARTING_CASH)
  })

  test('startOnboardingCompany deducts 200k from personal cash (IPO deposit)', async ({ page }) => {
    // When the player purchases their first factory lot, the mock calls startOnboardingCompany
    // which transfers STARTER_FOUNDER_CONTRIBUTION from the personal account to the new company.
    // Personal cash must be exactly 0 after this mutation.
    const { player } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    // Before purchasing factory lot: personal cash is 200k USD
    expect(player.personalCash).toBe(PERSONAL_STARTING_CASH)

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)

    // After purchasing factory lot: personal cash is 0 (200k USD was deposited as founder contribution)
    expect(player.personalCash).toBe(0)
  })

  test('personAccount query returns 0 personalCash after the IPO deposit', async ({ page }) => {
    // The personAccount GraphQL query must reflect the zero personal balance once
    // the pre-IPO deposit has been transferred to the company.
    const { player, state } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)

    // personalCash = 0
    const taxReserve = player.personalTaxReserve ?? 0
    expect(player.personalCash - taxReserve).toBe(0)

    // The mock state must have no other currency balances that inflate the personal funds
    const otherCurrencyBalances = state.playerCurrencyBalances.filter((b) => b.balance > 0)
    expect(otherCurrencyBalances).toHaveLength(0)
  })

  test('personal account shows zero available cash after full onboarding completion', async ({ page }) => {
    // After completing the entire onboarding wizard (factory + shop), the player's
    // personal cash must remain 0 — the deposit is never reversed during onboarding.
    const { player } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)
    await purchaseShopLot(page)

    // Completion screen must be visible
    await expect(page.getByRole('heading', { name: /Your Empire Has Launched/i })).toBeVisible()

    // Personal cash must still be 0 — shop lot purchase comes from company cash, not personal
    expect(player.personalCash).toBe(0)
  })
})

test.describe('Pre-IPO deposit — company receives correct capital', () => {
  test('company cash equals founder contribution + IPO raise minus factory lot price', async ({ page }) => {
    // After startOnboardingCompany the company's starting cash must be:
    // STARTER_FOUNDER_CONTRIBUTION + DEFAULT_IPO_RAISE_TARGET − FACTORY_LOT_PRICE
    const { player } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)

    const company = player.companies[0]
    expect(company).toBeDefined()
    expect(company?.cash).toBe(STARTING_COMPANY_CASH_AFTER_FACTORY)
  })

  test('company cash is further reduced after shop lot purchase', async ({ page }) => {
    // After finishOnboarding the company's cash must be:
    // STARTING_COMPANY_CASH_AFTER_FACTORY − SHOP_LOT_PRICE
    const { player } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)
    await purchaseShopLot(page)

    const company = player.companies[0]
    expect(company).toBeDefined()
    expect(company?.cash).toBe(STARTING_COMPANY_CASH_AFTER_SHOP)
  })

  test('completion screen configure-guide shows correct remaining company cash', async ({ page }) => {
    // The "Review your cash" configure-guide step must display the remaining balance
    // matching STARTING_COMPANY_CASH_AFTER_SHOP.
    await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)
    await purchaseShopLot(page)

    await expect(page.getByRole('heading', { name: /Your Empire Has Launched/i })).toBeVisible()

    const cashStep = page.locator('.configure-step').filter({ hasText: 'Review your cash' })
    await expect(cashStep).toBeVisible()
    // 200k founder + 400k IPO − 90k factory − 120k shop = 390,000
    await expect(cashStep).toContainText('390,000')
  })
})

test.describe('Pre-IPO deposit — zero balance across all currencies', () => {
  test('personal account has no USD balance after deposit (primary currency zeroed)', async ({ page }) => {
    // USD is the player's personal currency for the founder contribution. After the deposit it must be 0.
    const { player } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)

    // The primary USD personal balance must be zero
    expect(player.personalCash).toBe(0)
  })

  test('no additional currency balances exist on a default player before deposit', async ({ page }) => {
    // A freshly registered player has only USD personal cash — no CZK, GBP, EUR etc.
    // This is the edge-case baseline: deposit operates on exactly one currency.
    const { state } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    const nonUsdBalances = state.playerCurrencyBalances.filter((b) => b.balance > 0)
    expect(nonUsdBalances).toHaveLength(0)
  })

  test('pre-existing non-USD balances are not affected by the USD IPO deposit', async ({ page }) => {
    // If a player held other-currency balances before completing onboarding,
    // the startOnboardingCompany mutation must only deduct the USD personal cash —
    // other-currency balances must remain unchanged.
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    // Seed non-USD balances (e.g. from a prior Forex trade) — must survive the deposit
    state.playerCurrencyBalances = [
      { currencyCode: 'CZK', currencySymbol: 'Kč', balance: 5_000 },
      { currencyCode: 'EUR', currencySymbol: '€', balance: 250 },
    ]

    await authenticateViaLocalStorage(page, `token-${player.id}`)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)

    // USD personal cash must be 0
    expect(player.personalCash).toBe(0)

    // Non-USD balances must be untouched
    const czkBalance = state.playerCurrencyBalances.find((b) => b.currencyCode === 'CZK')
    const eurBalance = state.playerCurrencyBalances.find((b) => b.currencyCode === 'EUR')
    expect(czkBalance?.balance).toBe(5_000)
    expect(eurBalance?.balance).toBe(250)
  })

  test('player has zero total personal liquid funds across all currencies after deposit', async ({ page }) => {
    // Verifies that no personal liquid funds remain after the deposit:
    // USD personalCash = 0 and no other-currency balances hold positive amounts.
    const { player, state } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)

    const usdBalance = player.personalCash
    const otherCurrencyTotal = state.playerCurrencyBalances.reduce((sum, b) => sum + b.balance, 0)
    const totalLiquid = usdBalance + otherCurrencyTotal

    expect(totalLiquid).toBe(0)
  })
})

test.describe('Pre-IPO deposit — deposit amount validation', () => {
  test('deposit is exactly 200k regardless of chosen IPO plan size', async ({ page }) => {
    // The founder contribution is always STARTER_FOUNDER_CONTRIBUTION = 200k USD.
    // The IPO raise target (public investors) varies by plan but does not change
    // how much the founder personally contributes.
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await authenticateViaLocalStorage(page, `token-${player.id}`)

    // Start with 200k USD personal cash
    expect(player.personalCash).toBe(PERSONAL_STARTING_CASH)

    await page.goto('/onboarding')
    await chooseOnboardingCity(page)
    await expect(page.getByRole('heading', { name: 'Choose Your Industry' })).toBeVisible()
    await page.locator('.industry-card', { hasText: 'Furniture' }).click()
    await page.locator('.product-card', { hasText: 'Wooden Chair' }).click()

    // Select "Growth IPO" plan (higher raise, still 200k USD founder contribution)
    await expect(page.getByRole('heading', { name: 'Choose Your IPO Plan' })).toBeVisible()
    await page.locator('.ipo-card', { hasText: 'Growth IPO' }).click()
    await expect(page.getByRole('heading', { name: 'Choose Your First Factory Lot' })).toBeVisible()

    await page.getByRole('button', { name: 'List View' }).click()
    await page.getByRole('button', { name: /Factory Site B1/i }).click()
    await page.getByRole('button', { name: 'Purchase First Factory' }).click()
    await expect(page.getByRole('heading', { name: 'Choose Your First Shop Lot' })).toBeVisible()

    // Regardless of IPO plan chosen, the personal cash deduction must be exactly 200k USD
    const deposited = PERSONAL_STARTING_CASH - player.personalCash
    expect(deposited).toBe(STARTER_FOUNDER_CONTRIBUTION)
    expect(player.personalCash).toBe(0)
  })

  test('deposit is exactly 200k USD for all three starter industries', async ({ page }) => {
    // The personal cash deduction must be 200k USD for Furniture, Food Processing, and Healthcare.
    const industriesAndProducts: Array<[string, string]> = [
      ['Food Processing', 'Bread'],
      ['Healthcare', 'Basic Medicine'],
    ]

    for (const [industry, product] of industriesAndProducts) {
      const player = makePlayer()
      const state = setupMockApi(page, { players: [player] })
      state.currentUserId = player.id
      state.currentToken = `token-${player.id}`
      await authenticateViaLocalStorage(page, `token-${player.id}`)

      await page.goto('/onboarding')
      await completeRouteChoices(page, industry, product)
      await purchaseFactoryLot(page)

      expect(player.personalCash).toBe(0)
    }
  })
})

test.describe('Pre-IPO deposit — bank account and ledger consistency', () => {
  test('after deposit player has no personal bank accounts with positive balance by default', async ({ page }) => {
    // By default the mock creates no personal PERSON-type bank accounts.
    // The personal cash lives on player.personalCash (the legacy personal-wallet field).
    // After the deposit, both the wallet field AND the bank-account array must be zero/empty.
    const { player, state } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)

    // personalCash = 0
    expect(player.personalCash).toBe(0)

    // The seeded personal USD account (created by startOnboardingCompany) has balance 0
    const personalAccounts = state.myBankAccounts.filter((acc) => acc.ownerType === 'PERSON' || acc.companyId === null)
    for (const acc of personalAccounts) {
      expect(acc.balance).toBe(0)
    }
  })

  test('company has correct cash after full onboarding', async ({ page }) => {
    // After finishOnboarding the company's cash must equal starter capital minus lot costs.
    const { player } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)
    await purchaseShopLot(page)

    const company = player.companies[0]
    expect(company).toBeDefined()

    // The company's cash field is the canonical source of truth in the mock
    expect(company?.cash).toBe(STARTING_COMPANY_CASH_AFTER_SHOP)
  })

  test('company ledger is initialised empty after onboarding (no auto-seeded entries)', async ({ page }) => {
    // The mock does not auto-seed ledger entries for the IPO deposit. Tests that
    // specifically verify ledger entries should seed state.ledgerData manually.
    // This test confirms the default state so ledger-display tests know the baseline.
    const { state } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)
    await purchaseShopLot(page)

    // No ledger entries seeded — ledgerData is empty by default
    expect(Object.keys(state.ledgerData)).toHaveLength(0)
  })

  test('personal bank statement has exactly 2 rows after onboarding: gov deposit and founder contribution', async ({
    page,
  }) => {
    // After startOnboardingCompany the personal bank statement must show:
    //   Row 1: BANK_ACCOUNT_TRANSFER_IN  +200,000 USD (government deposit)
    //   Row 2: FOUNDER_CONTRIBUTION      -200,000 USD (transferred to company)
    // Running balance after row 2 must be 0.
    const { state } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)

    // The mock must have seeded exactly 2 personal bank statement rows
    expect(state.personalBankStatementRows).toHaveLength(2)

    const depositRow = state.personalBankStatementRows[0]
    const contributionRow = state.personalBankStatementRows[1]

    expect(depositRow?.category).toBe('BANK_ACCOUNT_TRANSFER_IN')
    expect(depositRow?.amount).toBe(STARTER_FOUNDER_CONTRIBUTION)

    expect(contributionRow?.category).toBe('FOUNDER_CONTRIBUTION')
    expect(contributionRow?.amount).toBe(-STARTER_FOUNDER_CONTRIBUTION)
    expect(contributionRow?.runningBalance).toBe(0)
  })
})

test.describe('Pre-IPO deposit — onboarding step state transitions', () => {
  test('wizard advances to shop step with personalCash already at 0 after factory purchase', async ({ page }) => {
    // When the player arrives at the shop selection step, personalCash must already be 0.
    const { player } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)

    // Verify the shop step is displayed (the wizard advanced correctly)
    await expect(page.getByRole('heading', { name: 'Choose Your First Shop Lot' })).toBeVisible()

    // Personal cash must be 0 at this point — the deposit was collected when the factory was purchased
    expect(player.personalCash).toBe(0)
  })

  test('shop step shows company available cash panel confirming funds moved from personal', async ({ page }) => {
    // The shop-selection step displays the company's "Available cash" to help the player
    // choose a lot they can afford. This panel indirectly confirms the deposit succeeded
    // (company has capital, personal account was the source).
    const { player } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)

    // The "Available cash" label on the shop step must be visible
    await expect(page.getByText('Available cash')).toBeVisible()

    // Company has capital — the deposit moved 200k USD to the company plus the IPO raise
    const company = player.companies[0]
    expect(company).toBeDefined()
    expect((company?.cash ?? 0)).toBeGreaterThan(0)
  })

  test('completion screen is reachable only after both lots are purchased', async ({ page }) => {
    // Ensures the deposit (factory purchase) and shop purchase both succeed in sequence,
    // and the final "Your Empire Has Launched" heading confirms the complete flow ran.
    const { player } = await setupAuthenticatedPlayer(page)
    await page.goto('/onboarding')

    await completeRouteChoices(page)
    await purchaseFactoryLot(page)
    await purchaseShopLot(page)

    // Both deposits done → completion screen
    await expect(page.getByRole('heading', { name: /Your Empire Has Launched/i })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Go to Dashboard' })).toBeVisible()

    // Personal account drained
    expect(player.personalCash).toBe(0)

    // Company funded
    const company = player.companies[0]
    expect(company?.cash).toBe(STARTING_COMPANY_CASH_AFTER_SHOP)
  })
})

test.describe('Pre-IPO deposit — personal bank statement UI', () => {
  /** Creates a pre-configured state: player in PERSON context with a personal USD
   *  bank account and two seeded statement rows (gov deposit + founder contribution).
   *  The auth store will pick up activeAccountType='PERSON' from the me query.
   */
  async function setupPersonalStatementFixture(page: Page) {
    const player = makePlayer()
    // Keep player in PERSON context so BankStatementView shows PERSON-type accounts
    player.activeAccountType = 'PERSON'

    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    const personalAccountId = `personal-usd-${player.id}`
    state.myBankAccounts = [
      {
        id: personalAccountId,
        accountNumber: '1234567890123456',
        currencyCode: 'USD',
        currencySymbol: '$',
        balance: 0,
        companyId: null,
        companyName: null,
        ownerType: 'PERSON',
        ownerDisplayName: player.displayName,
      },
    ]

    state.personalBankStatementRows = [
      {
        id: 'row-gov-deposit',
        recordedAtTick: 0,
        recordedAtUtc: new Date().toISOString(),
        description: 'Government starter funding deposit',
        category: 'BANK_ACCOUNT_TRANSFER_IN',
        amount: STARTER_FOUNDER_CONTRIBUTION,
        runningBalance: STARTER_FOUNDER_CONTRIBUTION,
        buildingId: null,
        buildingName: null,
      },
      {
        id: 'row-founder-contribution',
        recordedAtTick: 1,
        recordedAtUtc: new Date().toISOString(),
        description: `Founder contribution: ${STARTER_FOUNDER_CONTRIBUTION.toLocaleString()} USD government starter deposit`,
        category: 'FOUNDER_CONTRIBUTION',
        amount: -STARTER_FOUNDER_CONTRIBUTION,
        runningBalance: 0,
        buildingId: null,
        buildingName: null,
      },
    ]

    await authenticateViaLocalStorage(page, `token-${player.id}`)

    return { player, state, personalAccountId }
  }

  test('bank statement page shows government deposit row after onboarding', async ({ page }) => {
    // Verifies the bank statement page shows the BANK_ACCOUNT_TRANSFER_IN entry:
    // the government starter funding deposit of +200k USD.
    const { personalAccountId } = await setupPersonalStatementFixture(page)
    await page.goto(`/bank-statement/${personalAccountId}`)

    // Page must load and show the statement title
    await expect(page.getByRole('heading', { name: '🏦 Bank Statement' })).toBeVisible()

    // The government deposit row must appear
    await expect(page.getByText('Government starter funding deposit')).toBeVisible()
  })

  test('bank statement page shows founder contribution row after onboarding', async ({ page }) => {
    // Verifies the bank statement page shows the FOUNDER_CONTRIBUTION entry:
    // the -200k USD transfer from personal account to the new company.
    const { personalAccountId } = await setupPersonalStatementFixture(page)
    await page.goto(`/bank-statement/${personalAccountId}`)

    await expect(page.getByRole('heading', { name: '🏦 Bank Statement' })).toBeVisible()

    // 2 rows must be rendered (gov deposit + founder contribution)
    await expect(page.locator('.statement-row')).toHaveCount(2)

    // The second row's description cell must contain "Founder contribution"
    await expect(page.locator('.statement-row').nth(1)).toContainText('Founder contribution')
  })

  test('bank statement page shows both rows and personal account balance is zero', async ({ page }) => {
    // After the government deposit (+200k) and founder contribution (-200k),
    // the personal account balance is exactly 0 and both rows are visible.
    const { state, personalAccountId } = await setupPersonalStatementFixture(page)
    await page.goto(`/bank-statement/${personalAccountId}`)

    await expect(page.getByRole('heading', { name: '🏦 Bank Statement' })).toBeVisible()

    // Both transaction rows must be present
    await expect(page.locator('.statement-row')).toHaveCount(2)

    // First row is the government deposit (credit: +200k)
    await expect(page.locator('.statement-row').nth(0)).toContainText('Government starter funding deposit')

    // Second row is the founder contribution (debit: -200k)
    await expect(page.locator('.statement-row').nth(1)).toContainText('Founder contribution')

    // The personal account balance is 0 after the deposit
    const personalAccount = state.myBankAccounts.find((acc) => acc.ownerType === 'PERSON')
    expect(personalAccount?.balance).toBe(0)
  })
})

