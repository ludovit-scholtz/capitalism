// The editable "planned configuration" grid — port of the `v-if="isEditing"`
// half of `BuildingUnitGrid.vue`. Renders the same 4x4 layout as the
// read-only `BuildingUnitGrid` (`building_unit_grid.dart`) but with tappable
// cells (open the unit picker / select for the config sheet) and the full
// 8-directional link connector grid wired to
// `BuildingGridDraftController`. Cell/connector sizing is width-aware
// (`building_grid_sizing.dart`), matching the read-only grid, so it also
// scales to fit the left column of the responsive layout on wide screens.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_grid_draft_controller.dart';
import 'building_grid_sizing.dart';
import 'building_link_connector_widgets.dart';
import 'building_link_helpers.dart';
import 'building_unit_grid.dart';

class BuildingGridEditor extends StatelessWidget {
  const BuildingGridEditor({super.key, required this.controller, required this.itemNameFor, required this.onCellTap});

  final BuildingGridDraftController controller;
  final String Function(String? resourceTypeId, String? productTypeId) itemNameFor;

  /// Called after `clickDraftCell` runs — lets the screen open the unit
  /// picker (empty cell) or the config sheet (occupied cell) based on
  /// `controller.showUnitPicker`.
  final void Function(int x, int y) onCellTap;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final sizing = computeGridSizing(constraints.maxWidth);
        final grid = SizedBox(
          width: sizing.totalWidth,
          child: Column(
            children: [
              for (var y = 0; y < 4; y++) ...[
                _unitRow(context, y, sizing),
                if (y < 3) _connectorRow(context, y, sizing),
              ],
            ],
          ),
        );
        if (!sizing.scrollable) return grid;
        return SingleChildScrollView(scrollDirection: Axis.horizontal, child: grid);
      },
    );
  }

  Widget _unitRow(BuildContext context, int y, GridSizing sizing) {
    return Row(
      children: [
        for (var x = 0; x < 4; x++) ...[
          _cell(context, x, y, sizing),
          if (x < 3)
            LinkConnectorButton(
              orientation: LinkOrientation.horizontal,
              state: getHorizontalLinkState(controller.draftUnits, x, y),
              canToggle: canToggleHorizontalLink(controller.draftUnits, x, y),
              size: sizing.connectorSize,
              thickness: sizing.cellSize,
              onTap: () => controller.toggleHorizontalLink(x, y),
            ),
        ],
      ],
    );
  }

  Widget _connectorRow(BuildContext context, int y, GridSizing sizing) {
    return Row(
      children: [
        for (var x = 0; x < 4; x++) ...[
          LinkConnectorButton(
            orientation: LinkOrientation.vertical,
            state: getVerticalLinkState(controller.draftUnits, x, y),
            canToggle: canToggleVerticalLink(controller.draftUnits, x, y),
            size: sizing.connectorSize,
            thickness: sizing.cellSize,
            onTap: () => controller.toggleVerticalLink(x, y),
          ),
          if (x < 3)
            DiagonalConnectorWidget(
              primaryState: getPrimaryDiagonalLinkState(controller.draftUnits, x, y),
              secondaryState: getSecondaryDiagonalLinkState(controller.draftUnits, x, y),
              canTogglePrimary: canTogglePrimaryDiagonalLink(controller.draftUnits, x, y),
              canToggleSecondary: canToggleSecondaryDiagonalLink(controller.draftUnits, x, y),
              size: sizing.connectorSize,
              onTogglePrimary: () => controller.togglePrimaryDiagonalLink(x, y),
              onToggleSecondary: () => controller.toggleSecondaryDiagonalLink(x, y),
            ),
        ],
      ],
    );
  }

  Widget _cell(BuildContext context, int x, int y, GridSizing sizing) {
    final theme = Theme.of(context);
    final unit = controller.draftUnitAt(x, y);
    final isSelected = controller.selectedCell?.x == x && controller.selectedCell?.y == y;

    if (unit == null) {
      return SizedBox(
        width: sizing.cellSize,
        height: sizing.cellSize,
        child: Padding(
          padding: const EdgeInsets.all(2),
          child: Material(
            color: Colors.transparent,
            child: InkWell(
              key: ValueKey('draft-cell-$x-$y'),
              borderRadius: BorderRadius.circular(AppRadius.md),
              onTap: () {
                controller.clickDraftCell(x, y);
                onCellTap(x, y);
              },
              child: DecoratedBox(
                decoration: BoxDecoration(
                  border: Border.all(color: isSelected ? theme.colorScheme.primary : theme.colorScheme.outlineVariant),
                  borderRadius: BorderRadius.circular(AppRadius.md),
                ),
                child: Icon(Icons.add, color: theme.colorScheme.outlineVariant, size: 18),
              ),
            ),
          ),
        ),
      );
    }

    final accent = unitTypeColors[unit.unitType] ?? theme.colorScheme.primary;
    final itemName = itemNameFor(unit.resourceTypeId, unit.productTypeId);
    return SizedBox(
      width: sizing.cellSize,
      height: sizing.cellSize,
      child: Padding(
        padding: const EdgeInsets.all(2),
        child: Material(
          color: theme.colorScheme.surfaceContainer,
          borderRadius: BorderRadius.circular(AppRadius.md),
          child: InkWell(
            key: ValueKey('draft-cell-unit-${unit.id}'),
            borderRadius: BorderRadius.circular(AppRadius.md),
            onTap: () {
              controller.clickDraftCell(x, y);
              onCellTap(x, y);
            },
            child: Container(
              padding: const EdgeInsets.all(AppSpacing.xs),
              decoration: BoxDecoration(
                border: Border.all(color: isSelected ? theme.colorScheme.primary : accent.withValues(alpha: 0.7), width: isSelected ? 3 : 2),
                borderRadius: BorderRadius.circular(AppRadius.md),
              ),
              // See `building_unit_grid.dart`'s matching cell for why this is
              // wrapped in a `FittedBox` rather than relying on a minimum
              // cell size alone.
              child: FittedBox(
                fit: BoxFit.scaleDown,
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    CircleAvatar(
                      radius: 10,
                      backgroundColor: accent.withValues(alpha: 0.2),
                      child: Text(
                        unitTypeShortLabel(unit.unitType).characters.first.toUpperCase(),
                        style: theme.textTheme.labelSmall?.copyWith(color: accent, fontWeight: FontWeight.bold),
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text('Lvl ${unit.level}', style: theme.textTheme.labelSmall, maxLines: 1, overflow: TextOverflow.ellipsis),
                    if (itemName.isNotEmpty)
                      Text(itemName, style: theme.textTheme.labelSmall, maxLines: 1, overflow: TextOverflow.ellipsis, textAlign: TextAlign.center),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
