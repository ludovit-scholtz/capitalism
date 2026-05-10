import { expect, test } from '@playwright/test'
import {
  loginAs,
  makeApiKey,
  makeApiKeyAuditLog,
  makeGameCompany,
  makePlayer,
  setupMockApi,
} from './helpers/mock-api'

test.describe('API key management', () => {
  test('player creates a scoped key, sees denied trading recorded, and revokes it', async ({
    page,
  }) => {
    const player = makePlayer({ id: 'player-api-1', email: 'pilot@example.com', displayName: 'Pilot' })
    const state = setupMockApi(page, {
      currentPlayer: player,
      myCompanies: [makeGameCompany({ id: 'company-a', name: 'Alpha Manufacturing' })],
      apiKeys: [],
      apiKeyAuditLogs: [],
    })
    await loginAs(page, state, player)

    await page.goto('/api-keys')

    await expect(page.getByRole('heading', { name: 'API Keys' })).toBeVisible()
    await page.getByRole('button', { name: 'Generate New Key' }).click()
    await page.getByLabel('Key name').fill('Read-only risk key')
    await page.getByLabel('Company-bound').check()
    await page.getByLabel('Alpha Manufacturing').check()
    await page.getByRole('button', { name: 'Generate', exact: true }).click()

    const generatedKey = await page
      .locator('.fixed.inset-0')
      .last()
      .locator('code')
      .textContent()
    expect(generatedKey).toContain('plain-api-key-')

    const tradeAttempt = await page.evaluate(async ({ key }) => {
      const response = await fetch('/graphql', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `ApiKey ${key}`,
        },
        body: JSON.stringify({
          query: `
            mutation ExecuteForexSwap($input: ExecuteForexSwapInput!) {
              executeForexSwap(input: $input) { fromAmount }
            }
          `,
          variables: {
            input: {
              fromCurrencyCode: 'EUR',
              toCurrencyCode: 'USD',
              amount: 100,
            },
          },
        }),
      })

      return {
        status: response.status,
        body: await response.json(),
      }
    }, { key: generatedKey })

    expect(tradeAttempt.status).toBe(403)
    expect(tradeAttempt.body.errors[0].extensions.code).toBe('API_KEY_SCOPE_FORBIDDEN')

    await page.getByRole('button', { name: 'Close' }).click()
    await page.getByRole('button', { name: 'Refresh' }).click()

    await expect(page.locator('section[aria-label="Player API key audit log"]')).toContainText(
      'executeForexSwap',
    )
    await expect(page.locator('section[aria-label="Player API key audit log"]')).toContainText(
      'Denied',
    )

    page.once('dialog', (dialog) => dialog.accept())
    await page.getByRole('button', { name: 'Revoke' }).click()
    await expect(page.locator('section[aria-label="Player API keys"]')).toContainText('Revoked')
  })

  test('admin can filter, force revoke, and bulk revoke player keys', async ({ page }) => {
    const admin = makePlayer({ id: 'admin-1', email: 'admin@example.com', displayName: 'Admin' })
    const state = setupMockApi(page, {
      currentPlayer: admin,
      isGlobalAdmin: true,
      apiKeys: [
        makeApiKey({
          id: 'api-key-one',
          name: 'Trader One',
          scopes: ['trading-only'],
          playerId: 'player-one',
          playerEmail: 'owner@example.com',
          playerDisplayName: 'Owner',
        }),
        makeApiKey({
          id: 'api-key-two',
          name: 'Bot Two',
          scopes: ['bot-only', 'company-bound'],
          companyIds: ['company-x'],
          playerId: 'player-one',
          playerEmail: 'owner@example.com',
          playerDisplayName: 'Owner',
        }),
      ],
      apiKeyAuditLogs: [
        makeApiKeyAuditLog({
          keyId: 'api-key-one',
          keyName: 'Trader One',
          playerId: 'player-one',
          playerEmail: 'owner@example.com',
          playerDisplayName: 'Owner',
          operationName: 'buyShares',
          scopeUsed: 'trading-only',
        }),
      ],
    })
    await loginAs(page, state, admin)

    await page.goto('/api-keys')

    await page.getByLabel('Filter by player email').fill('owner@example.com')
    await page.getByRole('button', { name: 'Refresh' }).last().click()

    await expect(page.locator('section[aria-label="Admin API key tooling"]')).toContainText(
      'owner@example.com',
    )
    await expect(page.locator('section[aria-label="Admin API key tooling"]')).toContainText(
      'Trader One',
    )

    page.once('dialog', (dialog) => dialog.accept())
    await page.getByRole('button', { name: 'Force revoke' }).first().click()
    await expect(page.locator('section[aria-label="Admin API key tooling"]')).toContainText(
      'Revoked',
    )

    page.once('dialog', (dialog) => dialog.accept())
    await page.getByRole('button', { name: 'Revoke all for player' }).first().click()
    await expect(page.locator('section[aria-label="Admin API key tooling"]')).toContainText(
      'Bot Two',
    )
    await expect(page.locator('section[aria-label="Admin API key tooling"]')).toContainText(
      'buyShares',
    )
  })
})
