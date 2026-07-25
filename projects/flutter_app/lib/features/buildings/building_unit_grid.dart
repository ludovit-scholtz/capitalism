// Read-only port of the web's 4x4 grid layout
// (`gridIndexes = [0,1,2,3]` in `useBuildingDetail.ts`, rendered by
// `BuildingUnitGrid.vue`'s "Active Configuration" grid). Renders units at
// their `gridX`/`gridY` (each 0..3) instead of a flat list, so the physical
// layout matches what the player configured on web.
//
// Explicitly NOT ported here (tracked as their own separate ROADMAP items,
// since they are large, independent features in their own right):
// - Placing a unit on an empty cell (ROADMAP: "Implement placing a new unit
//   on an empty grid cell") — empty cells render but are non-interactive.
// - The 8-directional link system and its connector/arrow visuals
//   (ROADMAP: "Implement the 8-directional unit link system", "Port
//   link-state/flow arrow visuals").
// - The draft/planned grid, `storeBuildingConfiguration`/
//   `cancelBuildingConfiguration`, and per-unit Config/Energy/Performance/
//   Maintenance editing tabs (ROADMAP items covering the planning grid and
//   per-unit configuration editing).
// - Inventory fill bars / inflow-outflow visualization (ROADMAP: "Add unit
//   inventory/capacity visualization per cell").
//
// This screen keeps the two quick actions the flat list already had
// (`scheduleUnitUpgrade`, `updatePublicSalesPrice`) but moves them behind a
// tap-to-open bottom sheet per cell, since a 4x4 grid on a phone screen has
// no room for inline icon buttons the way the desktop web layout does.

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';

import '../../core/theme/app_icons.dart';
import '../../core/theme/app_spacing.dart';
import 'building_detail_models.dart';

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
    required this.onUpgrade,
    required this.onUpdatePrice,
  });

  final List<BuildingUnitDetail> units;
  final String Function(BuildingUnitDetail unit) itemNameFor;
  final Set<String> actionLoadingIds;
  final void Function(BuildingUnitDetail unit) onUpgrade;
  final void Function(BuildingUnitDetail unit) onUpdatePrice;

  BuildingUnitDetail? _unitAt(int x, int y) {
    for (final unit in units) {
      if (unit.gridX == x && unit.gridY == y) return unit;
    }
    return null;
  }

  void _openCellSheet(BuildContext context, BuildingUnitDetail unit) {
    final itemName = itemNameFor(unit);
    showModalBottomSheet<void>(
      context: context,
      builder: (sheetContext) {
        final theme = Theme.of(sheetContext);
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('${unit.unitType} · Level ${unit.level}', style: theme.textTheme.titleMedium),
                if (itemName.isNotEmpty) ...[const SizedBox(height: 4), Text(itemName, style: theme.textTheme.bodyMedium)],
                const SizedBox(height: AppSpacing.md),
                Row(
                  children: [
                    if (unit.unitType == 'PUBLIC_SALES')
                      Expanded(
                        child: OutlinedButton.icon(
                          icon: const FaIcon(AppIcons.sell, size: 16),
                          label: const Text('Update price'),
                          onPressed: () {
                            Navigator.of(sheetContext).pop();
                            onUpdatePrice(unit);
                          },
                        ),
                      ),
                    if (unit.unitType == 'PUBLIC_SALES') const SizedBox(width: AppSpacing.sm),
                    Expanded(
                      child: FilledButton.icon(
                        icon: const FaIcon(AppIcons.upgrade, size: 16),
                        label: const Text('Upgrade'),
                        onPressed: () {
                          Navigator.of(sheetContext).pop();
                          onUpgrade(unit);
                        },
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  // Fixed pixel cell size rather than one derived from screen width: with a
  // flexible/`Expanded` cell, narrow phones (or a large accessibility text
  // scale) shrink cells below what the monogram + two text lines need,
  // producing a RenderFlex overflow. The web has the same problem on narrow
  // viewports and solves it the same way — `BuildingUnitGrid.layout.css`
  // gives the grid a `min-width: 330px` and lets it scroll horizontally
  // below the 640px breakpoint instead of shrinking cells. Mirrored here: a
  // fixed total grid width wrapped in a horizontal `SingleChildScrollView`,
  // which only actually scrolls when the viewport is narrower than that.
  static const double _cellSize = 88;

  @override
  Widget build(BuildContext context) {
    const totalWidth = _cellSize * 4 + AppSpacing.sm * 3;
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: SizedBox(
        width: totalWidth,
        child: Column(
          children: [
            for (var y = 0; y < 4; y++)
              Padding(
                padding: EdgeInsets.only(bottom: y < 3 ? AppSpacing.sm : 0),
                child: Row(
                  children: [
                    for (var x = 0; x < 4; x++)
                      Padding(
                        padding: EdgeInsets.only(right: x < 3 ? AppSpacing.sm : 0),
                        child: Builder(
                          builder: (context) {
                            final unit = _unitAt(x, y);
                            return SizedBox(
                              width: _cellSize,
                              height: _cellSize,
                              child: _GridCell(
                                key: ValueKey('cell-$x-$y'),
                                unit: unit,
                                itemNameFor: itemNameFor,
                                isLoading: unit != null && actionLoadingIds.contains(unit.id),
                                onTap: unit == null ? null : () => _openCellSheet(context, unit),
                              ),
                            );
                          },
                        ),
                      ),
                  ],
                ),
              ),
          ],
        ),
      ),
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
    );
  }
}
