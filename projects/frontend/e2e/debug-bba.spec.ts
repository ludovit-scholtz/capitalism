import { expect, test } from '@playwright/test'
import { makeDefaultCities, makeDefaultProducts, makeDefaultResources, makePlayer, setupMockApi } from './helpers/mock-api'

test('debug suspension', async ({ page }) => {
  const player = makePlayer()
  const companyId = 'company-bba-debug'
  const buildingId = 'building-bba-debug'

  player.companies.push({
    id: companyId, playerId: player.id, name: 'Debug Co', cash: 500_000,
    foundedAtUtc: '2026-01-01T00:00:00Z',
    buildings: [{
      id: buildingId, companyId, cityId: 'city-ba', type: 'FACTORY', name: 'Debug Factory',
      latitude: 48.15, longitude: 17.11, level: 1, powerConsumption: 2, isForSale: false,
      builtAtUtc: '2026-01-01T00:00:00Z', powerStatus: 'POWERED', isUnderConstruction: false,
      constructionCompletesAtTick: null, constructionCost: 0, contentValue: 0,
      contentBudgetPerTick: null, isSuspendedForFunds: true,
      suspendedReason: 'INSUFFICIENT_FUNDS:150.00', units: [], pendingConfiguration: null,
      askingPrice: null, pricePerSqm: null, pendingPriceActivationTick: null,
      pendingPricePerSqm: null, occupancyPercent: null, totalAreaSqm: null,
      powerPlantType: null, powerOutput: null, mediaType: null, interestRate: null,
      cityReferenceRentPerSqm: null, adjustedMarketRentPerSqm: null, populationIndex: null,
    }],
  })

  const state = setupMockApi(page, {
    players: [player], cities: makeDefaultCities(),
    resourceTypes: makeDefaultResources(), productTypes: makeDefaultProducts(),
  })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.buildingBankAccounts[buildingId] = {
    hasBankAccount: true, bankAccountId: 'acc-debug', accountNumber: '1234567890123456',
    balance: 0, isSuspendedForFunds: true, suspendedReason: 'INSUFFICIENT_FUNDS:150.00', currencyCode: 'EUR',
  }

  const requests: string[] = []
  page.on('request', req => {
    if (req.url().includes('graphql') || req.url().includes('localhost')) {
      try {
        const body = req.postDataJSON()
        const op = body?.query?.trim().slice(0, 100)
        requests.push(`REQ: ${op}`)
      } catch { requests.push(`REQ (no body): ${req.url()}`) }
    }
  })
  page.on('response', async res => {
    if (res.url().includes('localhost')) {
      try {
        const body = await res.json()
        const summary = JSON.stringify(body.data).slice(0, 150)
        requests.push(`RES: ${summary}`)
      } catch {}
    }
  })

  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)

  await page.goto(`/building/${buildingId}`)
  await expect(page.getByRole('heading', { name: 'Building Overview' })).toBeVisible({ timeout: 10000 })
  await expect(page.locator('.bba-alert-danger')).toBeVisible()

  const panel = page.locator('.building-bank-account-panel')
  const fundPanel = panel.locator('.bba-fund-panel')
  await fundPanel.locator('.bba-fund-summary').click()
  await expect(panel.locator('.bba-fund-input')).toBeVisible()
  await panel.locator('.bba-fund-input').fill('50000')
  await fundPanel.getByRole('button', { name: /^transfer$/i }).click()
  await expect(fundPanel.locator('.bba-fund-success')).toBeVisible({ timeout: 10000 })

  console.log('=== NETWORK TRACE ===')
  for (const r of requests) console.log(r)
  console.log('=== END TRACE ===')

  const alertVisible = await page.locator('.bba-alert-danger').isVisible()
  console.log(`Alert visible after fund: ${alertVisible}`)
  const balText = await panel.locator('.bba-balance').textContent().catch(() => 'N/A')
  console.log(`Balance text: ${balText}`)
})
