import 'package:flutter/material.dart';

import '../theme/app_spacing.dart';

/// Compact "label over big value" tile for key figures (cash, buildings,
/// profit). [valueWidget] lets callers swap in an [AnimatedNumber].
class StatTile extends StatelessWidget {
  const StatTile({super.key, required this.label, this.value, this.valueWidget, this.icon, this.color})
      : assert(value != null || valueWidget != null, 'Provide value or valueWidget');

  final String label;
  final String? value;
  final Widget? valueWidget;
  final IconData? icon;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final accent = color ?? theme.colorScheme.primary;
    return MergeSemantics(
      child: Container(
        padding: const EdgeInsets.all(AppSpacing.md),
        decoration: BoxDecoration(
          color: accent.withValues(alpha: 0.08),
          borderRadius: BorderRadius.circular(AppRadius.md),
          border: Border.all(color: accent.withValues(alpha: 0.28)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                if (icon != null) ...[Icon(icon, size: 14, color: accent), const SizedBox(width: AppSpacing.xs + 2)],
                Flexible(child: Text(label, style: theme.textTheme.labelSmall, overflow: TextOverflow.ellipsis)),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            DefaultTextStyle.merge(
              style: theme.textTheme.titleLarge?.copyWith(color: accent),
              softWrap: false,
              child: FittedBox(
                fit: BoxFit.scaleDown,
                alignment: Alignment.centerLeft,
                child: valueWidget ?? Text(value!),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
