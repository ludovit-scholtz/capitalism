import { test, expect, type Page } from '@playwright/test'
import { setupMockApi, makePlayer } from '../../helpers/mock-api'
import { makeShop, makeUnit, mockUnitStatuses, type MockUnitStatus } from '../../helpers/mock-api-advisor'

/** The first page load of a run compiles the dev bundle lazily, which can exceed the default 2 s assertion timeout. */
const COLD_START_TIMEOUT = 15_000

const outOfStock = (unitId: string, idleTicks: number): MockUnitStatus => ({
  buildingUnitId: unitId,
  status: 'BLOCKED',
  blockedCode: 'NO_INVENTORY',
  blockedReason: 'No stock available to sell.',
  idleTicks,
})

async function openDashboard(page: Page, shops: ReturnType<typeof makeShop>[], statuses: Record<string, MockUnitStatus[]>) {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    companies: [
      {
        id: 'comp-advisor',
        playerId: 'player-1',
        name: 'Advisor Corp',
        cash: 400000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: shops,
      },
    ],
  })
  const mock = setupMockApi(page, { players: [player] })
  mock.currentUserId = player.id
  mock.currentToken = `token-${player.id}`
  await mockUnitStatuses(page, statuses)
  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  await page.goto('/dashboard')
}

test.describe('Dashboard — advisor card', () => {
  test('suggests adding a purchase unit to a shop that has been out of stock, with a link to the building', async ({ page }) => {
    const shop = makeShop('shop-empty', 'Corner Shop', [makeUnit('shop-empty', 'shop-empty-sales', 'PUBLIC_SALES')])
    await openDashboard(page, [shop], { 'shop-empty': [outOfStock('shop-empty-sales', 14)] })

    const advisor = page.getByRole('region', { name: 'Advisor' })
    await expect(advisor).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(advisor.getByText('Corner Shop is out of stock')).toBeVisible()
    await expect(advisor.getByText(/nothing to sell for 14 hours/)).toBeVisible()
    await expect(advisor.getByText(/Add a purchase unit/)).toBeVisible()
    await expect(advisor.getByRole('link', { name: /Open building/ })).toHaveAttribute('href', '/building/shop-empty')
  })

  test('says to check the purchase unit when the shop already has one', async ({ page }) => {
    const shop = makeShop('shop-linked', 'Linked Shop', [
      makeUnit('shop-linked', 'shop-linked-sales', 'PUBLIC_SALES'),
      makeUnit('shop-linked', 'shop-linked-buy', 'PURCHASE', 1),
    ])
    await openDashboard(page, [shop], { 'shop-linked': [outOfStock('shop-linked-sales', 30)] })

    const advisor = page.getByRole('region', { name: 'Advisor' })
    await expect(advisor.getByText(/Check that its purchase unit is linked/)).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(advisor.getByText(/nothing to sell for 1 day 6 hours/)).toBeVisible()
  })

  test('dismissing a suggestion hides it and keeps it hidden after a reload', async ({ page }) => {
    const shop = makeShop('shop-empty', 'Corner Shop', [makeUnit('shop-empty', 'shop-empty-sales', 'PUBLIC_SALES')])
    await openDashboard(page, [shop], { 'shop-empty': [outOfStock('shop-empty-sales', 14)] })

    const advisor = page.getByRole('region', { name: 'Advisor' })
    await expect(advisor).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await advisor.getByRole('button', { name: 'Dismiss' }).click()
    await expect(page.getByRole('region', { name: 'Advisor' })).toHaveCount(0)

    await page.reload()
    await expect(page.getByRole('heading', { name: 'Dashboard' }).first()).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(page.getByRole('region', { name: 'Advisor' })).toHaveCount(0)
  })

  test('stays hidden while the shop is below the out-of-stock threshold', async ({ page }) => {
    const shop = makeShop('shop-fresh', 'Fresh Shop', [makeUnit('shop-fresh', 'shop-fresh-sales', 'PUBLIC_SALES')])
    await openDashboard(page, [shop], { 'shop-fresh': [outOfStock('shop-fresh-sales', 3)] })

    await expect(page.getByRole('heading', { name: 'Dashboard' }).first()).toBeVisible({ timeout: COLD_START_TIMEOUT })
    await expect(page.getByText('Fresh Shop is out of stock')).toHaveCount(0)
  })
})
