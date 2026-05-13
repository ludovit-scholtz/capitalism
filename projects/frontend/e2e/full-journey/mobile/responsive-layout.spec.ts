import { expect, test, type Page } from '@playwright/test'
import { makeAdminPlayer, makePlayer, setupMockApi } from '../../helpers/mock-api'

const MAX_TABLET_WIDTH = 768
const MIN_4K_ROOT_FONT_SIZE_PX = 17

// 375×667 is the acceptance-criteria baseline (iPhone SE class).
// Keep 390×844 too so we continue covering modern larger mobile devices.
const overflowViewports = [
  { label: 'mobile', width: 375, height: 667 },
  { label: 'mobile-lg', width: 390, height: 844 },
  { label: 'tablet', width: 768, height: 1024 },
  { label: 'fullhd', width: 1920, height: 1080 },
] as const
const OVERFLOW_TOLERANCE_PX = 1

function seedAuthenticatedState(page: Page) {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    companies: [
        {
          id: 'company-responsive',
          playerId: 'player-1',
          name: 'Responsive Corp',
          cash: 650000,
          foundedAtUtc: '2026-01-01T00:00:00Z',
          buildings: [
            {
              id: 'building-responsive',
              companyId: 'company-responsive',
              cityId: 'city-ba',
              type: 'FACTORY',
              name: 'Responsive Factory',
              latitude: 48.15,
              longitude: 17.1,
              level: 1,
              powerConsumption: 0,
              isForSale: false,
              units: [
                {
                  id: 'unit-purchase',
                  buildingId: 'building-responsive',
                  unitType: 'PURCHASE',
                  gridX: 0,
                  gridY: 0,
                  level: 1,
                  linkRight: true,
                },
                {
                  id: 'unit-manufacturing',
                  buildingId: 'building-responsive',
                  unitType: 'MANUFACTURING',
                  gridX: 1,
                  gridY: 0,
                  level: 1,
                  linkRight: true,
                },
                {
                  id: 'unit-storage',
                  buildingId: 'building-responsive',
                  unitType: 'STORAGE',
                  gridX: 2,
                  gridY: 0,
                  level: 1,
                  linkRight: true,
                },
                {
                  id: 'unit-sales',
                  buildingId: 'building-responsive',
                  unitType: 'B2B_SALES',
                  gridX: 3,
                  gridY: 0,
                  level: 1,
                },
              ],
            },
          ],
        },
      ],
    })

  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`

  return { player, state }
}

function seedAdminState(page: Page) {
  const admin = makeAdminPlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
  })
  const state = setupMockApi(page, { players: [admin] })
  state.currentUserId = admin.id
  state.currentToken = `token-${admin.id}`
  return { admin, state }
}

async function seedAuthenticatedStorage(page: Page, token: string) {
  await page.addInitScript((t) => {
    localStorage.setItem('auth_token', t)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, token)
}

async function expectNoHorizontalOverflow(page: Page) {
  // Allow a 1px tolerance to avoid false positives from sub-pixel rounding across
  // different viewport/device scale combinations in Chromium.
  const overflowInfo = await page.evaluate((tolerance) => {
    const hasOverflow = document.documentElement.scrollWidth > window.innerWidth + tolerance
    const offenders = Array.from(document.querySelectorAll<HTMLElement>('body *'))
      .map((element) => {
        const rect = element.getBoundingClientRect()
        return {
          tag: element.tagName.toLowerCase(),
          className: element.className,
          width: Math.round(rect.width),
          right: Math.round(rect.right),
        }
      })
      .filter((item) => item.right > window.innerWidth + tolerance)
      .sort((a, b) => b.right - a.right)
      .slice(0, 8)

    return {
      hasOverflow,
      viewport: window.innerWidth,
      scrollWidth: document.documentElement.scrollWidth,
      offenders,
    }
  }, OVERFLOW_TOLERANCE_PX)
  expect(overflowInfo.hasOverflow, JSON.stringify(overflowInfo)).toBe(false)
}

test.describe('Responsive layout baseline checks', () => {
  test('dashboard, leaderboard, forex, market and city map stay responsive across target viewports', async ({
    page,
  }) => {
    const { player, state } = seedAuthenticatedState(page)
    await seedAuthenticatedStorage(page, `token-${player.id}`)
    const cityId = state.cities[0]?.id ?? 'city-ba'

    for (const viewport of overflowViewports) {
      await page.setViewportSize({ width: viewport.width, height: viewport.height })

      await page.goto('/dashboard')
      await expect(page.getByRole('heading', { name: /Dashboard/i })).toBeVisible()
      await expectNoHorizontalOverflow(page)

      await page.goto('/leaderboard')
      await expect(page.getByRole('heading', { name: /Leaderboard/i })).toBeVisible()
      await expectNoHorizontalOverflow(page)

      await page.goto('/forex')
      await expect(page.getByRole('heading', { name: /Forex Exchange/i })).toBeVisible()
      await expectNoHorizontalOverflow(page)

      await page.goto('/buildings/market')
      await expect(page.getByRole('heading', { name: /Building Market/i })).toBeVisible()
      await expectNoHorizontalOverflow(page)

      await page.goto(`/city/${cityId}/buildings`)
      await expect(page.locator('.map-container')).toBeVisible()
      await expectNoHorizontalOverflow(page)
    }
  })

  test('4K viewport keeps content width constrained', async ({ page }) => {
    const { player } = seedAuthenticatedState(page)
    await seedAuthenticatedStorage(page, `token-${player.id}`)
    await page.setViewportSize({ width: 3840, height: 2160 })

    await page.goto('/leaderboard')
    await expect(page.getByRole('heading', { name: /Leaderboard/i })).toBeVisible()
    await expectNoHorizontalOverflow(page)

    await page.goto('/dashboard')
    await expect(page.getByRole('heading', { name: /Dashboard/i })).toBeVisible()
    await expectNoHorizontalOverflow(page)

    await page.goto('/forex')
    await expect(page.getByRole('heading', { name: /Forex Exchange/i })).toBeVisible()
    await expectNoHorizontalOverflow(page)

    const maxContainerWidth = await page.evaluate(() =>
      Math.max(
        0,
        ...Array.from(document.querySelectorAll('.container')).map((element) =>
          Math.round((element as HTMLElement).getBoundingClientRect().width),
        ),
      ),
    )
    expect(maxContainerWidth).toBeLessThanOrEqual(1600)

    const rootFontSizePx = await page.evaluate(() => Number.parseFloat(getComputedStyle(document.documentElement).fontSize))
    expect(rootFontSizePx).toBeGreaterThanOrEqual(MIN_4K_ROOT_FONT_SIZE_PX)
  })

  test('mobile navigation menu opens grouped second-level links and closes correctly', async ({ page }) => {
    setupMockApi(page)
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto('/')

    await page.getByRole('button', { name: 'Toggle navigation menu' }).click()
    await expect(page.getByRole('button', { name: 'Main' })).toBeVisible()
    await expect(page.locator('.mobile-nav-sections').getByRole('link', { name: 'Leaderboard', exact: true })).toBeVisible()

    await page.getByRole('button', { name: 'Economy' }).click()
    await expect(page.locator('.mobile-nav-sections').getByRole('link', { name: 'Banking', exact: true })).toBeVisible()
    await expect(page.locator('.mobile-nav-sections').getByRole('link', { name: 'Stocks', exact: true })).toBeVisible()

    await page.getByRole('button', { name: 'Toggle navigation menu' }).click()
    await expect(page.locator('.mobile-nav-sections').getByRole('link', { name: 'Leaderboard', exact: true })).toBeHidden()
  })

  test('city map lot detail behaves as a bottom drawer on mobile', async ({ page }) => {
    const { player, state } = seedAuthenticatedState(page)
    await seedAuthenticatedStorage(page, `token-${player.id}`)
    await page.setViewportSize({ width: 390, height: 844 })

    const cityId = state.cities[0]?.id ?? 'city-ba'
    await page.goto(`/city/${cityId}/buildings`)

    await expect(page.locator('.map-container')).toBeVisible()
    const mapHeight = await page.locator('.map-container').evaluate((element) => element.getBoundingClientRect().height)
    expect(mapHeight).toBeGreaterThan(300)

    await page.locator('.view-toggle .toggle-btn').nth(1).click()
    await page.locator('.lot-list-item').first().click()
    await expect(page.locator('.detail-header')).toBeVisible()

    const panelPosition = await page.locator('.detail-header').evaluate((element) => getComputedStyle(element.closest('.detail-panel') as Element).position)
    const panelBottom = await page.locator('.detail-header').evaluate((element) => getComputedStyle(element.closest('.detail-panel') as Element).bottom)
    expect(panelPosition).toBe('fixed')
    expect(panelBottom).toBe('8px')
  })

  test('building detail grid editor and operations dashboard stay responsive on mobile and tablet', async ({ page }) => {
    const { player } = seedAuthenticatedState(page)
    await seedAuthenticatedStorage(page, `token-${player.id}`)

    for (const viewport of overflowViewports.filter((item) => item.width <= MAX_TABLET_WIDTH)) {
      await page.setViewportSize({ width: viewport.width, height: viewport.height })
      await page.goto('/building/building-responsive')
      await expect(page.getByRole('heading', { name: 'Responsive Factory' })).toBeVisible()
      await page.getByRole('button', { name: 'Edit Building' }).click()
      await expect(page.locator('.unit-grid')).toBeVisible()
      await expectNoHorizontalOverflow(page)
    }

    const { admin } = seedAdminState(page)
    await seedAuthenticatedStorage(page, `token-${admin.id}`)

    for (const viewport of overflowViewports.filter((item) => item.width <= MAX_TABLET_WIDTH)) {
      await page.setViewportSize({ width: viewport.width, height: viewport.height })
      await page.goto('/operations/statistics')
      await expect(page.getByRole('heading', { name: /operations overview/i })).toBeVisible()
      await expectNoHorizontalOverflow(page)
    }
  })
})
