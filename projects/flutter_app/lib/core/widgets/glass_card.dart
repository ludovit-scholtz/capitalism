import 'package:flutter/material.dart';

import '../theme/app_spacing.dart';

/// Frosted-glass surface used for every grouped block of content. Replaces
/// ad-hoc `Card` + `Padding` combos so spacing, radius and the optional tap
/// ripple stay consistent. When [onTap] is set the whole surface is a button.
class GlassCard extends StatelessWidget {
  const GlassCard({
    super.key,
    required this.child,
    this.onTap,
    this.padding = const EdgeInsets.all(AppSpacing.md),
    this.accent,
    this.margin = EdgeInsets.zero,
  });

  final Widget child;
  final VoidCallback? onTap;
  final EdgeInsetsGeometry padding;
  final EdgeInsetsGeometry margin;

  /// Optional accent that tints the border (e.g. status color).
  final Color? accent;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final radius = BorderRadius.circular(AppRadius.lg);
    final borderColor = (accent ?? scheme.outline).withValues(alpha: accent == null ? 0.6 : 0.55);

    return Padding(
      padding: margin,
      child: Material(
        color: scheme.surfaceContainer.withValues(alpha: 0.72),
        shape: RoundedRectangleBorder(borderRadius: radius, side: BorderSide(color: borderColor)),
        clipBehavior: Clip.antiAlias,
        child: onTap == null
            ? Padding(padding: padding, child: child)
            : InkWell(onTap: onTap, child: Padding(padding: padding, child: child)),
      ),
    );
  }
}
