import { MILESTONE_FIRST_RESOURCE_SOLD, MILESTONE_FIRST_B2B_TRADE, MILESTONE_FIRST_LOAN_TAKEN, MILESTONE_FIRST_COMPETITOR_OBSERVED, MILESTONE_FIRST_BRAND_ESTABLISHED, MILESTONE_FIRST_BUILDING_DETAIL_VISIT, MILESTONE_FIRST_GRID_EDITOR_OPEN } from '@/composables/useTutorialContext'

export interface MilestoneDef {
  id: string
  icon: string
  titleKey: string
  descKey: string
  valueKey: string
  bountyPoints: number
  resumeRoute: string
}

export const MILESTONE_DEFS: MilestoneDef[] = [
  {
    id: MILESTONE_FIRST_RESOURCE_SOLD,
    icon: '💰',
    titleKey: 'tutorial.milestones.firstResourceSold.title',
    descKey: 'tutorial.milestones.firstResourceSold.desc',
    valueKey: 'tutorial.milestones.firstResourceSold.value',
    bountyPoints: 50,
    resumeRoute: '/dashboard',
  },
  {
    id: MILESTONE_FIRST_B2B_TRADE,
    icon: '🤝',
    titleKey: 'tutorial.milestones.firstB2BTrade.title',
    descKey: 'tutorial.milestones.firstB2BTrade.desc',
    valueKey: 'tutorial.milestones.firstB2BTrade.value',
    bountyPoints: 75,
    resumeRoute: '/exchange',
  },
  {
    id: MILESTONE_FIRST_LOAN_TAKEN,
    icon: '🏦',
    titleKey: 'tutorial.milestones.firstLoanTaken.title',
    descKey: 'tutorial.milestones.firstLoanTaken.desc',
    valueKey: 'tutorial.milestones.firstLoanTaken.value',
    bountyPoints: 60,
    resumeRoute: '/banking',
  },
  {
    id: MILESTONE_FIRST_COMPETITOR_OBSERVED,
    icon: '🔭',
    titleKey: 'tutorial.milestones.firstCompetitorObserved.title',
    descKey: 'tutorial.milestones.firstCompetitorObserved.desc',
    valueKey: 'tutorial.milestones.firstCompetitorObserved.value',
    bountyPoints: 40,
    resumeRoute: '/market-intelligence',
  },
  {
    id: MILESTONE_FIRST_BRAND_ESTABLISHED,
    icon: '⭐',
    titleKey: 'tutorial.milestones.firstBrandEstablished.title',
    descKey: 'tutorial.milestones.firstBrandEstablished.desc',
    valueKey: 'tutorial.milestones.firstBrandEstablished.value',
    bountyPoints: 80,
    resumeRoute: '/dashboard',
  },
  {
    id: MILESTONE_FIRST_BUILDING_DETAIL_VISIT,
    icon: '🏗️',
    titleKey: 'tutorial.milestones.firstBuildingDetailVisit.title',
    descKey: 'tutorial.milestones.firstBuildingDetailVisit.desc',
    valueKey: 'tutorial.milestones.firstBuildingDetailVisit.value',
    bountyPoints: 30,
    resumeRoute: '/dashboard',
  },
  {
    id: MILESTONE_FIRST_GRID_EDITOR_OPEN,
    icon: '🧩',
    titleKey: 'tutorial.milestones.firstGridEditorOpen.title',
    descKey: 'tutorial.milestones.firstGridEditorOpen.desc',
    valueKey: 'tutorial.milestones.firstGridEditorOpen.value',
    bountyPoints: 30,
    resumeRoute: '/dashboard',
  },
]
