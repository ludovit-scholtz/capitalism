import 'package:flutter/material.dart';

import '../theme/app_spacing.dart';
import 'skeleton.dart';

enum _StateKind { loading, empty, error }

/// One consistent treatment for the three non-content states of any screen:
/// skeleton loading, empty, and error-with-retry.
class StateView extends StatelessWidget {
  const StateView.loading({super.key})
      : _kind = _StateKind.loading,
        title = null,
        message = null,
        icon = null,
        onRetry = null,
        retryLabel = 'Retry';

  const StateView.empty({super.key, required this.title, this.message, this.icon = Icons.inbox_outlined})
      : _kind = _StateKind.empty,
        onRetry = null,
        retryLabel = 'Retry';

  const StateView.error({super.key, required this.message, this.onRetry, this.retryLabel = 'Retry'})
      : _kind = _StateKind.error,
        title = null,
        icon = Icons.cloud_off_outlined;

  final _StateKind _kind;
  final String? title;
  final String? message;
  final IconData? icon;
  final VoidCallback? onRetry;
  final String retryLabel;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    if (_kind == _StateKind.loading) {
      return Semantics(
        label: 'Loading',
        child: const Padding(
          padding: EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SkeletonBox(height: 28, width: 180),
              SizedBox(height: AppSpacing.md),
              SkeletonBox(height: 96),
              SizedBox(height: AppSpacing.sm),
              SkeletonBox(height: 96),
              SizedBox(height: AppSpacing.sm),
              SkeletonBox(height: 96),
            ],
          ),
        ),
      );
    }

    final isError = _kind == _StateKind.error;
    final color = isError ? theme.colorScheme.error : theme.colorScheme.onSurfaceVariant;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 48, color: color),
            const SizedBox(height: AppSpacing.md),
            if (title != null) Text(title!, style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
            if (message != null) ...[
              const SizedBox(height: AppSpacing.xs),
              Text(message!, style: theme.textTheme.bodyMedium, textAlign: TextAlign.center),
            ],
            if (isError && onRetry != null) ...[
              const SizedBox(height: AppSpacing.md),
              FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh, size: 18), label: Text(retryLabel)),
            ],
          ],
        ),
      ),
    );
  }
}
