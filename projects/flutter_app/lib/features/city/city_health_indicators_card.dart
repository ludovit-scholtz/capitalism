// City economic-health indicators card, ported from
// `projects/frontend/src/components/cityMap/HealthIndicatorsPanel.vue` —
// factored out of `city_economy_screen.dart` to keep that file under the
// 500-line budget. The score ring uses a `CircularProgressIndicator`
// instead of the web's hand-drawn SVG ring (same information).

import 'package:flutter/material.dart';

import 'city_economy_models.dart';

class CityHealthIndicatorsCard extends StatelessWidget {
  const CityHealthIndicatorsCard({super.key, required this.report});

  final CityEconomicReportResult? report;

  Color _indexColor(double index) {
    if (index >= 70) return Colors.green;
    if (index >= 40) return Colors.amber;
    return Colors.red;
  }

  String _indexLabel(double index) {
    if (index >= 70) return 'Thriving';
    if (index >= 40) return 'Neutral';
    return 'Declining';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final latest = report?.latest;

    if (latest == null) {
      return const Card(child: Padding(padding: EdgeInsets.all(16), child: Text('No economic health data yet.')));
    }

    final color = _indexColor(latest.economicIndex);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                SizedBox(
                  width: 56,
                  height: 56,
                  child: Stack(
                    alignment: Alignment.center,
                    children: [
                      CircularProgressIndicator(
                        value: (latest.economicIndex / 100).clamp(0.0, 1.0),
                        strokeWidth: 6,
                        color: color,
                        backgroundColor: theme.colorScheme.surfaceContainerHighest,
                      ),
                      Text(latest.economicIndex.toStringAsFixed(0), style: theme.textTheme.titleMedium),
                    ],
                  ),
                ),
                const SizedBox(width: 16),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Economic index', style: theme.textTheme.labelMedium),
                    Text(_indexLabel(latest.economicIndex), style: TextStyle(color: color, fontWeight: FontWeight.bold)),
                  ],
                ),
              ],
            ),
            const SizedBox(height: 16),
            GridView.count(
              crossAxisCount: 2,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              mainAxisSpacing: 8,
              crossAxisSpacing: 8,
              childAspectRatio: 2.4,
              children: [
                _MetricTile(label: 'Salaries', value: latest.totalSalaries.toStringAsFixed(0)),
                _MetricTile(label: 'Revenue', value: latest.totalPublicRevenue.toStringAsFixed(0)),
                _MetricTile(label: 'Companies', value: '${latest.activeCompanies}'),
                _MetricTile(label: 'Quality', value: '${(latest.averageProductQuality * 100).toStringAsFixed(0)}%'),
              ],
            ),
            if (report!.history.length > 1) ...[
              const SizedBox(height: 12),
              TextButton(
                onPressed: () => _showDetails(context, report!),
                child: const Text('View details'),
              ),
            ],
          ],
        ),
      ),
    );
  }

  void _showDetails(BuildContext context, CityEconomicReportResult report) {
    final latest = report.latest!;
    showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('City economic health'),
        content: SizedBox(
          width: double.maxFinite,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Economic index: ${latest.economicIndex.toStringAsFixed(1)} — ${_indexLabel(latest.economicIndex)}'),
                Text('Salaries: ${latest.totalSalaries.toStringAsFixed(0)}'),
                Text('Revenue: ${latest.totalPublicRevenue.toStringAsFixed(0)}'),
                Text('Companies: ${latest.activeCompanies}'),
                Text('Power: ${latest.totalPowerSupply.toStringAsFixed(1)} / ${latest.totalPowerConsumption.toStringAsFixed(1)} MW'),
                Text('Quality: ${(latest.averageProductQuality * 100).toStringAsFixed(1)}%'),
                Text('Cycle ending tick: ${latest.taxCycleEnd}'),
                const Divider(height: 24),
                Text('History', style: Theme.of(dialogContext).textTheme.titleSmall),
                for (final entry in report.history)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 2),
                    child: Row(
                      children: [
                        Expanded(child: Text('Tick ${entry.taxCycleEnd}')),
                        Text(entry.economicIndex.toStringAsFixed(1), style: TextStyle(color: _indexColor(entry.economicIndex), fontWeight: FontWeight.bold)),
                      ],
                    ),
                  ),
              ],
            ),
          ),
        ),
        actions: [TextButton(onPressed: () => Navigator.of(dialogContext).pop(), child: const Text('Close'))],
      ),
    );
  }
}

class _MetricTile extends StatelessWidget {
  const _MetricTile({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        border: Border.all(color: theme.colorScheme.outlineVariant),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(label, style: theme.textTheme.labelSmall),
          Text(value, style: theme.textTheme.titleSmall),
        ],
      ),
    );
  }
}
