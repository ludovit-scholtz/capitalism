import { expect, test } from '@playwright/test'
import { setupMockApi } from './helpers/mock-api'

test.describe('Documentation page', () => {
  test('renders page header and three topic buttons', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/docs')

    await expect(page.getByRole('heading', { name: 'Getting Started' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Getting Started' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Buildings Guide' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Economy Overview' })).toBeVisible()
  })

  test('shows Getting Started content by default', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/docs')

    await expect(page.getByRole('heading', { name: 'Getting Started' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Welcome to Capitalism' })).toBeVisible()
  })

  test('switches to Buildings Guide topic on click', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/docs')

    await page.getByRole('button', { name: 'Buildings Guide' }).click()
    await expect(page.getByRole('heading', { name: 'Buildings Guide' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Building Types' })).toBeVisible()
  })

  test('switches to Economy Overview topic on click', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/docs')

    await page.getByRole('button', { name: 'Economy Overview' }).click()
    await expect(page.getByRole('heading', { name: 'Economy Overview' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'The Tick Cycle' })).toBeVisible()
  })

  test('nav link to /docs is visible on home page', async ({ page }) => {
    setupMockApi(page, { servers: [] })
    await page.goto('/')

    const docsLink = page.getByRole('link', { name: 'Docs' }).first()
    await expect(docsLink).toBeVisible()
    await expect(docsLink).toHaveAttribute('href', '/docs')
  })
})
