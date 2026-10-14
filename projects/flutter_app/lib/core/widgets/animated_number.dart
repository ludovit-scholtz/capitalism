import 'package:flutter/material.dart';

/// Text that tweens between numeric values (balances, prices) instead of
/// snapping, so changes after a tick are noticeable. [builder] formats the
/// in-between value — pass an `AppNumberFormat` call so it stays locale-aware.
class AnimatedNumber extends StatelessWidget {
  const AnimatedNumber({
    super.key,
    required this.value,
    required this.builder,
    this.style,
    this.duration = const Duration(milliseconds: 600),
  });

  final double value;
  final String Function(double value) builder;
  final TextStyle? style;
  final Duration duration;

  @override
  Widget build(BuildContext context) {
    final reduce = MediaQuery.maybeDisableAnimationsOf(context) ?? false;
    if (reduce) return Text(builder(value), style: style);
    return TweenAnimationBuilder<double>(
      tween: Tween<double>(begin: value, end: value),
      duration: duration,
      curve: Curves.easeOutCubic,
      builder: (context, v, _) => Text(builder(v), style: style),
    );
  }
}
