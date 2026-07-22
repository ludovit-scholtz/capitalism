// Data model for the Tutorial screen, mirroring
// `projects/frontend/src/composables/useTutorialContext.ts`. GraphQL
// field names verified against `Api/Types/Query.Tutorial.cs` (or
// equivalent) — the `tutorialProgress` query.

class TutorialMilestoneStatus {
  const TutorialMilestoneStatus({
    required this.milestone,
    required this.isCompleted,
    required this.bountyAwarded,
    required this.bountyPoints,
  });

  final String milestone;
  final bool isCompleted;
  final bool bountyAwarded;
  final int bountyPoints;

  factory TutorialMilestoneStatus.fromJson(Map<String, dynamic> json) => TutorialMilestoneStatus(
    milestone: json['milestone'] as String,
    isCompleted: json['isCompleted'] as bool? ?? false,
    bountyAwarded: json['bountyAwarded'] as bool? ?? false,
    bountyPoints: (json['bountyPoints'] as num?)?.toInt() ?? 0,
  );
}

class MilestoneDef {
  const MilestoneDef({
    required this.id,
    required this.icon,
    required this.title,
    required this.description,
    required this.bountyPoints,
    required this.resumeRoute,
  });

  final String id;
  final String icon;
  final String title;
  final String description;
  final int bountyPoints;
  final String resumeRoute;
}

/// Mirrors `MILESTONE_DEFS` in `TutorialView.vue`.
const List<MilestoneDef> tutorialMilestoneDefs = [
  MilestoneDef(id: 'FIRST_RESOURCE_SOLD', icon: '💰', title: 'First Sale', description: 'Sell a resource on the public market.', bountyPoints: 50, resumeRoute: '/dashboard'),
  MilestoneDef(id: 'FIRST_B2B_TRADE', icon: '🤝', title: 'First B2B Trade', description: 'Buy or sell through the global exchange.', bountyPoints: 75, resumeRoute: '/exchange'),
  MilestoneDef(id: 'FIRST_LOAN_TAKEN', icon: '🏦', title: 'First Loan', description: 'Take out a loan from a bank.', bountyPoints: 60, resumeRoute: '/banking'),
  MilestoneDef(id: 'FIRST_COMPETITOR_OBSERVED', icon: '🔭', title: 'Scout a Competitor', description: 'Check market intelligence for a city.', bountyPoints: 40, resumeRoute: '/market-intelligence'),
  MilestoneDef(id: 'FIRST_BRAND_ESTABLISHED', icon: '⭐', title: 'Establish a Brand', description: 'Build brand quality for a product.', bountyPoints: 80, resumeRoute: '/dashboard'),
  MilestoneDef(id: 'FIRST_BUILDING_DETAIL_VISIT', icon: '🏗️', title: 'Inspect a Building', description: 'Open a building\'s detail screen.', bountyPoints: 30, resumeRoute: '/dashboard'),
  MilestoneDef(id: 'FIRST_GRID_EDITOR_OPEN', icon: '🧩', title: 'Configure a Building', description: 'Open a building\'s unit configuration.', bountyPoints: 30, resumeRoute: '/dashboard'),
];
