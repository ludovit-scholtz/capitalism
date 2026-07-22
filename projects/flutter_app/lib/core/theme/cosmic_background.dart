import 'package:flutter/material.dart';

import 'app_theme.dart';

/// Deep-space gradient + faint starfield painted once behind [AppShell]'s
/// `Scaffold` (which is themed with a transparent `scaffoldBackgroundColor`
/// so this shows through) — every screen gets the same backdrop for free,
/// without needing per-screen styling.
class CosmicBackground extends StatelessWidget {
  const CosmicBackground({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: RadialGradient(
          center: Alignment(-0.7, -1.0),
          radius: 1.8,
          colors: [AppTheme.spaceSurfaceHigh, AppTheme.spaceBlack],
          stops: [0.0, 0.85],
        ),
      ),
      child: CustomPaint(painter: _StarfieldPainter(), child: child),
    );
  }
}

/// A sparse, deterministic starfield — deterministic (fixed seed) so golden
/// output and screenshots stay stable across runs rather than flickering
/// with a different random pattern each launch.
class _StarfieldPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()..color = Colors.white.withValues(alpha: 0.5);
    // Fixed seed (not `Random()`/system entropy) for reproducible output.
    var seed = 42;
    int next() {
      seed = (seed * 1103515245 + 12345) & 0x7fffffff;
      return seed;
    }

    for (var i = 0; i < 60; i++) {
      final dx = (next() % 1000) / 1000 * size.width;
      final dy = (next() % 1000) / 1000 * size.height;
      final radius = 0.4 + (next() % 100) / 100 * 0.8;
      canvas.drawCircle(Offset(dx, dy), radius, paint);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
