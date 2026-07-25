// Per-unit resource-flow history (ROADMAP 137), embedded in
// `UnitConfigSheet` for any persisted unit tracking a resource/product, and
// per-unit product analytics for MANUFACTURING units. Mirrors
// `UnitResourceHistoryPanel.vue`'s inflow/outflow/consumed/produced chart
// and the "Product Performance" card in `BuildingReadonlySidebar.vue`'s
// Market Intelligence tab — hand-rolled bar history instead of an SVG line
// chart, matching this app's existing "no charting dependency" convention.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_analytics_models.dart';
import 'building_bar_history.dart';

class UnitResourceHistoryPanel extends StatelessWidget {
  const UnitResourceHistoryPanel({super.key, required this.history});

  final List<UnitResourceHistoryPoint> history;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    if (history.isEmpty) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SizedBox(height: AppSpacing.md),
          Text('History', style: theme.textTheme.titleSmall),
          const SizedBox(height: AppSpacing.xs),
          Text('No tracked history yet.', style: theme.textTheme.bodySmall),
        ],
      );
    }

    final sorted = [...history]..sort((a, b) => a.tick.compareTo(b.tick));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: AppSpacing.md),
        Text('History · T${sorted.first.tick}–T${sorted.last.tick}', style: theme.textTheme.titleSmall),
        const SizedBox(height: AppSpacing.xs),
        Text('Inflow', style: theme.textTheme.labelSmall),
        BarHistoryRow(values: sorted.map((p) => p.inflowQuantity).toList(), color: const Color(0xFF22C55E), height: 28),
        const SizedBox(height: 4),
        Text('Outflow', style: theme.textTheme.labelSmall),
        BarHistoryRow(values: sorted.map((p) => p.outflowQuantity).toList(), color: const Color(0xFFEF4444), height: 28),
        const SizedBox(height: 4),
        Text('Consumed', style: theme.textTheme.labelSmall),
        BarHistoryRow(values: sorted.map((p) => p.consumedQuantity).toList(), color: const Color(0xFFF59E0B), height: 28),
        const SizedBox(height: 4),
        Text('Produced', style: theme.textTheme.labelSmall),
        BarHistoryRow(values: sorted.map((p) => p.producedQuantity).toList(), color: const Color(0xFF0047FF), height: 28),
      ],
    );
  }
}

class UnitProductAnalyticsPanel extends StatelessWidget {
  const UnitProductAnalyticsPanel({super.key, required this.analytics, required this.loading});

  final UnitProductAnalytics? analytics;
  final bool loading;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final data = analytics;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: AppSpacing.md),
        Text('Product Performance', style: theme.textTheme.titleSmall),
        const SizedBox(height: AppSpacing.xs),
        if (loading)
          const Padding(padding: EdgeInsets.symmetric(vertical: AppSpacing.sm), child: LinearProgressIndicator())
        else if (data == null)
          Text('No production data yet.', style: theme.textTheme.bodySmall)
        else ...[
          Wrap(
            spacing: AppSpacing.md,
            runSpacing: AppSpacing.xs,
            children: [
              _Metric(label: 'Produced', value: data.totalQuantityProduced.toStringAsFixed(0)),
              _Metric(label: 'Cost', value: data.totalCost.toStringAsFixed(0)),
              _Metric(label: 'Est. revenue', value: data.estimatedRevenue.toStringAsFixed(0)),
              _Metric(label: 'Est. profit', value: data.estimatedProfit.toStringAsFixed(0)),
            ],
          ),
          if (data.snapshots.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.sm),
            Text('Cost history', style: theme.textTheme.labelSmall),
            BarHistoryRow(values: data.snapshots.map((s) => s.totalCost).toList(), color: const Color(0xFFDC2626), height: 28),
          ],
        ],
      ],
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [Text(label, style: theme.textTheme.labelSmall), Text(value, style: theme.textTheme.bodyMedium)],
    );
  }
}
