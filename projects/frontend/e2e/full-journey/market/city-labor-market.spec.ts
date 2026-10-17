import { expect, test } from '@playwright/test'
import { loginAs, makePlayer, setupMockApi } from '../../helpers/mock-api'

function companyPlayer() {
  return makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    activeAccountType: 'COMPANY',
    activeCompanyId: 'comp-labor',
    companies: [
      {
        id: 'comp-labor',
        playerId: 'player-1',
        name: 'Labor Corp',
        cash: 500000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ],
  })
}

test('city economy tab shows unemployment, wage pressure and a fair-pay state', async ({ page }) => {
  const player = companyPlayer()
  const state = setupMockApi(page, { players: [player] })
  state.cityLaborMarketOverrides['city-ba'] = { unemploymentRate: 0.6, wageIndex: 1.2, averageSalaryMultiplier: 1.1, yourSalaryMultiplier: 1.1, payRatio: 1 }
  await loginAs(page, state, player)

  await page.goto('/city/city-ba/economy')

  const section = page.getByTestId('city-labor-market')
  await expect(section.getByRole('heading', { name: 'Labor market' })).toBeVisible()
  await expect(section.getByTestId('labor-unemployment')).toHaveText('60%')
  await expect(section.getByTestId('labor-wage-pressure')).toHaveText('+20%')
  await expect(section.getByTestId('labor-strike-risk')).toHaveText('No strike risk')
  await expect(section.getByTestId('labor-hint')).toHaveCount(0)
})

test('underpaying company sees strike risk, a settings hint and the buildings held back', async ({ page }) => {
  const player = companyPlayer()
  const state = setupMockApi(page, { players: [player] })
  state.cityLaborMarketOverrides['city-ba'] = {
    averageSalaryMultiplier: 1.2,
    yourSalaryMultiplier: 0.6,
    payRatio: 0.6,
    strikeProne: true,
    affectedBuildings: [
      { buildingId: 'b-strike', name: 'Old Plant', staffingFactor: 0.4, onStrike: true, strikeUntilTick: 9001 },
      { buildingId: 'b-short', name: 'Small Plant', staffingFactor: 0.4, onStrike: false, strikeUntilTick: null },
    ],
  }
  await loginAs(page, state, player)

  await page.goto('/city/city-ba/economy')

  const section = page.getByTestId('city-labor-market')
  await expect(section.getByTestId('labor-strike-risk')).toHaveText('High strike risk')
  await expect(section.getByTestId('labor-hint')).toContainText('60%')
  await expect(section.getByRole('link', { name: 'Open company settings' })).toHaveAttribute('href', '/company/comp-labor/settings')
  const affected = section.getByTestId('labor-affected-buildings')
  await expect(affected.getByRole('link', { name: 'Old Plant' })).toHaveAttribute('href', '/building/b-strike')
  await expect(affected).toContainText(/On strike until .*2001/)
  await expect(affected).toContainText('Staffed at 40%')
})

test('person account sees market figures but no company comparison', async ({ page }) => {
  const player = makePlayer({ onboardingCompletedAtUtc: '2026-01-01T00:00:00Z' })
  const state = setupMockApi(page, { players: [player] })
  await loginAs(page, state, player)

  await page.goto('/city/city-ba/economy')

  const section = page.getByTestId('city-labor-market')
  await expect(section.getByTestId('labor-unemployment')).toBeVisible()
  await expect(section.getByTestId('labor-pay-ratio')).toHaveCount(0)
  await expect(section).toContainText('Switch to a company account')
})
