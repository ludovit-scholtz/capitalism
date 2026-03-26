/**
 * Onboarding flow E2E tests.
 * Covers: registration, login, onboarding wizard, and dashboard verification.
 */
import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from './helpers/mock-api'

test.describe('Authentication', () => {
  test('register new account and redirect to home', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/login')

    // Switch to register mode
    await page.getByRole('button', { name: 'Create Account' }).click()
    await expect(page.getByRole('heading', { name: 'Create Account' })).toBeVisible()

    // Fill form
    await page.getByLabel('Email').fill('new@test.com')
    await page.getByLabel('Display Name').fill('New Player')
    await page.getByLabel('Password').fill('TestPass1!')

    // Submit
    await page.getByRole('button', { name: 'Create Account' }).first().click()
    await page.waitForURL('/')
    await expect(page).toHaveURL('/')
  })

  test('login existing account', async ({ page }) => {
    const player = makePlayer({ email: 'existing@test.com', password: 'TestPass1!' })
    setupMockApi(page, { players: [player] })
    await page.goto('/login')

    await expect(page.getByRole('heading', { name: 'Sign In' })).toBeVisible()
    await page.getByLabel('Email').fill('existing@test.com')
    await page.getByLabel('Password').fill('TestPass1!')
    await page.getByRole('button', { name: 'Sign In' }).click()
    await page.waitForURL('/')
    await expect(page).toHaveURL('/')
  })

  test('login with wrong password shows error', async ({ page }) => {
    const player = makePlayer({ email: 'existing@test.com', password: 'TestPass1!' })
    setupMockApi(page, { players: [player] })
    await page.goto('/login')

    await page.getByLabel('Email').fill('existing@test.com')
    await page.getByLabel('Password').fill('WrongPass!')
    await page.getByRole('button', { name: 'Sign In' }).click()
    await expect(page.getByRole('alert')).toBeVisible()
  })

  test('toggle between login and register forms', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/login')

    // Initially login
    await expect(page.getByRole('heading', { name: 'Sign In' })).toBeVisible()

    // Switch to register
    await page.getByRole('button', { name: 'Create Account' }).click()
    await expect(page.getByRole('heading', { name: 'Create Account' })).toBeVisible()
    await expect(page.getByLabel('Display Name')).toBeVisible()

    // Switch back to login
    await page.getByRole('button', { name: 'Sign In' }).click()
    await expect(page.getByRole('heading', { name: 'Sign In' })).toBeVisible()
  })
})

test.describe('Onboarding wizard', () => {
  test('complete full onboarding flow', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })

    // Authenticate first
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.goto('/onboarding')

    // Step 1: Choose industry
    await expect(page.getByRole('heading', { name: 'Start Your Empire' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Choose Your Industry' })).toBeVisible()

    // Select Furniture industry
    await page.locator('.industry-card', { hasText: 'Furniture' }).click()
    await page.getByRole('button', { name: 'Next' }).click()

    // Step 2: Choose city
    await expect(page.getByRole('heading', { name: 'Choose Your City' })).toBeVisible()
    await page.locator('.city-card', { hasText: 'Bratislava' }).click()
    await page.getByRole('button', { name: 'Next' }).click()

    // Step 3: Name company and pick product
    await expect(page.getByRole('heading', { name: 'Name Your Company & Pick a Product' })).toBeVisible()
    await page.getByLabel('Company Name').fill('My Empire Inc')
    await page.locator('.product-card', { hasText: 'Wooden Chair' }).click()

    // Verify summary appears
    await expect(page.getByText('My Empire Inc')).toBeVisible()
    await expect(page.getByText('Bratislava')).toBeVisible()

    // Complete onboarding
    await page.getByRole('button', { name: 'Start Playing' }).click()
    await page.waitForURL('/dashboard')
    await expect(page).toHaveURL('/dashboard')
  })

  test('step 1 Next button disabled until industry selected', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/onboarding')
    await expect(page.getByRole('heading', { name: 'Choose Your Industry' })).toBeVisible()

    // Next button should be disabled
    await expect(page.getByRole('button', { name: 'Next' })).toBeDisabled()

    // Select industry
    await page.locator('.industry-card', { hasText: 'Furniture' }).click()
    await expect(page.getByRole('button', { name: 'Next' })).toBeEnabled()
  })

  test('back button navigates to previous step', async ({ page }) => {
    const player = makePlayer()
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/onboarding')

    // Go to step 2
    await page.locator('.industry-card', { hasText: 'Furniture' }).click()
    await page.getByRole('button', { name: 'Next' }).click()
    await expect(page.getByRole('heading', { name: 'Choose Your City' })).toBeVisible()

    // Back to step 1
    await page.getByRole('button', { name: 'Back' }).click()
    await expect(page.getByRole('heading', { name: 'Choose Your Industry' })).toBeVisible()
  })

  test('redirects to login if not authenticated', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/onboarding')
    await page.waitForURL('/login')
    await expect(page).toHaveURL('/login')
  })
})

test.describe('Dashboard', () => {
  test('shows empty state with onboarding link when no companies', async ({ page }) => {
    const player = makePlayer({ companies: [] })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/dashboard')
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
    await expect(page.getByText('You have no companies yet')).toBeVisible()
    await expect(page.getByRole('link', { name: 'Start Onboarding' })).toBeVisible()
  })

  test('shows company card after onboarding', async ({ page }) => {
    const player = makePlayer()
    player.companies.push({
      id: 'comp-1',
      playerId: player.id,
      name: 'Test Corp',
      cash: 500000,
      foundedAtUtc: '2026-01-01T00:00:00Z',
      buildings: [
        { id: 'b1', companyId: 'comp-1', cityId: 'city-ba', type: 'FACTORY', name: 'Test Corp Factory', latitude: 48.15, longitude: 17.11, level: 1, powerConsumption: 2, isForSale: false, units: [] },
      ],
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`

    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/dashboard')
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
    const companyCard = page.locator('.company-card').first()
    await expect(companyCard.getByRole('heading', { name: 'Test Corp' })).toBeVisible()
    await expect(companyCard.getByText('$500,000')).toBeVisible()
    await expect(companyCard.locator('.building-type', { hasText: 'FACTORY' })).toBeVisible()
  })

  test('redirects to login if not authenticated', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/dashboard')
    await page.waitForURL('/login')
    await expect(page).toHaveURL('/login')
  })
})

test.describe('Full onboarding journey', () => {
  test('register → onboard → dashboard shows new company', async ({ page }) => {
    setupMockApi(page)

    // Start at home, click Get Started
    await page.goto('/')
    await page.getByRole('link', { name: 'Get Started' }).click()
    await page.waitForURL('/login')

    // Register
    await page.getByRole('button', { name: 'Create Account' }).click()
    await page.getByLabel('Email').fill('journey@test.com')
    await page.getByLabel('Display Name').fill('Journey Player')
    await page.getByLabel('Password').fill('TestPass1!')
    await page.getByRole('button', { name: 'Create Account' }).first().click()
    await page.waitForURL('/')

    // Go to onboarding
    await page.goto('/onboarding')

    // Step 1: Choose industry
    await page.locator('.industry-card', { hasText: 'Furniture' }).click()
    await page.getByRole('button', { name: 'Next' }).click()

    // Step 2: Choose city
    await page.locator('.city-card', { hasText: 'Bratislava' }).click()
    await page.getByRole('button', { name: 'Next' }).click()

    // Step 3: Company name + product
    await page.getByLabel('Company Name').fill('Journey Corp')
    await page.locator('.product-card', { hasText: 'Wooden Chair' }).click()

    // Complete
    await page.getByRole('button', { name: 'Start Playing' }).click()
    await page.waitForURL('/dashboard')

    // Verify dashboard has company
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
    await expect(page.locator('.company-card').first().getByRole('heading', { name: 'Journey Corp' })).toBeVisible()
  })
})
