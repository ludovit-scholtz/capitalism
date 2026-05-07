import { test, expect } from '@playwright/test'
import { makePlayer, setupMockApi } from '../../helpers/mock-api'

test.describe('Referral code detection', () => {
  test('shows referral banner when ?ref= query param is present', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/login?ref=ABC123')
    await expect(page.locator('.referral-banner')).toBeVisible()
    await expect(page.locator('.referral-banner')).toContainText('ABC123')
    await expect(page.locator('.referral-banner')).toContainText('10%')
  })

  test('does not show referral banner when no ?ref= param', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/login')
    await expect(page.locator('.referral-banner')).toBeHidden()
  })

  test('does not show referral banner for invalid short code', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/login?ref=AB')
    await expect(page.locator('.referral-banner')).toBeHidden()
  })

  test('normalizes referral code to uppercase in banner', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/login?ref=abc123xyz')
    await expect(page.locator('.referral-banner')).toBeVisible()
    await expect(page.locator('.referral-banner')).toContainText('ABC123XYZ')
  })

  test('stores referral code in localStorage on page load', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/login?ref=REF9999')
    const stored = await page.evaluate(() => localStorage.getItem('pending_referral_code'))
    expect(stored).toBe('REF9999')
  })

  test('referral code persists on page reload from localStorage', async ({ page }) => {
    setupMockApi(page, {})

    // First visit sets the code
    await page.goto('/login?ref=PERSIST1')

    // Navigate to a different page, then back to login without the param
    await page.goto('/login')
    await expect(page.locator('.referral-banner')).toBeVisible()
    await expect(page.locator('.referral-banner')).toContainText('PERSIST1')
  })

  test('ignores referral code containing special characters', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/login?ref=BAD!CODE')
    await expect(page.locator('.referral-banner')).toBeHidden()
  })

  test('accepts a referral code at the maximum length boundary (20 chars)', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/login?ref=ABCDEFGHIJ1234567890')
    await expect(page.locator('.referral-banner')).toBeVisible()
    await expect(page.locator('.referral-banner')).toContainText('ABCDEFGHIJ1234567890')
  })

  test('rejects a referral code that exceeds the maximum length (21 chars)', async ({ page }) => {
    setupMockApi(page, {})

    await page.goto('/login?ref=ABCDEFGHIJ12345678901')
    await expect(page.locator('.referral-banner')).toBeHidden()
  })

  test('shows referral discount banner during onboarding for referred players', async ({ page }) => {
    const player = makePlayer({
      id: 'player-ref-onboarding',
      email: 'referred@example.com',
      onboardingCompletedAtUtc: null,
      appliedReferralCode: 'FRIEND10',
    })
    const state = setupMockApi(page, { players: [player] })
    state.currentUserId = player.id
    state.currentToken = `token-${player.id}`
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
    }, `token-${player.id}`)

    await page.goto('/onboarding')
    await expect(page.locator('.referral-onboarding-banner')).toBeVisible()
    await expect(page.locator('.referral-onboarding-banner')).toContainText('FRIEND10')
    await expect(page.locator('.referral-onboarding-banner')).toContainText('10%')
  })
})
