import { test } from '@playwright/test'
import { setupMockApi } from './e2e/helpers/mock-api'

test('screenshot banking marketplace', async ({ page }) => {
  const state = setupMockApi(page, {})
  state.allBanks = [
    {
      bankBuildingId: 'b1', bankBuildingName: 'Alpha Bank', cityId: 'city-ba', cityName: 'Bratislava',
      lenderCompanyId: 'co-b1', lenderCompanyName: 'Alpha Corp',
      depositInterestRatePercent: 5.5, lendingInterestRatePercent: 11.0,
      totalDeposits: 10_000_000, lendableCapacity: 9_000_000,
      outstandingLoanPrincipal: 2_000_000, availableLendingCapacity: 7_000_000, baseCapitalDeposited: true,
    },
    {
      bankBuildingId: 'b2', bankBuildingName: 'Beta Bank', cityId: 'city-pr', cityName: 'Prague',
      lenderCompanyId: 'co-b2', lenderCompanyName: 'Beta Corp',
      depositInterestRatePercent: 4.0, lendingInterestRatePercent: 8.5,
      totalDeposits: 5_000_000, lendableCapacity: 4_500_000,
      outstandingLoanPrincipal: 0, availableLendingCapacity: 4_500_000, baseCapitalDeposited: true,
    },
  ]
  await page.goto('/loans')
  await page.getByRole('tab', { name: 'Deposit' }).click()
  await page.waitForTimeout(300)
  await page.screenshot({ path: '/tmp/banking-marketplace.png', fullPage: false })
})
