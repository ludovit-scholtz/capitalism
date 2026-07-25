// Fill bar + quantity/capacity/sourcing-cost/average-quality display for a
// unit's inventory (ROADMAP 127). Renders `getFlowSegments`'s fill/inflow/
// outflow segments as stacked colored bars.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_grid_models.dart';
import 'building_inventory_helpers.dart';

const Map<FillBucket, Color> _bucketColors = {
  FillBucket.empty: Color(0xFF64748B),
  FillBucket.low: Color(0xFF22C55E),
  FillBucket.medium: Color(0xFFF59E0B),
  FillBucket.high: Color(0xFFEF4444),
};

class InventoryFillBar extends StatelessWidget {
  const InventoryFillBar({super.key, required this.summary, this.isPublicSales = false});

  final BuildingUnitInventorySummary summary;
  final bool isPublicSales;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final bucket = getFillBucket(summary.fillPercent);
    final fillColor = _bucketColors[bucket]!;
    final segments = getFlowSegments(summary.fillPercent, summary.capacity, summary.lastTickInflow, summary.lastTickOutflow);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(AppRadius.sm),
          child: SizedBox(
            height: 10,
            child: Stack(
              children: [
                Container(color: theme.colorScheme.surfaceContainerHigh),
                FractionallySizedBox(
                  widthFactor: (segments.fillWidth / 100).clamp(0, 1),
                  child: Container(color: fillColor),
                ),
                if (segments.inflowWidth > 0)
                  Align(
                    alignment: Alignment.centerLeft,
                    child: FractionallySizedBox(
                      widthFactor: ((segments.inflowLeft + segments.inflowWidth) / 100).clamp(0, 1),
                      child: FractionallySizedBox(
                        alignment: Alignment.centerRight,
                        widthFactor: (segments.inflowWidth / (segments.inflowLeft + segments.inflowWidth)).clamp(0, 1),
                        child: const ColoredBox(color: Color(0xFF22C55E)),
                      ),
                    ),
                  ),
                if (segments.outflowWidth > 0)
                  Align(
                    alignment: Alignment.centerLeft,
                    child: FractionallySizedBox(
                      widthFactor: ((segments.outflowLeft + segments.outflowWidth) / 100).clamp(0, 1),
                      child: FractionallySizedBox(
                        alignment: Alignment.centerRight,
                        widthFactor: (segments.outflowWidth / (segments.outflowLeft + segments.outflowWidth)).clamp(0, 1),
                        child: ColoredBox(color: isPublicSales ? const Color(0xFF3B82F6) : const Color(0xFFF59E0B)),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
        const SizedBox(height: AppSpacing.xs),
        Text(
          '${summary.quantity.toStringAsFixed(0)}/${summary.capacity.toStringAsFixed(0)} '
          '(${(summary.fillPercent * 100).toStringAsFixed(0)}%)'
          '${(summary.lastTickInflow ?? 0) > 0 ? ' ↑${summary.lastTickInflow!.toStringAsFixed(0)}' : ''}'
          '${(summary.lastTickOutflow ?? 0) > 0 ? ' ↓${summary.lastTickOutflow!.toStringAsFixed(0)}' : ''}',
          style: theme.textTheme.bodySmall,
        ),
        if (summary.averageQuality != null) Text('Avg quality: ${(summary.averageQuality! * 100).toStringAsFixed(0)}%', style: theme.textTheme.bodySmall),
        Text('Sourcing cost: ${summary.totalSourcingCost.toStringAsFixed(0)}', style: theme.textTheme.bodySmall),
      ],
    );
  }
}
