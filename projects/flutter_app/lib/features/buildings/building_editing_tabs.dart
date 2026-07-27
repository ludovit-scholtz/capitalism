// Outer edit-mode tab strip — port of `BuildingOverviewSidebar.vue`'s
// `editTabs` (Basic Data/Energy/Bank Account/Layouts). Shown in the right
// column (desktop) / stacked below the grid (mobile) while
// `controller.isEditing` is true and no cell is selected — once a cell is
// selected, `BuildingUnitEditTabs` takes over instead (matching web's
// mutually-exclusive `BuildingEditingSidebar` vs. edit-mode
// `BuildingOverviewSidebar`).
//
// - Basic Data — thin, matching web's own placeholder: building name/type
//   and a construction-cost summary. Web itself has no rename field wired
//   up here yet either.
// - Energy — building-scoped `BuildingEnergySettingsPanel` (priority/max
//   bid), no unit selected.
// - Bank Account — `BuildingBankAccountTab`.
// - Layouts — trimmed relative to web's `BuildingLayoutsTab.vue`: that tab
//   is a full named-layout save/load library (local + cloud/master-API
//   storage, mini-grid previews, overwrite confirmation) — a materially
//   separate feature from reorganizing this screen's tabs/columns, so it's
//   out of scope here. This tab instead surfaces the most valuable
//   adjacent content: the existing `BuildingDraftSummaryPanel`
//   (save/cancel/copy/paste + change summary) for the plan being edited on
//   the grid to the left.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_bank_account_tab.dart';
import 'building_draft_summary_panel.dart';
import 'building_energy_settings_panel.dart';
import 'building_grid_draft_controller.dart';
import 'building_panel_service.dart';
import 'building_tab_strip.dart';

class BuildingEditingTabs extends StatelessWidget {
  const BuildingEditingTabs({
    super.key,
    required this.controller,
    required this.panelService,
    required this.onSave,
    required this.onCancel,
    required this.onCopy,
    required this.onPaste,
  });

  final BuildingGridDraftController controller;
  final BuildingPanelService panelService;
  final Future<void> Function() onSave;
  final VoidCallback onCancel;
  final Future<void> Function() onCopy;
  final Future<void> Function() onPaste;

  @override
  Widget build(BuildContext context) {
    final building = controller.building;
    if (building == null) return const SizedBox.shrink();

    final tabs = <BuildingTab>[
      BuildingTab(key: 'basicData', label: 'Basic Data', builder: (context) => _basicDataTab(context, building.name, building.type)),
      BuildingTab(
        key: 'energy',
        label: 'Energy',
        builder: (context) => BuildingEnergySettingsPanel(
          buildingId: building.id,
          buildingType: building.type,
          currentPriority: building.powerPriority,
          currentMaxBidPrice: building.maxEnergyBidPrice,
          panelService: panelService,
        ),
      ),
      BuildingTab(key: 'bankAccount', label: 'Bank Account', builder: (context) => BuildingBankAccountTab(buildingId: building.id, panelService: panelService)),
      BuildingTab(
        key: 'layouts',
        label: 'Layouts',
        builder: (context) => BuildingDraftSummaryPanel(controller: controller, onSave: onSave, onCancel: onCancel, onCopy: onCopy, onPaste: onPaste),
      ),
    ];

    return BuildingTabStrip(key: ValueKey('editing-tabs-${building.id}'), tabs: tabs);
  }

  Widget _basicDataTab(BuildContext context, String name, String type) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(name, style: theme.textTheme.titleMedium),
            Text(type, style: theme.textTheme.bodySmall),
            const SizedBox(height: AppSpacing.md),
            Text('Cost summary', style: theme.textTheme.labelLarge),
            const SizedBox(height: 4),
            Text('Total build cost: ${controller.draftConstructionCost.toStringAsFixed(2)}'),
            if (controller.projectedCompanyCashAfterApply != null) Text('Cash after apply: ${controller.projectedCompanyCashAfterApply!.toStringAsFixed(2)}'),
          ],
        ),
      ),
    );
  }
}
