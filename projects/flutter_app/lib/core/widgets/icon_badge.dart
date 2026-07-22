import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';

import '../theme/app_spacing.dart';

/// A small glowing icon chip used as a leading accent on cards, list tiles
/// and section headers — the app's recurring "HUD badge" motif, factored
/// out so every screen gets the same glow/shape instead of hand-rolling a
/// `Container` + `BoxShadow` per usage.
class IconBadge extends StatelessWidget {
  const IconBadge({super.key, required this.icon, this.color, this.size = 40, this.iconSize = 18});

  final IconData icon;
  final Color? color;
  final double size;
  final double iconSize;

  @override
  Widget build(BuildContext context) {
    final accent = color ?? Theme.of(context).colorScheme.primary;
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: accent.withValues(alpha: 0.14),
        border: Border.all(color: accent.withValues(alpha: 0.45)),
        boxShadow: [BoxShadow(color: accent.withValues(alpha: 0.35), blurRadius: 12, spreadRadius: -2)],
      ),
      child: FaIcon(icon, size: iconSize, color: accent),
    );
  }
}

/// Section heading with an [IconBadge] + title (+ optional trailing widget)
/// used at the top of card groups across screens for a consistent rhythm.
class SectionHeading extends StatelessWidget {
  const SectionHeading({super.key, required this.icon, required this.title, this.color, this.trailing});

  final IconData icon;
  final String title;
  final Color? color;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      children: [
        IconBadge(icon: icon, color: color, size: 32, iconSize: 14),
        const SizedBox(width: AppSpacing.sm),
        Expanded(child: Text(title, style: theme.textTheme.titleMedium)),
        if (trailing != null) trailing!,
      ],
    );
  }
}
