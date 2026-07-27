// Mobile (narrow-screen) container for the edit-mode selected-unit tabs
// (`BuildingUnitEditPane`/`BuildingUnitEditTabs` —
// Config/Energy/Performance/Maintenance). On wide screens the same pane
// renders inline in the right column instead of inside this bottom sheet —
// the tab structure and data-fetching are identical everywhere
// (`BuildingUnitEditPane` is self-fetching), only the container differs:
// a modal sheet for focused editing on a small grid vs. a persistent
// sidebar once there's room for one.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_detail_service.dart';
import 'building_grid_draft_controller.dart';
import 'building_grid_models.dart';
import 'building_panel_service.dart';
import 'building_unit_edit_tabs.dart';

class UnitConfigSheet extends StatelessWidget {
  const UnitConfigSheet({
    super.key,
    required this.controller,
    required this.service,
    required this.panelService,
    required this.unit,
    required this.resourceNames,
    required this.productNames,
    required this.onChanged,
    required this.onRemove,
  });

  final BuildingGridDraftController controller;
  final BuildingDetailService service;
  final BuildingPanelService panelService;
  final EditableGridUnit unit;
  final Map<String, String> resourceNames;
  final Map<String, String> productNames;
  final VoidCallback onChanged;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    return DraggableScrollableSheet(
      initialChildSize: 0.75,
      minChildSize: 0.4,
      maxChildSize: 0.95,
      expand: false,
      builder: (context, scrollController) => SafeArea(
        child: ListView(
          controller: scrollController,
          padding: const EdgeInsets.all(AppSpacing.md),
          children: [
            BuildingUnitEditPane(
              controller: controller,
              service: service,
              panelService: panelService,
              unit: unit,
              resourceNames: resourceNames,
              productNames: productNames,
              onChanged: onChanged,
              onRemove: onRemove,
              onClose: () => Navigator.of(context).maybePop(),
            ),
          ],
        ),
      ),
    );
  }
}
