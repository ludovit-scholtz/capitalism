// FACTORY-only supply-chain diagram (ROADMAP 137), mirroring
// `SupplyChainDiagram.vue`'s health banner + positioned unit grid. A
// simplified visual rather than an SVG port (documented trim, matching this
// app's existing precedent of trimming secondary chart/diagram detail —
// see item 132's skipped P&L bar chart): units are positioned at their
// real `gridX`/`gridY` using the same fixed-cell-size grid convention as
// `building_unit_grid.dart`, color-coded by status/fill, but links are
// listed as flat rows below the grid instead of drawn as arrows on top of
// it — same underlying data, no custom line-routing/arrow-head painter.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_analytics_models.dart';
import 'building_unit_grid.dart' show unitTypeColors, unitTypeShortLabel;

const Map<String, Color> _statusColors = {
  'ACTIVE': Color(0xFF22C55E),
  'BLOCKED': Color(0xFFEF4444),
  'FULL': Color(0xFF0047FF),
  'IDLE': Color(0xFFF59E0B),
  'UNCONFIGURED': Color(0xFF8B949E),
};

const Map<String, Color> _healthColors = {
  'GREEN': Color(0xFF22C55E),
  'YELLOW': Color(0xFFF59E0B),
  'RED': Color(0xFFEF4444),
};

class BuildingSupplyChainDiagramView extends StatelessWidget {
  const BuildingSupplyChainDiagramView({super.key, required this.diagram});

  final BuildingSupplyChainDiagram? diagram;

  static const double _cellSize = 72;

  SupplyChainUnitSummary? _unitAt(List<SupplyChainUnitSummary> units, int x, int y) {
    for (final unit in units) {
      if (unit.gridX == x && unit.gridY == y) return unit;
    }
    return null;
  }

  String _unitLabel(List<SupplyChainUnitSummary> units, String unitId) {
    for (final unit in units) {
      if (unit.buildingUnitId == unitId) return unitTypeShortLabel(unit.unitType);
    }
    return 'Unknown';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final data = diagram;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Supply Chain Health', style: theme.textTheme.titleSmall),
            const SizedBox(height: AppSpacing.sm),
            if (data == null)
              Text('No supply chain data yet.', style: theme.textTheme.bodySmall)
            else ...[
              Container(
                padding: const EdgeInsets.symmetric(horizontal: AppSpacing.sm, vertical: AppSpacing.xs),
                decoration: BoxDecoration(
                  color: (_healthColors[data.healthScore] ?? theme.colorScheme.outline).withValues(alpha: 0.14),
                  borderRadius: BorderRadius.circular(AppRadius.sm),
                  border: Border.all(color: (_healthColors[data.healthScore] ?? theme.colorScheme.outline).withValues(alpha: 0.5)),
                ),
                child: Text(
                  data.healthReason ?? data.healthScore,
                  style: theme.textTheme.bodySmall?.copyWith(color: _healthColors[data.healthScore]),
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: SizedBox(
                  width: _cellSize * 4 + AppSpacing.xs * 3,
                  child: Column(
                    children: [
                      for (var y = 0; y < 4; y++)
                        Padding(
                          padding: EdgeInsets.only(bottom: y < 3 ? AppSpacing.xs : 0),
                          child: Row(
                            children: [
                              for (var x = 0; x < 4; x++)
                                Padding(
                                  padding: EdgeInsets.only(right: x < 3 ? AppSpacing.xs : 0),
                                  child: SizedBox(
                                    width: _cellSize,
                                    height: _cellSize,
                                    child: _SupplyChainCell(unit: _unitAt(data.units, x, y)),
                                  ),
                                ),
                            ],
                          ),
                        ),
                    ],
                  ),
                ),
              ),
              if (data.links.isNotEmpty) ...[
                const SizedBox(height: AppSpacing.sm),
                Text('Links', style: theme.textTheme.labelMedium),
                for (final link in data.links)
                  Padding(
                    key: ValueKey('link-${link.fromUnitId}-${link.toUnitId}'),
                    padding: const EdgeInsets.symmetric(vertical: 2),
                    child: Text(
                      '${_unitLabel(data.units, link.fromUnitId)} → ${_unitLabel(data.units, link.toUnitId)}'
                      ' · transit ${link.estimatedTransitCost.toStringAsFixed(0)}',
                      style: theme.textTheme.bodySmall,
                    ),
                  ),
              ],
            ],
          ],
        ),
      ),
    );
  }
}

class _SupplyChainCell extends StatelessWidget {
  const _SupplyChainCell({required this.unit});

  final SupplyChainUnitSummary? unit;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final unit = this.unit;
    if (unit == null) {
      return DecoratedBox(
        decoration: BoxDecoration(
          border: Border.all(color: theme.colorScheme.outlineVariant),
          borderRadius: BorderRadius.circular(AppRadius.sm),
        ),
      );
    }

    final statusColor = _statusColors[unit.status] ?? theme.colorScheme.outline;
    final accent = unitTypeColors[unit.unitType] ?? theme.colorScheme.primary;

    return Container(
      key: ValueKey('supply-chain-cell-${unit.buildingUnitId}'),
      padding: const EdgeInsets.all(AppSpacing.xs),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainer,
        border: Border.all(color: statusColor, width: 2),
        borderRadius: BorderRadius.circular(AppRadius.sm),
      ),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              CircleAvatar(radius: 8, backgroundColor: accent.withValues(alpha: 0.2), child: Icon(Icons.circle, size: 6, color: accent)),
              if (unit.idleTicks > 5) const Padding(padding: EdgeInsets.only(left: 2), child: Text('⚠', style: TextStyle(fontSize: 10))),
            ],
          ),
          const SizedBox(height: 2),
          Text(unitTypeShortLabel(unit.unitType), style: theme.textTheme.labelSmall, maxLines: 1, overflow: TextOverflow.ellipsis, textAlign: TextAlign.center),
          Text('${(unit.fillPercent * 100).toStringAsFixed(0)}%', style: theme.textTheme.labelSmall),
        ],
      ),
    );
  }
}
