import 'package:flutter/material.dart';

/// Compact horizontal bar-history row — this app's hand-rolled chart
/// primitive (no charting dependency added, matching the existing
/// precedent set by `building_inventory_fill_bar.dart` and the web's own
/// CSS-bar-chart approach in `BuildingReadonlySidebar.vue`). Reused across
/// the PUBLIC_SALES tools panel and the building/unit analytics panels
/// (ROADMAP 135/137) for revenue/price/profit/resource-flow history.
class BarHistoryRow extends StatelessWidget {
  const BarHistoryRow({super.key, required this.values, required this.color, this.height = 48, this.allowNegative = false});

  final List<double> values;
  final Color color;
  final double height;

  /// When true, negative values render in red regardless of [color] —
  /// used for profit history, which can dip below zero.
  final bool allowNegative;

  @override
  Widget build(BuildContext context) {
    if (values.isEmpty) return const SizedBox.shrink();
    final maxAbs = values.fold<double>(0, (max, v) => v.abs() > max ? v.abs() : max);
    return SizedBox(
      height: height,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          for (final value in values)
            Expanded(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 1),
                child: Tooltip(
                  message: value.toStringAsFixed(1),
                  child: FractionallySizedBox(
                    alignment: Alignment.bottomCenter,
                    heightFactor: maxAbs == 0 ? 0.02 : (value.abs() / maxAbs).clamp(0.02, 1),
                    child: ColoredBox(color: allowNegative && value < 0 ? const Color(0xFFEF4444) : color),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}
