// Per-city power-grid balance chip for the dashboard Buildings tab,
// mirroring the `.power-balance` chip in `DashboardMainContent.vue`. Reuses
// the existing `CityPowerBalance` model (`building_panel_models.dart`,
// already used by `BuildingPowerPlantPanel`) rather than inventing a new type.

import 'package:flutter/material.dart';

import '../buildings/building_panel_models.dart';

class DashboardPowerBalanceChip extends StatelessWidget {
  const DashboardPowerBalanceChip({super.key, required this.cityId, required this.balance});

  final String cityId;

  /// `null` while still loading — renders nothing (no flash of an
  /// unknown/legacy state before the real value arrives).
  final CityPowerBalance? balance;

  @override
  Widget build(BuildContext context) {
    final value = balance;
    if (value == null) return const SizedBox.shrink();

    final color = switch (value.status) {
      'CRITICAL' => Colors.red,
      'CONSTRAINED' => Colors.amber,
      _ => Colors.green,
    };
    final label = value.status == 'BALANCED'
        ? '${value.totalSupplyMw.toStringAsFixed(0)} / ${value.totalDemandMw.toStringAsFixed(0)} MW'
        : '${value.status} power grid';

    return Container(
      key: ValueKey('power-balance-$cityId'),
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: color),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.bolt, size: 14, color: color),
          const SizedBox(width: 6),
          Text('⚡ Power: $label', style: TextStyle(color: color, fontSize: 12, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}
