// Read-only port of the web's 4x4 grid layout
// (`gridIndexes = [0,1,2,3]` in `useBuildingDetail.ts`, rendered by
// `BuildingUnitGrid.vue`'s "Active Configuration" grid). Renders units at
// their `gridX`/`gridY` (each 0..3) instead of a flat list, so the physical
// layout matches what the player configured on web. Cell/connector sizing
// is width-aware (`building_grid_sizing.dart`) so the grid — including the
// 8-directional link arrows between cells — scales to fit the left column
// of the responsive two-column Building Detail layout on wide screens,
// matching web's "grid fit to the page" behavior, and only falls back to a
// fixed size + horizontal scroll below a legible minimum cell size.
//
// Tapping an occupied cell calls [onUnitTap] — the screen decides whether
// that opens a bottom sheet (narrow screens) or updates the inline
// right-column selection (wide screens), mirroring `selectedCell` driving
// `BuildingReadonlySidebar` on web. Placing a unit on an empty cell remains
// edit-mode-only (`BuildingGridEditor`); empty cells here stay
// non-interactive.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_detail_models.dart';
import 'building_grid_models.dart';
import 'building_grid_sizing.dart';
import 'building_link_connector_widgets.dart';
import 'building_link_helpers.dart';

/// Mirrors `unitColors` in `useBuildingDetail.ts` — a distinct hue per unit
/// type used to tint grid cells/legend entries.
const Map<String, Color> unitTypeColors = {
  'MINING': Color(0xFFFF6D00),
  'STORAGE': Color(0xFF8B949E),
  'B2B_SALES': Color(0xFF00C853),
  'PURCHASE': Color(0xFF0047FF),
  'MANUFACTURING': Color(0xFFFF6D00),
  'BRANDING': Color(0xFF9333EA),
  'MARKETING': Color(0xFFEC4899),
  'PUBLIC_SALES': Color(0xFF00C853),
  'PRODUCT_QUALITY': Color(0xFF0047FF),
  'BRAND_QUALITY': Color(0xFF9333EA),
  'POWER_GENERATION': Color(0xFFF59E0B),
  'BATTERY_STORAGE': Color(0xFFA855F7),
  'FUEL_PURCHASE': Color(0xFF2563EB),
  'WIND_TURBINE': Color(0xFF14B8A6),
  'WATER_TURBINE': Color(0xFF0EA5E9),
  'ENERGY_STORAGE': Color(0xFF64748B),
  'ENERGY_PRODUCING': Color(0xFFEF4444),
};

String unitTypeShortLabel(String unitType) => unitType.replaceAll('_', ' ');

class BuildingUnitGrid extends StatelessWidget {
  const BuildingUnitGrid({
    super.key,
    required this.units,
    required this.itemNameFor,
    required this.actionLoadingIds,
    required this.onUnitTap,
  });

  final List<BuildingUnitDetail> units;
  final String Function(BuildingUnitDetail unit) itemNameFor;
  final Set<String> actionLoadingIds;
  final void Function(BuildingUnitDetail unit) onUnitTap;

  BuildingUnitDetail? _unitAt(int x, int y) {
    for (final unit in units) {
      if (unit.gridX == x && unit.gridY == y) return unit;
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final linkUnits = units.map(EditableGridUnit.fromActive).toList();
    return LayoutBuilder(
      builder: (context, constraints) {
        final sizing = computeGridSizing(constraints.maxWidth);
        final grid = SizedBox(
          width: sizing.totalWidth,
          child: Column(
            children: [
              for (var y = 0; y < 4; y++) ...[
                _unitRow(context, y, sizing, linkUnits),
                if (y < 3) _connectorRow(linkUnits, y, sizing),
              ],
            ],
          ),
        );
        if (!sizing.scrollable) return grid;
        return SingleChildScrollView(scrollDirection: Axis.horizontal, child: grid);
      },
    );
  }

  Widget _unitRow(BuildContext context, int y, GridSizing sizing, List<EditableGridUnit> linkUnits) {
    return Row(
      children: [
        for (var x = 0; x < 4; x++) ...[
          SizedBox(
            width: sizing.cellSize,
            height: sizing.cellSize,
            child: Builder(
              builder: (context) {
                final unit = _unitAt(x, y);
                return _GridCell(
                  key: ValueKey('cell-$x-$y'),
                  unit: unit,
                  itemNameFor: itemNameFor,
                  isLoading: unit != null && actionLoadingIds.contains(unit.id),
                  onTap: unit == null ? null : () => onUnitTap(unit),
                );
              },
            ),
          ),
          if (x < 3)
            LinkConnectorButton(
              orientation: LinkOrientation.horizontal,
              state: getHorizontalLinkState(linkUnits, x, y),
              canToggle: false,
              dimWhenDisabled: false,
              size: sizing.connectorSize,
              thickness: sizing.cellSize,
              onTap: () {},
            ),
        ],
      ],
    );
  }

  Widget _connectorRow(List<EditableGridUnit> linkUnits, int y, GridSizing sizing) {
    return Row(
      children: [
        for (var x = 0; x < 4; x++) ...[
          LinkConnectorButton(
            orientation: LinkOrientation.vertical,
            state: getVerticalLinkState(linkUnits, x, y),
            canToggle: false,
            dimWhenDisabled: false,
            size: sizing.connectorSize,
            thickness: sizing.cellSize,
            onTap: () {},
          ),
          if (x < 3)
            DiagonalConnectorWidget(
              primaryState: getPrimaryDiagonalLinkState(linkUnits, x, y),
              secondaryState: getSecondaryDiagonalLinkState(linkUnits, x, y),
              canTogglePrimary: false,
              canToggleSecondary: false,
              dimWhenDisabled: false,
              size: sizing.connectorSize,
              onTogglePrimary: () {},
              onToggleSecondary: () {},
            ),
        ],
      ],
    );
  }
}

class _GridCell extends StatelessWidget {
  const _GridCell({super.key, required this.unit, required this.itemNameFor, required this.isLoading, required this.onTap});

  final BuildingUnitDetail? unit;
  final String Function(BuildingUnitDetail unit) itemNameFor;
  final bool isLoading;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final unit = this.unit;

    if (unit == null) {
      return DecoratedBox(
        decoration: BoxDecoration(
          border: Border.all(color: theme.colorScheme.outlineVariant),
          borderRadius: BorderRadius.circular(AppRadius.md),
        ),
        child: Center(child: Icon(Icons.add, color: theme.colorScheme.outlineVariant, size: 18)),
      );
    }

    final itemName = itemNameFor(unit);
    final accent = unitTypeColors[unit.unitType] ?? theme.colorScheme.primary;

    return Material(
      color: theme.colorScheme.surfaceContainer,
      borderRadius: BorderRadius.circular(AppRadius.md),
      child: InkWell(
        key: ValueKey('cell-unit-${unit.id}'),
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.all(AppSpacing.xs),
          decoration: BoxDecoration(
            border: Border.all(color: accent.withValues(alpha: 0.7), width: 2),
            borderRadius: BorderRadius.circular(AppRadius.md),
          ),
          // `FittedBox` rather than a fixed minimum cell size: cells now
          // scale down to fit the available column width
          // (`building_grid_sizing.dart`), and combined with a large
          // accessibility text scale a small-but-legible cell can still be
          // too small for two lines of scaled text — scaling the whole
          // content block down (rather than throwing a RenderFlex overflow)
          // keeps every cell size/text-scale combination safe.
          child: FittedBox(
            fit: BoxFit.scaleDown,
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              mainAxisSize: MainAxisSize.min,
              children: [
                if (isLoading)
                  const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2))
                else
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
                  Text(
                    itemName,
                    style: theme.textTheme.labelSmall,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    textAlign: TextAlign.center,
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
