// Flutter port of `UnitLinkArrow.vue`/`DiagonalConnector.vue`'s visuals.
// Not a pixel-for-pixel SVG port (that would need a much larger custom-paint
// investment for a mobile-only surface) — a `CustomPainter`-drawn shaft +
// 0/1/2 arrowhead triangles reproduces the same information (direction,
// active/inactive, both-directions) with the same color/dash-vs-solid
// encoding the web uses.

import 'package:flutter/material.dart';

import 'building_link_helpers.dart';

const Color _linkActiveColor = Color(0xFF3B82F6);
const Color _linkInactiveColor = Color(0xFF9CA3AF);

enum LinkOrientation { horizontal, vertical }

class LinkConnectorButton extends StatelessWidget {
  const LinkConnectorButton({
    super.key,
    required this.orientation,
    required this.state,
    required this.canToggle,
    required this.onTap,
    this.size = 32,
    this.thickness = 88,
    this.dimWhenDisabled = true,
  });

  final LinkOrientation orientation;
  final LinkState state;
  final bool canToggle;
  final VoidCallback onTap;

  /// Length along the connector's own axis (e.g. width for a horizontal
  /// connector sitting in the gap column).
  final double size;

  /// Length along the cross axis (matches the adjacent cell size so the
  /// connector visually spans the same row/column).
  final double thickness;

  /// When `canToggle` is false, whether to render at reduced opacity. The
  /// editor uses this to mean "disabled" (no adjacent cell to link to); the
  /// read-only grid passes `false` here since every connector is
  /// non-interactive there but should still show its live flow state at
  /// full opacity rather than looking disabled.
  final bool dimWhenDisabled;

  @override
  Widget build(BuildContext context) {
    final width = orientation == LinkOrientation.horizontal ? size : thickness;
    final height = orientation == LinkOrientation.horizontal ? thickness : size;
    return Opacity(
      opacity: (!canToggle && dimWhenDisabled) ? 0.28 : 1,
      child: SizedBox(
        width: width,
        height: height,
        child: Material(
          color: Colors.transparent,
          child: InkWell(
            onTap: canToggle ? onTap : null,
            child: CustomPaint(painter: _LinkArrowPainter(orientation: orientation, state: state)),
          ),
        ),
      ),
    );
  }
}

class _LinkArrowPainter extends CustomPainter {
  _LinkArrowPainter({required this.orientation, required this.state});

  final LinkOrientation orientation;
  final LinkState state;

  @override
  void paint(Canvas canvas, Size size) {
    final active = state != LinkState.none;
    final color = active ? _linkActiveColor : _linkInactiveColor;
    final center = Offset(size.width / 2, size.height / 2);
    final isHorizontal = orientation == LinkOrientation.horizontal;
    final start = isHorizontal ? Offset(4, center.dy) : Offset(center.dx, 4);
    final end = isHorizontal ? Offset(size.width - 4, center.dy) : Offset(center.dx, size.height - 4);

    final linePaint = Paint()
      ..color = color
      ..strokeWidth = 2
      ..style = PaintingStyle.stroke;

    if (active) {
      canvas.drawLine(start, end, linePaint);
    } else {
      _drawDashedLine(canvas, start, end, linePaint);
    }

    final showStartArrow = state == LinkState.backward || state == LinkState.both;
    final showEndArrow = state == LinkState.forward || state == LinkState.both;
    final fillPaint = Paint()..color = color;

    if (showStartArrow) canvas.drawPath(_arrowhead(start, pointingToStart: true, horizontal: isHorizontal), fillPaint);
    if (showEndArrow) canvas.drawPath(_arrowhead(end, pointingToStart: false, horizontal: isHorizontal), fillPaint);
  }

  Path _arrowhead(Offset tip, {required bool pointingToStart, required bool horizontal}) {
    const wingLength = 4.0;
    const wingSpread = 3.0;
    final path = Path()..moveTo(tip.dx, tip.dy);
    if (horizontal) {
      final dx = pointingToStart ? wingLength : -wingLength;
      path.lineTo(tip.dx + dx, tip.dy - wingSpread);
      path.lineTo(tip.dx + dx, tip.dy + wingSpread);
    } else {
      final dy = pointingToStart ? wingLength : -wingLength;
      path.lineTo(tip.dx - wingSpread, tip.dy + dy);
      path.lineTo(tip.dx + wingSpread, tip.dy + dy);
    }
    path.close();
    return path;
  }

  void _drawDashedLine(Canvas canvas, Offset start, Offset end, Paint paint) {
    const dashLength = 3.0;
    const gapLength = 3.0;
    final total = (end - start).distance;
    final direction = (end - start) / total;
    var covered = 0.0;
    while (covered < total) {
      final segmentEnd = (covered + dashLength).clamp(0, total);
      canvas.drawLine(start + direction * covered, start + direction * segmentEnd.toDouble(), paint);
      covered += dashLength + gapLength;
    }
  }

  @override
  bool shouldRepaint(covariant _LinkArrowPainter oldDelegate) => oldDelegate.state != state || oldDelegate.orientation != orientation;
}

/// Port of `DiagonalConnector.vue` — the `\`/`/` diagonals of the 2x2 block
/// at the connector-row/gap-column intersection between 4 cells. Left half
/// toggles the primary (`\`) diagonal, right half toggles the secondary
/// (`/`) diagonal — matching the web's left/right hit-area split.
class DiagonalConnectorWidget extends StatelessWidget {
  const DiagonalConnectorWidget({
    super.key,
    required this.primaryState,
    required this.secondaryState,
    required this.canTogglePrimary,
    required this.canToggleSecondary,
    required this.onTogglePrimary,
    required this.onToggleSecondary,
    this.size = 32,
    this.dimWhenDisabled = true,
  });

  final LinkState primaryState;
  final LinkState secondaryState;
  final bool canTogglePrimary;
  final bool canToggleSecondary;
  final VoidCallback onTogglePrimary;
  final VoidCallback onToggleSecondary;
  final double size;

  /// See `LinkConnectorButton.dimWhenDisabled`.
  final bool dimWhenDisabled;

  @override
  Widget build(BuildContext context) {
    final disabled = !canTogglePrimary && !canToggleSecondary;
    return Opacity(
      opacity: disabled && dimWhenDisabled ? 0.28 : 1,
      child: SizedBox(
        width: size,
        height: size,
        child: Stack(
          children: [
            CustomPaint(size: Size(size, size), painter: _DiagonalPainter(primaryState: primaryState, secondaryState: secondaryState)),
            Positioned(
              left: 0,
              top: 0,
              bottom: 0,
              width: size / 2,
              child: canTogglePrimary ? GestureDetector(onTap: onTogglePrimary, behavior: HitTestBehavior.opaque) : const SizedBox(),
            ),
            Positioned(
              right: 0,
              top: 0,
              bottom: 0,
              width: size / 2,
              child: canToggleSecondary ? GestureDetector(onTap: onToggleSecondary, behavior: HitTestBehavior.opaque) : const SizedBox(),
            ),
          ],
        ),
      ),
    );
  }
}

class _DiagonalPainter extends CustomPainter {
  _DiagonalPainter({required this.primaryState, required this.secondaryState});

  final LinkState primaryState;
  final LinkState secondaryState;

  @override
  void paint(Canvas canvas, Size size) {
    _paintDiagonal(canvas, size, state: primaryState, from: const Offset(3, 3), to: Offset(size.width - 3, size.height - 3));
    _paintDiagonal(canvas, size, state: secondaryState, from: Offset(size.width - 3, 3), to: Offset(3, size.height - 3));
  }

  void _paintDiagonal(Canvas canvas, Size size, {required LinkState state, required Offset from, required Offset to}) {
    final active = state != LinkState.none;
    final color = active ? _linkActiveColor : _linkInactiveColor;
    final paint = Paint()
      ..color = color
      ..strokeWidth = 3
      ..strokeCap = StrokeCap.round;
    canvas.drawLine(from, to, paint);

    if (active) {
      final tip = state == LinkState.backward ? from : to;
      final fillPaint = Paint()..color = color;
      final direction = (to - from) / (to - from).distance;
      final perpendicular = Offset(-direction.dy, direction.dx);
      final back = tip - direction * (state == LinkState.backward ? -6 : 6);
      final path = Path()
        ..moveTo(tip.dx, tip.dy)
        ..lineTo(back.dx + perpendicular.dx * 3, back.dy + perpendicular.dy * 3)
        ..lineTo(back.dx - perpendicular.dx * 3, back.dy - perpendicular.dy * 3)
        ..close();
      canvas.drawPath(path, fillPaint);
    }
  }

  @override
  bool shouldRepaint(covariant _DiagonalPainter oldDelegate) =>
      oldDelegate.primaryState != primaryState || oldDelegate.secondaryState != secondaryState;
}
