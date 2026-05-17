import { test, expect } from '@playwright/test'
import { setupMockApi, makeDefaultCities, makePlayer } from '../../helpers/mock-api'

test.describe('Country flag icons', () => {
  // ──────────────────────────────────────────────────
  // Cities page
  // ──────────────────────────────────────────────────

  test.describe('Cities page flags', () => {
    test('each city card shows a flag image element', async ({ page }) => {
      const state = setupMockApi(page)
      state.cities = makeDefaultCities()
      await page.goto('/cities')

      // Wait for cards to render
      await expect(page.locator('.city-card').first()).toBeVisible()

      // Every city card should contain a country-flag element
      const cards = page.locator('.city-card')
      const count = await cards.count()
      for (let i = 0; i < count; i++) {
        const flag = cards.nth(i).locator('.country-flag[role="img"]').first()
        await expect(flag).toBeVisible()
      }
    })

    test('Bratislava card shows SK flag', async ({ page }) => {
      const state = setupMockApi(page)
      state.cities = makeDefaultCities()
      await page.goto('/cities')

      const card = page.locator('.city-card', { hasText: 'Bratislava' })
      await expect(card).toBeVisible()
      // The flag element should have aria-label containing country code
      const flag = card.locator('.country-flag[role="img"]').first()
      await expect(flag).toBeVisible()
      await expect(flag).toHaveAttribute('aria-label', /SK/i)
    })

    test('Prague card shows CZ flag', async ({ page }) => {
      const state = setupMockApi(page)
      state.cities = makeDefaultCities()
      await page.goto('/cities')

      const card = page.locator('.city-card', { hasText: 'Prague' })
      await expect(card).toBeVisible()
      const flag = card.locator('.country-flag[role="img"]').first()
      await expect(flag).toHaveAttribute('aria-label', /CZ/i)
    })

    test('Vienna card shows AT flag', async ({ page }) => {
      const state = setupMockApi(page)
      state.cities = makeDefaultCities()
      await page.goto('/cities')

      const card = page.locator('.city-card', { hasText: 'Vienna' })
      await expect(card).toBeVisible()
      const flag = card.locator('.country-flag[role="img"]').first()
      await expect(flag).toHaveAttribute('aria-label', /AT/i)
    })

    test('flags contain SVG elements', async ({ page }) => {
      const state = setupMockApi(page)
      state.cities = makeDefaultCities()
      await page.goto('/cities')

      const bratislavaCard = page.locator('.city-card', { hasText: 'Bratislava' })
      await expect(bratislavaCard).toBeVisible()
      // The flag component renders an inline SVG
      const svg = bratislavaCard.locator('.country-flag svg')
      await expect(svg).toBeVisible()
    })
  })

  // ──────────────────────────────────────────────────
  // Language switcher flags
  // ──────────────────────────────────────────────────

  test.describe('Language switcher flags', () => {
    test('footer does not expose the private repository link', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      await page.locator('.language-switcher').first().scrollIntoViewIfNeeded()
      await expect(page.locator('a[href*="github.com/ludovit-scholtz/capitalism"]')).toHaveCount(0)
    })

    test('footer language switcher shows flags in each locale button', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      // Language switcher is in the footer
      const switcher = page.locator('.language-switcher').first()
      await expect(switcher).toBeVisible()

      // Each language button should have a flag element
      const buttons = switcher.locator('.language-btn')
      const count = await buttons.count()
      expect(count).toBeGreaterThanOrEqual(3)

      for (let i = 0; i < count; i++) {
        const flag = buttons.nth(i).locator('.country-flag[role="img"]').first()
        await expect(flag).toBeVisible()
      }
    })

    test('English language button shows GB flag', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      const switcher = page.locator('.language-switcher').first()
      // English is always the first language button (locale=en → GB flag)
      const enButton = switcher.locator('.language-btn').nth(0)
      const flag = enButton.locator('.country-flag[role="img"]').first()
      await expect(flag).toBeVisible()
      // Flag title should contain the English language name
      await expect(flag).toHaveAttribute('title', /English/i)
    })

    test('Slovak language button shows SK flag', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      const switcher = page.locator('.language-switcher').first()
      // Slovak is always the second language button (locale=sk → SK flag)
      const skButton = switcher.locator('.language-btn').nth(1)
      const flag = skButton.locator('.country-flag[role="img"]').first()
      await expect(flag).toBeVisible()
      // Flag has an SVG child proving real SVG flag renders
      await expect(skButton.locator('.country-flag svg')).toBeVisible()
    })

    test('German language button shows DE flag', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      const switcher = page.locator('.language-switcher').first()
      // German is always the third language button (locale=de → DE flag)
      const deButton = switcher.locator('.language-btn').nth(2)
      const flag = deButton.locator('.country-flag[role="img"]').first()
      await expect(flag).toBeVisible()
      // Flag title should contain the German language name
      await expect(flag).toHaveAttribute('title', /Deutsch/i)
    })
  })

  // ──────────────────────────────────────────────────
  // Context switcher flags (authenticated)
  // ──────────────────────────────────────────────────

  test.describe('Context switcher flags', () => {
    test('context switcher trigger shows city flag', async ({ page }) => {
      const player = makePlayer()
      player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
      const state = setupMockApi(page, { players: [player] })
      state.cities = makeDefaultCities()
      state.currentUserId = player.id
      state.currentToken = `token-${player.id}`

      await page.addInitScript((token) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
      }, `token-${player.id}`)

      await page.goto('/dashboard')

      // The context switcher trigger should contain a flag image
      const trigger = page.locator('.ctx-trigger')
      await expect(trigger).toBeVisible()
      const flag = trigger.locator('.country-flag[role="img"]').first()
      await expect(flag).toBeVisible()
    })

    test('context switcher panel shows flag for each city', async ({ page }) => {
      const player = makePlayer()
      player.onboardingCompletedAtUtc = '2026-01-01T00:00:00Z'
      const state = setupMockApi(page, { players: [player] })
      state.cities = makeDefaultCities()
      state.currentUserId = player.id
      state.currentToken = `token-${player.id}`

      await page.addInitScript((token) => {
        localStorage.setItem('auth_token', token)
        localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
      }, `token-${player.id}`)

      await page.goto('/dashboard')

      // Open the context switcher panel
      const trigger = page.locator('.ctx-trigger')
      await expect(trigger).toBeVisible()
      await trigger.click()

      // Panel should be open
      const panel = page.locator('.ctx-panel')
      await expect(panel).toBeVisible()

      // Each city option should show a flag
      const cityOptions = panel.locator('.ctx-city-option')
      const count = await cityOptions.count()
      expect(count).toBeGreaterThanOrEqual(3)

      for (let i = 0; i < count; i++) {
        const flag = cityOptions.nth(i).locator('.country-flag[role="img"]').first()
        await expect(flag).toBeVisible()
      }
    })
  })

  // ──────────────────────────────────────────────────
  // Mobile viewport
  // ──────────────────────────────────────────────────

  test.describe('Flags on mobile viewport', () => {
    test('city flags visible on mobile', async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 812 })
      const state = setupMockApi(page)
      state.cities = makeDefaultCities()
      await page.goto('/cities')

      await expect(page.locator('.city-card').first()).toBeVisible()
      const flag = page.locator('.city-card').first().locator('.country-flag[role="img"]').first()
      await expect(flag).toBeVisible()
    })

    test('language switcher flags visible on mobile', async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 812 })
      setupMockApi(page)
      await page.goto('/')

      // Scroll to footer where language switcher lives
      await page.locator('.language-switcher').first().scrollIntoViewIfNeeded()
      const switcher = page.locator('.language-switcher').first()
      await expect(switcher).toBeVisible()

      const buttons = switcher.locator('.language-btn')
      const count = await buttons.count()
      expect(count).toBeGreaterThanOrEqual(3)

      // Each button should have a flag
      for (let i = 0; i < count; i++) {
        const flag = buttons.nth(i).locator('.country-flag[role="img"]').first()
        await expect(flag).toBeVisible()
      }
    })
  })

  // ──────────────────────────────────────────────────
  // Language switcher active state
  // ──────────────────────────────────────────────────

  test.describe('Language switcher active state', () => {
    test('active language button has aria-pressed=true', async ({ page }) => {
      setupMockApi(page)
      await page.goto('/')

      const switcher = page.locator('.language-switcher').first()
      await expect(switcher).toBeVisible()

      // Default locale is English – the EN button should have aria-pressed="true"
      const enButton = switcher.locator('.language-btn').nth(0)
      await expect(enButton).toHaveAttribute('aria-pressed', 'true')

      // Other buttons should not be pressed
      const skButton = switcher.locator('.language-btn').nth(1)
      await expect(skButton).toHaveAttribute('aria-pressed', 'false')
    })

    test('clicking a language button makes it active', async ({ page }) => {
      await page.addInitScript(() => {
        localStorage.removeItem('app_locale')
      })
      setupMockApi(page)
      await page.goto('/')

      const switcher = page.locator('.language-switcher').first()
      await expect(switcher).toBeVisible()

      // Click German (third button)
      const deButton = switcher.locator('.language-btn').nth(2)
      await deButton.click()
      await expect(deButton).toHaveAttribute('aria-pressed', 'true')

      // EN should no longer be pressed
      const enButton = switcher.locator('.language-btn').nth(0)
      await expect(enButton).toHaveAttribute('aria-pressed', 'false')
    })
  })

  // ──────────────────────────────────────────────────
  // Expanded city coverage – all default cities
  // ──────────────────────────────────────────────────

  test.describe('All city flags', () => {
    // Bratislava (SK), Prague (CZ), and Vienna (AT) have individual assertions above.
    // This block covers all 8 seeded cities including those three for completeness.
    const cityFlagCases = [
      { name: 'Bratislava', code: 'SK' },
      { name: 'Prague', code: 'CZ' },
      { name: 'Vienna', code: 'AT' },
      { name: 'New York', code: 'US' },
      { name: 'London', code: 'GB' },
      { name: 'Beijing', code: 'CN' },
      { name: 'Delhi', code: 'IN' },
      { name: 'Berlin', code: 'DE' },
    ]

    for (const { name, code } of cityFlagCases) {
      test(`${name} card shows ${code} flag`, async ({ page }) => {
        const state = setupMockApi(page)
        state.cities = makeDefaultCities()
        await page.goto('/cities')

        const card = page.locator('.city-card', { hasText: name })
        await expect(card).toBeVisible()
        const flag = card.locator('.country-flag[role="img"]').first()
        await expect(flag).toHaveAttribute('aria-label', new RegExp(code, 'i'))
      })
    }
  })

  // ──────────────────────────────────────────────────
  // Fallback badge for unknown country codes
  // ──────────────────────────────────────────────────

  test.describe('Flag fallback', () => {
    test('unknown country code renders text fallback badge instead of SVG', async ({ page }) => {
      const state = setupMockApi(page)
      // Inject a city with an unknown country code
      state.cities = [
        {
          id: 'city-zz',
          name: 'ZZCity',
          countryCode: 'ZZ',
          currencyCode: 'EUR',
          latitude: 0,
          longitude: 0,
          population: 1000,
          averageRentPerSqm: 10,
          baseSalaryPerManhour: 10,
          resources: [],
        },
      ]
      await page.goto('/cities')

      const card = page.locator('.city-card', { hasText: 'ZZCity' })
      await expect(card).toBeVisible()

      // Fallback renders a text badge (country-flag--fallback class)
      const fallback = card.locator('.country-flag--fallback')
      await expect(fallback).toBeVisible()
      await expect(fallback).toContainText('ZZ')

      // No SVG should be inside the fallback span
      const svg = card.locator('.country-flag svg')
      await expect(svg).toHaveCount(0)
    })
  })
})
