// Building-level financial timeline (ROADMAP 137), mirroring the P&L
// summary + chart from `BuildingOverviewTab.vue`/
// `BuildingFinancialTimelineChart.vue`. Hand-rolled bar history instead of
// the web's SVG line chart, matching this app's "no charting dependency"
// convention.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_analytics_models.dart';
import 'building_bar_history.dart';

class BuildingFinancialTimelinePanel extends StatelessWidget {
  const BuildingFinancialTimelinePanel({super.key, required this.timeline});

  final BuildingFinancialTimeline? timeline;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final data = timeline;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Financial Timeline', style: theme.textTheme.titleSmall),
            const SizedBox(height: AppSpacing.sm),
            if (data == null || data.timeline.isEmpty)
              Text('No financial data yet.', style: theme.textTheme.bodySmall)
            else ...[
              Wrap(
                spacing: AppSpacing.lg,
                runSpacing: AppSpacing.xs,
                children: [
                  _Stat(label: 'Sales', value: data.totalSales, color: const Color(0xFF2563EB)),
                  _Stat(label: 'Costs', value: data.totalCosts, color: const Color(0xFFDC2626)),
                  _Stat(label: 'Profit', value: data.totalProfit, color: const Color(0xFF059669)),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),
              Text('Profit trend · T${data.dataFromTick}–T${data.dataToTick}', style: theme.textTheme.bodySmall),
              const SizedBox(height: 4),
              BarHistoryRow(values: data.timeline.map((t) => t.profit).toList(), color: const Color(0xFF059669), allowNegative: true),
            ],
          ],
        ),
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.label, required this.value, required this.color});

  final String label;
  final double value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: theme.textTheme.labelSmall),
        Text(value.toStringAsFixed(0), style: theme.textTheme.titleMedium?.copyWith(color: color)),
      ],
    );
  }
}
