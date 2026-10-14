import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';

import '../../core/theme/app_spacing.dart';

/// Icon-over-label tile for a company's frequent actions. Replaces a row of
/// identical outlined buttons so the primary verbs are scannable at a glance
/// and reachable with a thumb.
class QuickActionTile extends StatelessWidget {
  const QuickActionTile({super.key, required this.icon, required this.label, required this.onTap, this.color});

  final FaIconData icon;
  final String label;
  final VoidCallback onTap;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final accent = color ?? theme.colorScheme.primary;
    return SizedBox(
      width: 88,
      child: Semantics(
        button: true,
        child: Material(
          color: accent.withValues(alpha: 0.08),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadius.md),
            side: BorderSide(color: accent.withValues(alpha: 0.28)),
          ),
          clipBehavior: Clip.antiAlias,
          child: InkWell(
            onTap: () {
              unawaited(HapticFeedback.selectionClick());
              onTap();
            },
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm + 4, horizontal: AppSpacing.xs),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  FaIcon(icon, size: 20, color: accent),
                  const SizedBox(height: AppSpacing.xs + 2),
                  Text(
                    label,
                    style: theme.textTheme.labelMedium,
                    textAlign: TextAlign.center,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
