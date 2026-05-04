import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer, makeDefaultCities } from '../../helpers/mock-api'

async function loginAsPlayer(page: Parameters<typeof test>[0]['page'], token: string) {
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

/**
 * A player with NO mock companies so the dashboard renders in person-account mode
 * (which is where the "Launch New Company" CTA lives). The prerequisites object
 * simulates eligibility without needing an actual company in the local mock state.
 */
function makePersonModePlayer() {
  return makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    personalCash: 500_000,
    companies: [],
  })
}

const ELIGIBLE_PREREQUISITES = {
  allRequirementsMet: true,
  companyCount: 1,
  underMaxCap: true,
  hasExistingCompany: true,
  companyAgeTicks: 9000,
  companyAgeRequirementMet: true,
  ticksUntilAgeRequirementMet: 0,
  netIncomeInWindow: 50000,
  profitabilityRequirementMet: true,
  personalBalanceUsd: 500000,
  balanceRequirementMet: true,
}

const INELIGIBLE_PREREQUISITES = {
  allRequirementsMet: false,
  companyCount: 1,
  underMaxCap: true,
  hasExistingCompany: true,
  companyAgeTicks: 1000,
  companyAgeRequirementMet: false,
  ticksUntilAgeRequirementMet: 7760,
  netIncomeInWindow: 0,
  profitabilityRequirementMet: false,
  personalBalanceUsd: 50000,
  balanceRequirementMet: false,
}

test.describe('Launch New Company — CTA visibility and prerequisites', () => {
  test('CTA is disabled when prerequisites not met', async ({ page }) => {
    const player = makePersonModePlayer()
    const state = setupMockApi(page, { players: [player], cities: makeDefaultCities() })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.additionalCompanyPrerequisites = INELIGIBLE_PREREQUISITES

    await loginAsPlayer(page, `token-${player.id}`)
    await page.goto('/dashboard')
    await page.waitForURL('/dashboard')

    // Person-account mode panel should be visible
    await expect(page.getByRole('heading', { name: 'Founder view' })).toBeVisible()

    // CTA button should be disabled
    const ctaBtn = page.locator('.launch-new-company-btn')
    await expect(ctaBtn).toBeVisible()
    await expect(ctaBtn).toBeDisabled()
  })

  test('CTA is enabled when prerequisites met', async ({ page }) => {
    const player = makePersonModePlayer()
    const state = setupMockApi(page, { players: [player], cities: makeDefaultCities() })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.additionalCompanyPrerequisites = ELIGIBLE_PREREQUISITES

    await loginAsPlayer(page, `token-${player.id}`)
    await page.goto('/dashboard')
    await page.waitForURL('/dashboard')

    await expect(page.getByRole('heading', { name: 'Founder view' })).toBeVisible()

    const ctaBtn = page.locator('.launch-new-company-btn')
    await expect(ctaBtn).toBeVisible()
    await expect(ctaBtn).toBeEnabled()
  })

  test('shows prerequisite checklist when requirements not met', async ({ page }) => {
    const player = makePersonModePlayer()
    const state = setupMockApi(page, { players: [player], cities: makeDefaultCities() })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.additionalCompanyPrerequisites = INELIGIBLE_PREREQUISITES

    await loginAsPlayer(page, `token-${player.id}`)
    await page.goto('/dashboard')
    await page.waitForURL('/dashboard')

    await expect(page.getByRole('heading', { name: 'Founder view' })).toBeVisible()
    const prereqList = page.locator('.nc-prereq-list')
    await expect(prereqList).toBeVisible()
    await expect(prereqList.locator('li')).not.toHaveCount(0)
  })
})

test.describe('Launch New Company — modal wizard', () => {
  test('clicking CTA opens the modal', async ({ page }) => {
    const player = makePersonModePlayer()
    const state = setupMockApi(page, { players: [player], cities: makeDefaultCities() })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.additionalCompanyPrerequisites = ELIGIBLE_PREREQUISITES

    await loginAsPlayer(page, `token-${player.id}`)
    await page.goto('/dashboard')
    await page.waitForURL('/dashboard')

    const ctaBtn = page.locator('.launch-new-company-btn')
    await expect(ctaBtn).toBeEnabled()
    await ctaBtn.click()

    await expect(page.locator('.new-company-modal')).toBeVisible()
    await expect(page.locator('.new-company-step1')).toBeVisible()
  })

  test('Next is disabled until company name has ≥3 chars and a city is selected', async ({ page }) => {
    const player = makePersonModePlayer()
    const state = setupMockApi(page, { players: [player], cities: makeDefaultCities() })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.additionalCompanyPrerequisites = ELIGIBLE_PREREQUISITES

    await loginAsPlayer(page, `token-${player.id}`)
    await page.goto('/dashboard')
    await page.waitForURL('/dashboard')

    await page.locator('.launch-new-company-btn').click()
    await expect(page.locator('.new-company-step1')).toBeVisible()

    const nextBtn = page.locator('.new-company-step1').getByRole('button', { name: 'Next' })

    // Empty name → disabled
    await expect(nextBtn).toBeDisabled()

    // Short name, no city → still disabled
    await page.locator('#nc-name').fill('AB')
    await expect(nextBtn).toBeDisabled()

    // Valid name but no city → still disabled
    await page.locator('#nc-name').fill('Venture Corp')
    await expect(nextBtn).toBeDisabled()
  })

  test('step 1 advances to step 2 after filling name and city', async ({ page }) => {
    const player = makePersonModePlayer()
    const state = setupMockApi(page, { players: [player], cities: makeDefaultCities() })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.additionalCompanyPrerequisites = ELIGIBLE_PREREQUISITES

    await loginAsPlayer(page, `token-${player.id}`)
    await page.goto('/dashboard')
    await page.waitForURL('/dashboard')

    await page.locator('.launch-new-company-btn').click()
    await expect(page.locator('.new-company-step1')).toBeVisible()

    await page.locator('#nc-name').fill('Venture Corp')
    await page.locator('#nc-city').selectOption({ index: 1 })

    const nextBtn = page.locator('.new-company-step1').getByRole('button', { name: 'Next' })
    await expect(nextBtn).toBeEnabled()
    await nextBtn.click()

    await expect(page.locator('.new-company-step2')).toBeVisible()
  })

  test('step 2 shows three IPO tier cards', async ({ page }) => {
    const player = makePersonModePlayer()
    const state = setupMockApi(page, { players: [player], cities: makeDefaultCities() })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.additionalCompanyPrerequisites = ELIGIBLE_PREREQUISITES

    await loginAsPlayer(page, `token-${player.id}`)
    await page.goto('/dashboard')
    await page.waitForURL('/dashboard')

    await page.locator('.launch-new-company-btn').click()
    await page.locator('#nc-name').fill('IPO Corp')
    await page.locator('#nc-city').selectOption({ index: 1 })
    await page.locator('.new-company-step1').getByRole('button', { name: 'Next' }).click()

    await expect(page.locator('.new-company-step2')).toBeVisible()
    const tierCards = page.locator('.nc-ipo-tier-card')
    await expect(tierCards).toHaveCount(3)
  })

  test('successful submission closes the modal', async ({ page }) => {
    const player = makePersonModePlayer()
    const state = setupMockApi(page, { players: [player], cities: makeDefaultCities() })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    state.additionalCompanyPrerequisites = ELIGIBLE_PREREQUISITES

    await loginAsPlayer(page, `token-${player.id}`)
    await page.goto('/dashboard')
    await page.waitForURL('/dashboard')

    await page.locator('.launch-new-company-btn').click()
    await page.locator('#nc-name').fill('Galaxy Corp')
    await page.locator('#nc-city').selectOption({ index: 1 })
    await page.locator('.new-company-step1').getByRole('button', { name: 'Next' }).click()
    await expect(page.locator('.new-company-step2')).toBeVisible()

    await page.locator('.new-company-step2').getByRole('button', { name: /Launch Company/ }).click()

    // Modal should close after success
    await expect(page.locator('.new-company-modal')).not.toBeVisible({ timeout: 5000 })
  })
})
