import { expect, test } from '@playwright/test'
import { setupMockApi } from './helpers/mock-api'

test.describe('Legal documents page', () => {
  test('renders Terms and Conditions by default', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/terms')

    await expect(page.getByRole('heading', { name: 'Terms and Conditions' })).toBeVisible()
    await expect(page.getByText('Scholtz & Company, jsa')).toBeVisible()
    await expect(page.getByText('Pay-to-play mechanism and tokenized gold')).toBeVisible()
    await expect(
      page.getByRole('link', { name: 'asa.gold/terms/latest' }),
    ).toHaveAttribute('href', 'https://asa.gold/terms/latest')
  })

  test('renders the Privacy Policy on the privacy route', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/privacy')

    await expect(page.getByRole('heading', { name: 'Privacy Policy' })).toBeVisible()
    await expect(page.getByText('Data storage in the EU')).toBeVisible()
  })

  test('switches between Terms and Privacy from the sidebar', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/terms')

    await expect(page.getByRole('heading', { name: 'Terms and Conditions' })).toBeVisible()
    await page.getByRole('button', { name: 'Privacy Policy' }).click()
    await expect(page).toHaveURL(/\/privacy$/)
    await expect(page.getByRole('heading', { name: 'Privacy Policy' })).toBeVisible()
  })
})
