// Pure port of `projects/frontend/src/lib/gridTileHelpers.ts`'s fill-bucket
// and flow-segment algorithms, feeding the inventory/capacity fill bar
// (ROADMAP 127).

enum FillBucket { empty, low, medium, high }

FillBucket getFillBucket(double? fillPercent) {
  if (fillPercent == null || fillPercent <= 0) return FillBucket.empty;
  if (fillPercent < 0.75) return FillBucket.low;
  if (fillPercent <= 0.90) return FillBucket.medium;
  return FillBucket.high;
}

class FlowSegments {
  const FlowSegments({
    required this.fillWidth,
    required this.inflowWidth,
    required this.inflowLeft,
    required this.outflowWidth,
    required this.outflowLeft,
    required this.hasMovement,
  });

  /// All widths/lefts are percentages (0-100) of the bar's total width.
  final double fillWidth;
  final double inflowWidth;
  final double inflowLeft;
  final double outflowWidth;
  final double outflowLeft;
  final bool hasMovement;
}

double _clamp(double value, double min, double max) => value < min ? min : (value > max ? max : value);

FlowSegments getFlowSegments(double? fillPercent, double? capacity, double? lastTickInflow, double? lastTickOutflow) {
  final fill = _clamp(fillPercent ?? 0, 0, 1);
  final cap = capacity ?? 0;
  final fillPct = fill * 100;
  final hasMovement = cap > 0 && (lastTickInflow != null || lastTickOutflow != null);

  if (!hasMovement || cap == 0) {
    return FlowSegments(fillWidth: fillPct, inflowWidth: 0, inflowLeft: 0, outflowWidth: 0, outflowLeft: fillPct, hasMovement: false);
  }

  final inflowRatio = _clamp((lastTickInflow ?? 0) / cap, 0, fill);
  final inflowWidth = inflowRatio * 100;
  final fillWidth = (fillPct - inflowWidth).clamp(0, 100).toDouble();
  final inflowLeft = fillWidth;

  final emptyRatio = (1 - fill).clamp(0, 1).toDouble();
  final outflowRatio = _clamp((lastTickOutflow ?? 0) / cap, 0, emptyRatio);
  final outflowWidth = outflowRatio * 100;
  final outflowLeft = fillPct;

  return FlowSegments(fillWidth: fillWidth, inflowWidth: inflowWidth, inflowLeft: inflowLeft, outflowWidth: outflowWidth, outflowLeft: outflowLeft, hasMovement: true);
}
