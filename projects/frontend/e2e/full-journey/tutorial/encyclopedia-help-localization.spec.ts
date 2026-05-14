import { expect, test } from '@playwright/test'
import { makeDefaultProducts, makeDefaultResources, setupMockApi } from '../../helpers/mock-api.js'

const localeCases = [
  { locale: 'en', expectedPath: '/onboarding-help/en/step-1-city.png' },
  { locale: 'sk', expectedPath: '/onboarding-help/sk/step-1-city.png' },
  { locale: 'de', expectedPath: '/onboarding-help/de/step-1-city.png' },
] as const

test.describe('Encyclopedia help image localization', () => {
  for (const { locale, expectedPath } of localeCases) {
    test(`uses ${locale} localized onboarding screenshots in the help section`, async ({ page }) => {
      setupMockApi(page, {
        resourceTypes: makeDefaultResources(),
        productTypes: makeDefaultProducts(),
      })

      await page.addInitScript((selectedLocale) => {
        localStorage.setItem('app_locale', selectedLocale)
      }, locale)

      await page.goto('/encyclopedia/onboarding-help')

      await expect(page.locator('.onboarding-help-card .help-card-image').first()).toHaveAttribute('src', new RegExp(expectedPath.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
    })
  }
})
