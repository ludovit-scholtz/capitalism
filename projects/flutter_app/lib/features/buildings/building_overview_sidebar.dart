// Building-level default overview — port of `BuildingOverviewSidebar.vue`.
// Shown in the right column (desktop) / stacked below the grid (mobile)
// when no unit is selected and the building is not being edited. Tabs:
// - Overview — financial timeline + recent activity (already fetched at
//   screen load via `_loadBuildingAnalytics`).
// - Supply Chain — `FACTORY` only, once diagram data has loaded. Trimmed
//   relative to web: shown purely off `supplyChain != null && units
//   isNotEmpty` rather than also covering the "still loading, has active
//   units" transitional state web additionally gates on — a minor,
//   documented simplification (avoids threading a dedicated supply-chain
//   loading flag through the screen for a tab that only flickers in for a
//   moment either way).
// - Bank Account — `BuildingBankAccountTab`.

import 'package:flutter/material.dart';

import 'building_analytics_models.dart';
import 'building_bank_account_tab.dart';
import 'building_financial_timeline_panel.dart';
import 'building_panel_service.dart';
import 'building_recent_activity_panel.dart';
import 'building_supply_chain_diagram.dart';
import 'building_tab_strip.dart';

class BuildingOverviewSidebar extends StatelessWidget {
  const BuildingOverviewSidebar({
    super.key,
    required this.buildingId,
    required this.buildingType,
    required this.financialTimeline,
    required this.recentActivity,
    required this.supplyChain,
    required this.panelService,
  });

  final String buildingId;
  final String buildingType;
  final BuildingFinancialTimeline? financialTimeline;
  final List<BuildingRecentActivityEvent> recentActivity;
  final BuildingSupplyChainDiagram? supplyChain;
  final BuildingPanelService panelService;

  @override
  Widget build(BuildContext context) {
    final showSupplyChain = buildingType == 'FACTORY' && (supplyChain?.units.isNotEmpty ?? false);
    final tabs = <BuildingTab>[
      BuildingTab(
        key: 'overview',
        label: 'Overview',
        builder: (context) => Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            BuildingFinancialTimelinePanel(timeline: financialTimeline),
            const SizedBox(height: 12),
            BuildingRecentActivityPanel(events: recentActivity),
          ],
        ),
      ),
      if (showSupplyChain) BuildingTab(key: 'supplyChain', label: 'Supply Chain', builder: (context) => BuildingSupplyChainDiagramView(diagram: supplyChain)),
      BuildingTab(key: 'bankAccount', label: 'Bank Account', builder: (context) => BuildingBankAccountTab(buildingId: buildingId, panelService: panelService)),
    ];

    return BuildingTabStrip(key: ValueKey('overview-sidebar-$buildingId'), tabs: tabs);
  }
}
