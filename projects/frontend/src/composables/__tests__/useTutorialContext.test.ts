import { beforeEach, describe, expect, it, vi } from 'vitest'

const gqlRequestMock = vi.fn()

const authState = {
  isAuthenticated: true,
}

vi.mock('@/lib/graphql', () => ({
  gqlRequest: (...args: unknown[]) => gqlRequestMock(...args),
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authState,
}))

describe('useTutorialContext', () => {
  beforeEach(() => {
    gqlRequestMock.mockReset()
    authState.isAuthenticated = true
  })

  it('fetchProgress loads tutorial milestones including new contextual milestones', async () => {
    const { useTutorialContext } = await import('../useTutorialContext')
    gqlRequestMock.mockResolvedValueOnce({
      tutorialProgress: [
        {
          milestone: 'FIRST_BUILDING_DETAIL_VISIT',
          isCompleted: true,
          completedAtUtc: '2026-01-01T00:00:00Z',
          bountyAwarded: true,
          bountyAwardedAtUtc: '2026-01-01T00:00:00Z',
          bountyPoints: 30,
        },
        {
          milestone: 'FIRST_GRID_EDITOR_OPEN',
          isCompleted: false,
          completedAtUtc: null,
          bountyAwarded: false,
          bountyAwardedAtUtc: null,
          bountyPoints: 30,
        },
      ],
    })

    const tutorial = useTutorialContext()
    await tutorial.fetchProgress()

    expect(tutorial.milestones.value).toHaveLength(2)
    expect(tutorial.isMilestoneCompleted('FIRST_BUILDING_DETAIL_VISIT')).toBe(true)
    expect(tutorial.isMilestoneCompleted('FIRST_GRID_EDITOR_OPEN')).toBe(false)
  })

  it('completeMilestone is idempotent and does not re-trigger mutation for completed milestone', async () => {
    const { useTutorialContext } = await import('../useTutorialContext')
    gqlRequestMock.mockResolvedValueOnce({
      markTutorialMilestoneComplete: {
        milestone: 'FIRST_GRID_EDITOR_OPEN',
        isCompleted: true,
        completedAtUtc: '2026-01-01T00:00:00Z',
        bountyAwarded: true,
        bountyAwardedAtUtc: '2026-01-01T00:00:00Z',
        bountyPoints: 30,
      },
    })

    const tutorial = useTutorialContext()
    await tutorial.completeMilestone('FIRST_GRID_EDITOR_OPEN')
    await tutorial.completeMilestone('FIRST_GRID_EDITOR_OPEN')

    expect(gqlRequestMock).toHaveBeenCalledTimes(1)
    expect(tutorial.isMilestoneCompleted('FIRST_GRID_EDITOR_OPEN')).toBe(true)
  })

  it('completedCount ignores non-tutorial tracking milestones', async () => {
    const { useTutorialContext } = await import('../useTutorialContext')
    gqlRequestMock.mockResolvedValueOnce({
      tutorialProgress: [
        {
          milestone: 'FIRST_RESOURCE_SOLD',
          isCompleted: true,
          completedAtUtc: '2026-01-01T00:00:00Z',
          bountyAwarded: true,
          bountyAwardedAtUtc: '2026-01-01T00:00:00Z',
          bountyPoints: 50,
        },
        {
          milestone: 'TOOLTIP_DASHBOARD_SHOWN',
          isCompleted: true,
          completedAtUtc: '2026-01-01T00:00:00Z',
          bountyAwarded: false,
          bountyAwardedAtUtc: null,
          bountyPoints: null,
        },
      ],
    })

    const tutorial = useTutorialContext()
    await tutorial.fetchProgress()

    expect(tutorial.completedCount.value).toBe(1)
  })
})
