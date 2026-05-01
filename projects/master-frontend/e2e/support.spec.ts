import { expect, test } from '@playwright/test'
import { loginAs, makePlayer, makeSupportTicket, setupMockApi } from './helpers/mock-api'

test.describe('Support tickets', () => {
  test('user can submit ticket with markdown editor and see it in personal list', async ({
    page,
  }) => {
    const player = makePlayer({
      id: 'player-user-1',
      email: 'user1@example.com',
      displayName: 'User 1',
    })
    const state = setupMockApi(page, {})
    await loginAs(page, state, player)

    await page.goto('/support/new')

    await page.getByLabel('Ticket type').selectOption('BUG')
    await page.getByLabel('Ticket title').fill('Factory layout issue')

    const editor = page.locator('.CodeMirror').first()
    await editor.click()
    await page.keyboard.type(
      'The factory layout save flow is broken when linking units in rapid sequence.',
    )

    await page.getByRole('button', { name: 'Submit ticket' }).click()

    await expect(page).toHaveURL(/\/support\/tickets/)
    await expect(page.getByRole('status')).toContainText('Support ticket submitted.')
    await expect(page.locator('table[aria-label="My support tickets table"]')).toContainText(
      'Factory layout issue',
    )
  })

  test('user ticket table filtering and sorting works', async ({ page }) => {
    const player = makePlayer({
      id: 'player-user-2',
      email: 'user2@example.com',
      displayName: 'User 2',
    })
    const state = setupMockApi(page, {
      supportTickets: [
        makeSupportTicket({
          id: 'ticket-a',
          createdByPlayerId: player.id,
          createdByEmail: player.email,
          createdByDisplayName: player.displayName,
          ticketType: 'BUG',
          title: 'Zeta ticket',
          createdAtUtc: '2026-04-20T12:00:00.000Z',
          updatedAtUtc: '2026-04-20T12:00:00.000Z',
        }),
        makeSupportTicket({
          id: 'ticket-b',
          createdByPlayerId: player.id,
          createdByEmail: player.email,
          createdByDisplayName: player.displayName,
          ticketType: 'SUGGESTION',
          title: 'Alpha ticket',
          createdAtUtc: '2026-04-25T12:00:00.000Z',
          updatedAtUtc: '2026-04-25T12:00:00.000Z',
        }),
      ],
    })
    await loginAs(page, state, player)

    await page.goto('/support/tickets')

    await page.getByLabel('Filter type').selectOption('SUGGESTION')
    await page.getByRole('button', { name: 'Apply' }).click()
    await expect(page.locator('table[aria-label="My support tickets table"]')).toContainText(
      'Alpha ticket',
    )
    await expect(page.locator('table[aria-label="My support tickets table"]')).not.toContainText(
      'Zeta ticket',
    )

    await page.getByLabel('Filter type').selectOption('')
    await page.getByLabel('Sort by').selectOption('TITLE')
    await page.getByLabel('Sort direction').selectOption('ASC')
    await page.getByRole('button', { name: 'Apply' }).click()

    const firstRowTitle = page
      .locator('table[aria-label="My support tickets table"] tbody tr')
      .first()
      .locator('td')
      .nth(1)
    await expect(firstRowTitle).toContainText('Alpha ticket')
  })

  test('admin moderation approval flow unlocks sanitized preview', async ({ page }) => {
    const admin = makePlayer({
      id: 'player-admin-1',
      email: 'root@example.com',
      displayName: 'Root Admin',
    })
    const state = setupMockApi(page, {
      isGlobalAdmin: true,
      supportTickets: [
        makeSupportTicket({
          id: 'ticket-unsafe',
          createdByPlayerId: 'player-other',
          createdByEmail: 'other@example.com',
          createdByDisplayName: 'Other',
          title: 'Unsafe markdown payload',
          containsUnsafeContent: true,
          moderationState: 'PENDING',
          markdownSource: '<script>alert(1)</script> hello',
          extractedUrls: ['javascript:alert(1)'],
        }),
      ],
    })
    await loginAs(page, state, admin)

    await page.goto('/support/admin')

    await page.locator('table[aria-label="Admin support tickets table"] tbody tr').first().click()
    await expect(page.getByRole('heading', { name: 'Unsafe markdown payload' })).toBeVisible()
    await expect(page.getByText('javascript:alert(1)')).toBeVisible()

    await page.getByLabel('Moderation note').fill('Safe to render after review')
    await page.getByRole('button', { name: 'Approve preview' }).click()

    await expect(page.getByRole('status')).toContainText('Ticket moderation approved.')
    await expect(page.locator('.preview-html')).toBeVisible()
  })

  test('visibility differs between user and admin ticket pages', async ({ page }) => {
    const user = makePlayer({
      id: 'player-user-3',
      email: 'user3@example.com',
      displayName: 'User 3',
    })
    const admin = makePlayer({
      id: 'player-admin-2',
      email: 'root@example.com',
      displayName: 'Root Admin',
    })

    const state = setupMockApi(page, {
      supportTickets: [
        makeSupportTicket({
          id: 'ticket-user-own',
          createdByPlayerId: user.id,
          createdByEmail: user.email,
          createdByDisplayName: user.displayName,
          title: 'User visible ticket',
        }),
        makeSupportTicket({
          id: 'ticket-foreign',
          createdByPlayerId: 'player-foreign',
          createdByEmail: 'foreign@example.com',
          createdByDisplayName: 'Foreign',
          title: 'Admin only foreign ticket',
        }),
      ],
    })

    await loginAs(page, state, user)
    await page.goto('/support/tickets')
    await expect(page.locator('table[aria-label="My support tickets table"]')).toContainText(
      'User visible ticket',
    )
    await expect(page.locator('table[aria-label="My support tickets table"]')).not.toContainText(
      'Admin only foreign ticket',
    )

    state.isGlobalAdmin = true
    await loginAs(page, state, admin, 'admin-token')
    await page.goto('/support/admin')
    await expect(page.locator('table[aria-label="Admin support tickets table"]')).toContainText(
      'User visible ticket',
    )
    await expect(page.locator('table[aria-label="Admin support tickets table"]')).toContainText(
      'Admin only foreign ticket',
    )
  })
})
