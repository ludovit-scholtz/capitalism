import 'package:flutter/material.dart';

/// A minimal line-chart widget for price/rate history — the mobile
/// equivalent of the web's `RankHistoryChart.vue`/stock-trading bar chart,
/// without adding a charting package dependency (matches the precedent set
/// by `PlayerProfileScreen`'s rank-history list). Values are plotted
/// left-to-right in the order given; a flat/empty series renders as a
/// centered line rather than throwing on divide-by-zero.
class SparklineChart extends StatelessWidget {
  const SparklineChart({super.key, required this.values, this.color, this.height = 80});

  final List<double> values;
  final Color? color;
  final double height;

  @override
  Widget build(BuildContext context) {
    final lineColor = color ?? Theme.of(context).colorScheme.primary;
    return SizedBox(
      height: height,
      width: double.infinity,
      child: CustomPaint(painter: _SparklinePainter(values: values, color: lineColor)),
    );
  }
}

class _SparklinePainter extends CustomPainter {
  _SparklinePainter({required this.values, required this.color});

  final List<double> values;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    if (values.length < 2) return;

    final minValue = values.reduce((a, b) => a < b ? a : b);
    final maxValue = values.reduce((a, b) => a > b ? a : b);
    final range = maxValue - minValue;

    final path = Path();
    for (var i = 0; i < values.length; i++) {
      final x = size.width * i / (values.length - 1);
      final normalized = range == 0 ? 0.5 : (values[i] - minValue) / range;
      final y = size.height * (1 - normalized);
      if (i == 0) {
        path.moveTo(x, y);
      } else {
        path.lineTo(x, y);
      }
    }

    canvas.drawPath(
      path,
      Paint()
        ..color = color
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2
        ..strokeJoin = StrokeJoin.round,
    );
  }

  @override
  bool shouldRepaint(covariant _SparklinePainter oldDelegate) =>
      oldDelegate.values != values || oldDelegate.color != color;
}
