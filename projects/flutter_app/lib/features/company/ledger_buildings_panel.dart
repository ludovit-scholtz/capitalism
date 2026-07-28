// Per-building revenue/costs/profit table for the Ledger screen, ported
// from the "buildings-card" section of `LedgerMainContent.vue`.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import 'company_models.dart';

class LedgerBuildingsPanel extends StatelessWidget {
  const LedgerBuildingsPanel({super.key, required this.buildings});

  final List<BuildingLedgerSummary> buildings;

  @override
  Widget build(BuildContext context) {
    if (buildings.isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('🏭 BUILDINGS PERFORMANCE', style: theme.textTheme.labelLarge?.copyWith(fontWeight: FontWeight.bold, letterSpacing: 0.5)),
            const SizedBox(height: 8),
            for (var i = 0; i < buildings.length; i++) ...[
              _BuildingRow(building: buildings[i], languageCode: languageCode),
              if (i < buildings.length - 1) const Divider(height: 12),
            ],
          ],
        ),
      ),
    );
  }
}

class _BuildingRow extends StatelessWidget {
  const _BuildingRow({required this.building, required this.languageCode});

  final BuildingLedgerSummary building;
  final String languageCode;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final profit = building.revenue - building.costs;

    return Row(
      children: [
        Expanded(
          flex: 2,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(building.buildingName, style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600)),
              Text(building.buildingType, style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
            ],
          ),
        ),
        Expanded(
          child: Text(
            AppNumberFormat.money(profit, currencyCode: building.currencyCode, languageCode: languageCode),
            textAlign: TextAlign.right,
            style: theme.textTheme.bodyMedium?.copyWith(color: profit >= 0 ? Colors.green.shade600 : Colors.red.shade600, fontWeight: FontWeight.w600),
          ),
        ),
        IconButton(
          icon: const Icon(Icons.chevron_right, size: 18),
          onPressed: () => context.go(building.buildingType == 'BANK' ? '/bank/${building.buildingId}' : '/building/${building.buildingId}'),
        ),
      ],
    );
  }
}
