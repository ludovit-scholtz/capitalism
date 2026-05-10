import { expect, test } from '@playwright/test'
import {
  loginAs,
  type MockRankingBountyDashboardItem,
  makePlayer,
  setupMockApi,
  type MockRankingBountyDefinition,
  type MockRankingEventModerationItem,
} from './helpers/mock-api'

test.describe('Ranking pages', () => {
  test('auto-opens page containing current player and highlights row', async ({ page }) => {
    const player = makePlayer({
      id: 'rank-player-25',
      email: 'rank25@example.com',
      displayName: 'Rank Twenty Five',
    })

    const rankingLeaderboard = Array.from({ length: 30 }, (_, index) => {
      if (index === 24) {
        return {
          playerId: player.id,
          displayName: player.displayName,
          totalPoints: 750,
          globalRank: 25,
          rankMovement: 1,
        }
      }

      return {
        playerId: `rank-player-${index + 1}`,
        displayName: `Rank Player ${index + 1}`,
        totalPoints: 1000 - index * 10,
        globalRank: index + 1,
        rankMovement: 0,
      }
    })

    const state = setupMockApi(page, {
      rankingLeaderboard,
      rankingSummary: {
        totalPoints: 750,
        globalRank: 25,
        previousGlobalRank: 26,
        rankMovement: 1,
        updatedAtUtc: '2026-05-01T00:00:00.000Z',
      },
    })
    await loginAs(page, state, player)

    await page.goto('/ranking')
    await expect(page).toHaveURL(/\/ranking\?page=3/)
    await expect(page.getByText('Page 3')).toBeVisible()
    const playerRow = page.locator('tr', { hasText: 'Rank Twenty Five' })
    await expect(playerRow).toHaveAttribute('aria-current', 'true')
    await expect(playerRow).toContainText('You')
  })

  test('player can view ranking dashboard and bounty history', async ({ page }) => {
    const player = makePlayer({
      id: 'rank-player-1',
      email: 'rank1@example.com',
      displayName: 'Rank One',
    })
    const state = setupMockApi(page, {
      rankingLeaderboard: [
        {
          playerId: 'rank-player-1',
          displayName: 'Rank One',
          totalPoints: 250,
          globalRank: 1,
          rankMovement: 2,
        },
        {
          playerId: 'rank-player-2',
          displayName: 'Rank Two',
          totalPoints: 200,
          globalRank: 2,
          rankMovement: -1,
        },
      ],
      rankingBountyDashboard: [
        {
          id: 'bounty-dashboard-improver',
          code: 'GAME_IMPROVER',
          displayName: 'Game improver',
          description: 'Submit a suggestion or bug report through support.',
          rewardPoints: 5,
          cooldownMode: 'UTC_DAY',
          proofRequirement: 'NONE',
          requiresModeration: false,
          awardedToday: true,
          isAvailableNow: false,
          nextAvailableAtUtc: '2026-05-01T00:00:00.000Z',
          lastAwardedAtUtc: '2026-04-30T10:00:00.000Z',
          totalAwards: 4,
        } satisfies MockRankingBountyDashboardItem,
      ],
      rankingHistory: [
        {
          id: 'reward-100',
          bountyCode: 'GAME_IMPROVER',
          bountyDisplayName: 'Game improver',
          pointsAwarded: 5,
          status: 'AWARDED',
          serverKey: 'capitalism-eu-1',
          eventDateUtc: '2026-04-30T10:00:00.000Z',
          awardedAtUtc: '2026-04-30T10:00:00.000Z',
          metadataJson: '{}',
        },
      ],
    })
    await loginAs(page, state, player)

    await page.goto('/ranking')
    await expect(page.getByRole('heading', { name: 'Master Ranking Dashboard' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Rank One' })).toBeVisible()
    await expect(
      page.locator('table[aria-label="Master ranking leaderboard table"]'),
    ).toContainText('Rank Two')

    await page.goto('/ranking/bounties')
    await expect(page.getByRole('heading', { name: 'Bounty Dashboard' })).toBeVisible()
    await expect(
      page.locator('table[aria-label="Ranking bounties dashboard table"]'),
    ).toContainText('Game improver')
    await expect(
      page.locator('table[aria-label="Ranking bounties dashboard table"]'),
    ).toContainText('Granted today')

    await page.goto('/ranking/bounties/history')
    await expect(page.getByRole('heading', { name: 'Bounty History' })).toBeVisible()
    await expect(page.locator('table[aria-label="Ranking bounty history table"]')).toContainText(
      'Game improver',
    )

    await page.getByLabel('Proof reference').fill('https://x.com/demo/status/100')
    await page.getByRole('button', { name: 'Submit proof' }).click()
    await expect(page.getByRole('status')).toContainText('Proof event submitted successfully')
  })

  test('global admin can moderate ranking events and trigger scheduler runs', async ({ page }) => {
    const admin = makePlayer({
      id: 'rank-admin-1',
      email: 'root@example.com',
      displayName: 'Root Admin',
    })

    const moderationEvent: MockRankingEventModerationItem = {
      id: 'rank-event-1',
      eventType: 'RETWEET_X_POST',
      playerEmail: 'rank1@example.com',
      serverKey: 'capitalism-eu-1',
      proofReference: 'https://x.com/demo/status/200',
      payloadJson: '{"tags":2}',
      status: 'PENDING_MODERATION',
      occurredAtUtc: '2026-04-30T11:00:00.000Z',
      createdAtUtc: '2026-04-30T11:00:00.000Z',
    }

    const bounty: MockRankingBountyDefinition = {
      id: 'bounty-retweet',
      code: 'RETWEET_X_POST',
      displayName: 'Retweet a X post',
      description: 'Submit a verified retweet proof',
      rewardPoints: 5,
      isEnabled: true,
      isVisibleToPlayers: false,
      requiresModeration: true,
      cooldownMode: 'PER_UNIQUE_KEY',
      sourceEventType: 'RETWEET_X_POST',
      proofRequirement: 'URL',
      visibilityScope: 'ADMIN_ONLY',
      validationSettingsJson: '{}',
      updatedAtUtc: '2026-04-30T11:00:00.000Z',
    }

    const state = setupMockApi(page, {
      isGlobalAdmin: true,
      rankingModerationEvents: [moderationEvent],
      rankingBounties: [bounty],
      rankingRuns: [],
      rankingTelemetryBatches: [
        {
          batchId: 'batch-1',
          serverKeyMasked: 'capi****eu-1',
          flagReasonCode: 'DUPLICATE_EVENT_SIGNATURE',
          eventCount: 2,
          isQuarantined: false,
          hasAppliedLeaderboardImpact: true,
          quarantineReason: null,
          clearJustification: null,
          createdAtUtc: '2026-04-30T11:00:00.000Z',
          lastAttemptAtUtc: '2026-04-30T11:01:00.000Z',
        },
      ],
    })
    await loginAs(page, state, admin)
    await page.addInitScript(() => {
      window.prompt = (message?: string) => {
        if (message?.includes('quarantine')) return 'Replay attack suspected'
        if (message?.includes('clear')) return 'False positive after review'
        return 'note'
      }
    })

    await page.goto('/ranking/admin')
    await expect(page.getByRole('heading', { name: 'Ranking Administration' })).toBeVisible()
    await expect(page.locator('table[aria-label="Ranking moderation queue table"]')).toContainText(
      'RETWEET_X_POST',
    )

    await page.getByRole('button', { name: 'Approve' }).first().click()
    await expect(page.getByRole('status')).toContainText('Ranking event approved.')

    await page.getByRole('button', { name: 'Run hourly evaluator now' }).click()
    await expect(page.getByRole('status')).toContainText('Hourly evaluator run created')

    await page.getByRole('button', { name: 'Run daily decay now' }).click()
    await expect(page.getByRole('status')).toContainText('Daily decay run created')

    await expect(page.locator('table[aria-label="Ranking integrity batch table"]')).toContainText(
      'DUPLICATE_EVENT_SIGNATURE',
    )
    await page.getByRole('button', { name: 'Quarantine', exact: true }).click()
    await expect(page.getByRole('status')).toContainText('Telemetry batch quarantined.')
    await page.getByRole('button', { name: 'Clear quarantine', exact: true }).click()
    await expect(page.getByRole('status')).toContainText('Telemetry batch quarantine cleared.')
  })

  test('player can submit Discord bounty proof via the bounty code selector', async ({ page }) => {
    const player = makePlayer({
      id: 'rank-discord-player',
      email: 'discord@example.com',
      displayName: 'Discord Player',
    })

    const state = setupMockApi(page, { rankingHistory: [] })
    await loginAs(page, state, player)

    await page.goto('/ranking/bounties/history')
    await expect(page.getByRole('heading', { name: 'Bounty History' })).toBeVisible()

    // Select DISCORD_PLAYER from bounty code dropdown (scoped to proof submission panel).
    const proofPanel = page.locator('[aria-label="Proof submission panel"]')
    await expect(proofPanel).toBeVisible()
    await proofPanel.locator('select').selectOption('DISCORD_PLAYER')
    await page.getByLabel('Proof reference').fill('DiscordPlayer#9988')
    await page.getByRole('button', { name: 'Submit proof' }).click()
    await expect(page.getByRole('status')).toContainText('Proof event submitted successfully')

    // Verify the event was added to the moderation queue in mock state.
    const submittedEvent = state.rankingModerationEvents.find(
      (e) => e.eventType === 'DISCORD_PLAYER',
    )
    expect(submittedEvent).toBeDefined()
    expect(submittedEvent?.proofReference).toBe('DiscordPlayer#9988')
  })

  test('admin moderation queue shows proof reference for pending retweet events', async ({
    page,
  }) => {
    const admin = makePlayer({
      id: 'rank-privacy-admin',
      email: 'root@example.com',
      displayName: 'Root Admin',
    })

    const pendingEvent: MockRankingEventModerationItem = {
      id: 'rank-privacy-event',
      eventType: 'RETWEET_X_POST',
      playerEmail: 'player@example.com',
      serverKey: null,
      proofReference: 'https://x.com/player/status/secret-url-999',
      payloadJson: '{}',
      status: 'PENDING_MODERATION',
      occurredAtUtc: '2026-04-30T12:00:00.000Z',
      createdAtUtc: '2026-04-30T12:00:00.000Z',
    }

    const state = setupMockApi(page, {
      isGlobalAdmin: true,
      rankingModerationEvents: [pendingEvent],
      rankingBounties: [],
      rankingRuns: [],
    })
    await loginAs(page, state, admin)

    await page.goto('/ranking/admin')

    // Admin can see the proof reference URL in the moderation queue table.
    await expect(page.locator('table[aria-label="Ranking moderation queue table"]')).toContainText(
      'https://x.com/player/status/secret-url-999',
    )

    // The player email is also visible (admin context).
    await expect(page.locator('table[aria-label="Ranking moderation queue table"]')).toContainText(
      'player@example.com',
    )
  })
})
