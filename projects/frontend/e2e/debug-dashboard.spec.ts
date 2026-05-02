import { test, expect } from '@playwright/test'
import { setupMockApi, makePlayer } from './helpers/mock-api'

test('debug dashboard', async ({ page }) => {
  const player = makePlayer({
    onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
    companies: [
      {
        id: 'comp-1',
        playerId: 'player-1',
        name: 'Tick Corp',
        cash: 400000,
        foundedAtUtc: '2026-01-01T00:00:00Z',
        buildings: [],
      },
    ],
  })
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  state.gameState.currentTick = 42

  await page.addInitScript((value) => {
    localStorage.setItem('auth_token', value)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  
  // Log all requests
  page.on('request', req => {
    if (req.url().includes('graphql')) {
      const body = req.postDataJSON()
      const query = body?.query ?? ''
      console.log('REQUEST:', query.substring(0, 120))
    }
  })
  
  page.on('response', async resp => {
    if (resp.url().includes('graphql')) {
      const body = await resp.json().catch(() => null)
      const keys = body?.data ? Object.keys(body.data) : ['no data, errors: ' + JSON.stringify(body?.errors)]
      console.log('RESPONSE:', JSON.stringify(keys))
    }
  })

  await page.goto('/dashboard')
  await page.waitForTimeout(3000)
  
  const snapshot = await page.locator('.tick-clock-widget').count()
  console.log('tick-clock-widget count:', snapshot)
})
