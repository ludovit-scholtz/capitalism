import { expect, test } from '@playwright/test'
import { loginAs, makePlayer, makeSubscription, setupMockApi } from './helpers/mock-api'

const REFERRAL_STORAGE_KEY = 'master_referral_program_v1'

test.describe('Referral pages', () => {
  test('referral setup redirects unauthenticated users to login', async ({ page }) => {
    setupMockApi(page)
    await page.goto('/referrals/setup')
    await expect(page).toHaveURL('/login')
  })

  test('setup page allows code to be set only once', async ({ page }) => {
    const currentPlayer = makePlayer({ email: 'invitee@example.com', displayName: 'Invitee' })
    const state = setupMockApi(page, { subscription: makeSubscription() })
    await loginAs(page, state, currentPlayer)

    await page.addInitScript((storageKey) => {
      const seeded = {
        players: {
          'owner@example.com': {
            appliedReferralCode: null,
            referralIdentity: {
              fullName: 'Owner',
              taxDomicile: 'Germany',
              createdAtUtc: '2026-01-01T00:00:00.000Z',
            },
            referralCodes: [
              {
                id: 'code-owner-1',
                code: 'AB12CD34',
                createdAtUtc: '2026-01-01T00:00:00.000Z',
              },
            ],
            hasActiveSubscription: true,
          },
        },
      }
      localStorage.setItem(storageKey, JSON.stringify(seeded))
    }, REFERRAL_STORAGE_KEY)

    await page.goto('/referrals/setup')

    await page.getByLabel('Referral code').fill('ab12cd34')
    await page.getByRole('button', { name: 'Save Referral Code' }).click()

    await expect(page.getByText('Referral code saved')).toBeVisible()
    await expect(page.getByText('Saved code: AB12CD34')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Save Referral Code' })).toBeDisabled()
  })

  test('become referral auto-generates first 8-character code', async ({ page }) => {
    const currentPlayer = makePlayer({ email: 'partner@example.com', displayName: 'Partner' })
    const state = setupMockApi(page, { subscription: makeSubscription() })
    await loginAs(page, state, currentPlayer)

    await page.goto('/referrals/become')

    await page.getByLabel('Name').fill('Partner Name')
    await page.getByLabel('Tax domicile').fill('Slovakia')
    await page.getByRole('button', { name: 'Activate Referral Profile' }).click()

    await expect(page.getByText('Referral profile is active')).toBeVisible()
    await expect(page.locator('.generated-code')).toContainText(/Your primary code: [A-Z0-9]{8}/)
  })

  test('dashboard shows per-code direct and second-level metrics', async ({ page }) => {
    const owner = makePlayer({ email: 'owner@example.com', displayName: 'Owner' })
    const state = setupMockApi(page, { subscription: makeSubscription() })
    await loginAs(page, state, owner)

    await page.addInitScript((storageKey) => {
      const seeded = {
        players: {
          'owner@example.com': {
            appliedReferralCode: null,
            referralIdentity: {
              fullName: 'Owner User',
              taxDomicile: 'Austria',
              createdAtUtc: '2026-01-01T00:00:00.000Z',
            },
            referralCodes: [
              {
                id: 'code-owner-1',
                code: 'OWNER001',
                createdAtUtc: '2026-01-01T00:00:00.000Z',
              },
            ],
            hasActiveSubscription: true,
          },
          'direct@example.com': {
            appliedReferralCode: 'OWNER001',
            referralIdentity: {
              fullName: 'Direct User',
              taxDomicile: 'Czechia',
              createdAtUtc: '2026-01-02T00:00:00.000Z',
            },
            referralCodes: [
              {
                id: 'code-direct-1',
                code: 'DIRECT01',
                createdAtUtc: '2026-01-02T00:00:00.000Z',
              },
            ],
            hasActiveSubscription: true,
          },
          'second-a@example.com': {
            appliedReferralCode: 'DIRECT01',
            referralIdentity: null,
            referralCodes: [],
            hasActiveSubscription: false,
          },
          'second-b@example.com': {
            appliedReferralCode: 'DIRECT01',
            referralIdentity: null,
            referralCodes: [],
            hasActiveSubscription: true,
          },
        },
      }
      localStorage.setItem(storageKey, JSON.stringify(seeded))
    }, REFERRAL_STORAGE_KEY)

    await page.goto('/referrals/dashboard')

    const row = page.locator('tbody tr').filter({ hasText: 'OWNER001' })
    await expect(row).toContainText('OWNER001')
    await expect(row).toContainText('1')
    await expect(row).toContainText('2')

    await expect(page.locator('.summary-card', { hasText: 'Direct registrations' })).toContainText(
      '1',
    )
    await expect(
      page.locator('.summary-card', { hasText: 'Second-level registrations' }),
    ).toContainText('2')
    await expect(page.locator('.summary-card', { hasText: 'Active subscriptions' })).toContainText(
      '1',
    )
    await expect(
      page.locator('.summary-card', { hasText: 'Second-level active subs' }),
    ).toContainText('1')
  })
})
