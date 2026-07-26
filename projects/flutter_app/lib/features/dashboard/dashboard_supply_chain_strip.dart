// Dashboard Buildings-tab supply-chain strip, mirroring
// `SupplyChainPanel.vue`: a horizontal left-to-right chain of unit icons
// (sorted by grid position) with a client-derived health badge. Distinct
// from the FACTORY-only 4x4 grid diagram (`building_supply_chain_diagram.dart`)
// used on the building-detail screen — this is a simpler, building-type-agnostic
// strip fed by `buildingUnitOperationalStatuses`, not `buildingSupplyChain`.

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';

import '../../core/theme/app_icons.dart';
import '../buildings/building_analytics_models.dart';
import '../buildings/building_unit_grid.dart' show unitTypeColors, unitTypeShortLabel;
import 'dashboard_models.dart';

const Map<String, FaIconData> _unitTypeIcons = {
  'PURCHASE': AppIcons.unitPurchase,
  'MINING': AppIcons.unitMining,
  'MANUFACTURING': AppIcons.unitManufacturing,
  'STORAGE': AppIcons.unitStorage,
  'B2B_SALES': AppIcons.unitB2bSales,
  'PUBLIC_SALES': AppIcons.unitPublicSales,
  'BRANDING': AppIcons.unitBranding,
  'MARKETING': AppIcons.unitMarketing,
  'PRODUCT_QUALITY': AppIcons.unitProductQuality,
  'BRAND_QUALITY': AppIcons.unitBrandQuality,
};

/// `RED` if any unit has been idle over 20 ticks, `YELLOW` if over 5, else
/// `GREEN`. `null` when no statuses have been fetched yet (no badge shown).
/// Mirrors `SupplyChainPanel.vue`'s `healthScore` computed.
String? supplyChainHealth(List<BuildingUnitOperationalStatus> statuses) {
  if (statuses.isEmpty) return null;
  if (statuses.any((s) => s.idleTicks > 20)) return 'RED';
  if (statuses.any((s) => s.idleTicks > 5)) return 'YELLOW';
  return 'GREEN';
}

class DashboardSupplyChainStrip extends StatelessWidget {
  const DashboardSupplyChainStrip({super.key, required this.units, required this.statuses});

  final List<DashboardUnit> units;
  final List<BuildingUnitOperationalStatus> statuses;

  @override
  Widget build(BuildContext context) {
    if (units.isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    final sorted = [...units]..sort((a, b) {
      final byX = a.gridX.compareTo(b.gridX);
      return byX != 0 ? byX : a.gridY.compareTo(b.gridY);
    });
    final statusByUnitId = {for (final s in statuses) s.buildingUnitId: s};
    final health = supplyChainHealth(statuses);

    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text('Supply chain', style: theme.textTheme.labelMedium),
              if (health != null) ...[const SizedBox(width: 8), _HealthBadge(health: health)],
            ],
          ),
          const SizedBox(height: 6),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                for (var i = 0; i < sorted.length; i++) ...[
                  _UnitNode(key: ValueKey('supply-chain-unit-${sorted[i].id}'), unit: sorted[i], status: statusByUnitId[sorted[i].id]),
                  if (i < sorted.length - 1)
                    const Padding(padding: EdgeInsets.symmetric(horizontal: 4), child: FaIcon(AppIcons.arrowRight, size: 12)),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _HealthBadge extends StatelessWidget {
  const _HealthBadge({required this.health});

  final String health;

  @override
  Widget build(BuildContext context) {
    final color = switch (health) {
      'RED' => Colors.red,
      'YELLOW' => Colors.amber,
      _ => Colors.green,
    };
    return Container(
      key: const Key('supply-chain-health-badge'),
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(4),
        border: Border.all(color: color),
      ),
      child: Text(health, style: TextStyle(color: color, fontSize: 10, fontWeight: FontWeight.bold)),
    );
  }
}

class _UnitNode extends StatelessWidget {
  const _UnitNode({super.key, required this.unit, this.status});

  final DashboardUnit unit;
  final BuildingUnitOperationalStatus? status;

  @override
  Widget build(BuildContext context) {
    final color = unitTypeColors[unit.unitType] ?? Theme.of(context).colorScheme.primary;
    final icon = _unitTypeIcons[unit.unitType] ?? AppIcons.unitManufacturing;
    final isIdleOrBlocked = status != null && status!.status != 'ACTIVE';

    return Tooltip(
      message: status?.blockedReason ?? status?.status ?? unitTypeShortLabel(unit.unitType),
      child: Container(
        width: 52,
        padding: const EdgeInsets.all(6),
        decoration: BoxDecoration(
          border: Border.all(color: color.withValues(alpha: isIdleOrBlocked ? 1 : 0.5), width: isIdleOrBlocked ? 2 : 1),
          borderRadius: BorderRadius.circular(8),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            FaIcon(icon, size: 16, color: color),
            const SizedBox(height: 2),
            Text(
              unitTypeShortLabel(unit.unitType),
              style: const TextStyle(fontSize: 8),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              textAlign: TextAlign.center,
            ),
            if (isIdleOrBlocked)
              Text(status!.status, style: TextStyle(fontSize: 7, color: color, fontWeight: FontWeight.bold)),
          ],
        ),
      ),
    );
  }
}
