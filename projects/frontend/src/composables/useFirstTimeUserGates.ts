import { ref, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import type { TutorialMilestoneStatus } from '@/types'

// ─── Tooltip milestone identifiers ────────────────────────────────────────────

export const TOOLTIP_DASHBOARD_SHOWN = 'TOOLTIP_DASHBOARD_SHOWN'
export const TOOLTIP_BUILDING_DETAIL_SHOWN = 'TOOLTIP_BUILDING_DETAIL_SHOWN'
export const TOOLTIP_GRID_EDITOR_SHOWN = 'TOOLTIP_GRID_EDITOR_SHOWN'

// ─── Session storage keys (fallback for unauthenticated users) ────────────────

const SS_DASHBOARD = 'tt_dashboard_dismissed'
const SS_BUILDING_DETAIL = 'tt_building_detail_dismissed'
const SS_GRID_EDITOR = 'tt_grid_editor_dismissed'

function ssGet(key: string): boolean {
  try {
    return typeof sessionStorage !== 'undefined' && sessionStorage.getItem(key) === '1'
  } catch {
    return false
  }
}

function ssSet(key: string): void {
  try {
    if (typeof sessionStorage !== 'undefined') sessionStorage.setItem(key, '1')
  } catch {
    /* noop */
  }
}

// ─── GraphQL ──────────────────────────────────────────────────────────────────

const MARK_TOOLTIP_MUTATION = `
  mutation MarkTutorialMilestoneComplete($input: MarkTutorialMilestoneCompleteInput!) {
    markTutorialMilestoneComplete(input: $input) {
      milestone
      isCompleted
      completedAtUtc
    }
  }
`

// ─── Composable ───────────────────────────────────────────────────────────────

/**
 * Manages first-time user gate tooltips for the dashboard and building detail
 * views. State is persisted to sessionStorage immediately (so it survives same-
 * session navigations) and asynchronously to the backend (so it survists future
 * sessions for authenticated players).
 */
export function useFirstTimeUserGates() {
  const auth = useAuthStore()

  // ── reactive dismissed flags ────────────────────────────────────────────────
  const dashboardDismissed = ref(ssGet(SS_DASHBOARD))
  const buildingDetailDismissed = ref(ssGet(SS_BUILDING_DETAIL))
  const gridEditorDismissed = ref(ssGet(SS_GRID_EDITOR))

  // ── computed visibility ─────────────────────────────────────────────────────
  const showDashboardTooltip = computed(() => !dashboardDismissed.value)
  const showBuildingDetailTooltip = computed(() => !buildingDetailDismissed.value)
  const showGridEditorTooltip = computed(() => !gridEditorDismissed.value)

  // ── backend sync helper ─────────────────────────────────────────────────────
  async function persistToBackend(milestone: string): Promise<void> {
    if (!auth.isAuthenticated) return
    try {
      await gqlRequest<{ markTutorialMilestoneComplete: TutorialMilestoneStatus }>(
        MARK_TOOLTIP_MUTATION,
        { input: { milestone } },
      )
    } catch {
      // Non-critical: silently ignore persistence failures
    }
  }

  /**
   * Hydrates dismissed state from the backend for the authenticated player.
   * Call once on mount of views that show these tooltips.
   */
  async function hydrateFromBackend(
    milestones: Array<{ milestone: string; isCompleted: boolean }>,
  ): Promise<void> {
    for (const m of milestones) {
      if (!m.isCompleted) continue
      if (m.milestone === TOOLTIP_DASHBOARD_SHOWN) {
        dashboardDismissed.value = true
        ssSet(SS_DASHBOARD)
      } else if (m.milestone === TOOLTIP_BUILDING_DETAIL_SHOWN) {
        buildingDetailDismissed.value = true
        ssSet(SS_BUILDING_DETAIL)
      } else if (m.milestone === TOOLTIP_GRID_EDITOR_SHOWN) {
        gridEditorDismissed.value = true
        ssSet(SS_GRID_EDITOR)
      }
    }
  }

  // ── dismiss actions ─────────────────────────────────────────────────────────

  async function dismissDashboardTooltip(): Promise<void> {
    dashboardDismissed.value = true
    ssSet(SS_DASHBOARD)
    await persistToBackend(TOOLTIP_DASHBOARD_SHOWN)
  }

  async function dismissBuildingDetailTooltip(): Promise<void> {
    buildingDetailDismissed.value = true
    ssSet(SS_BUILDING_DETAIL)
    await persistToBackend(TOOLTIP_BUILDING_DETAIL_SHOWN)
  }

  async function dismissGridEditorTooltip(): Promise<void> {
    gridEditorDismissed.value = true
    ssSet(SS_GRID_EDITOR)
    await persistToBackend(TOOLTIP_GRID_EDITOR_SHOWN)
  }

  return {
    showDashboardTooltip,
    showBuildingDetailTooltip,
    showGridEditorTooltip,
    dashboardDismissed,
    buildingDetailDismissed,
    gridEditorDismissed,
    hydrateFromBackend,
    dismissDashboardTooltip,
    dismissBuildingDetailTooltip,
    dismissGridEditorTooltip,
  }
}
