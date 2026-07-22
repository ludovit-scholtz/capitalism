import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'onboarding_models.dart';

/// Mirrors the core of step 7 in `OnboardingView.vue`: a completion summary
/// plus navigation onward. Deliberately trimmed from the web version: no
/// auto-polled first-sale-mission detection/celebration panel (that relies
/// on `useTickRefresh` polling a live backend — out of scope for this pass),
/// no per-industry price-benchmark "configure guide" cards. The "mark
/// complete" button still calls the real `completeFirstSaleMilestone`
/// mutation.
class OnboardingCompleteStep extends StatelessWidget {
  const OnboardingCompleteStep({
    super.key,
    required this.isGuestMode,
    required this.result,
    required this.onSaveProgress,
    required this.onMarkMilestoneComplete,
    required this.savingProgress,
    required this.completingMilestone,
    required this.milestoneCompleted,
    this.error,
  });

  final bool isGuestMode;
  final OnboardingCompletionResult? result;
  final VoidCallback onSaveProgress;
  final VoidCallback onMarkMilestoneComplete;
  final bool savingProgress;
  final bool completingMilestone;
  final bool milestoneCompleted;
  final String? error;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Icon(Icons.celebration_outlined, size: 48, color: theme.colorScheme.primary),
          const SizedBox(height: 12),
          Text('Your empire has launched!', style: theme.textTheme.headlineSmall, textAlign: TextAlign.center),
          const SizedBox(height: 16),
          if (error != null)
            Container(
              padding: const EdgeInsets.all(12),
              margin: const EdgeInsets.only(bottom: 16),
              decoration: BoxDecoration(
                color: theme.colorScheme.errorContainer,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(error!, style: TextStyle(color: theme.colorScheme.onErrorContainer)),
            ),
          if (result != null)
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Company', style: theme.textTheme.labelMedium),
                    Text(result!.companyName, style: theme.textTheme.titleMedium),
                    const SizedBox(height: 8),
                    Text('Starter product', style: theme.textTheme.labelMedium),
                    Text(result!.selectedProductName, style: theme.textTheme.titleMedium),
                    const SizedBox(height: 8),
                    Text('Cash on hand', style: theme.textTheme.labelMedium),
                    Text(
                      '${result!.companyCash.toStringAsFixed(0)} ${result!.cityCurrencyCode}',
                      style: theme.textTheme.titleMedium,
                    ),
                  ],
                ),
              ),
            ),
          const SizedBox(height: 16),
          if (isGuestMode) ...[
            const Text('Sign in with Biatec to save this progress to a real account.'),
            const SizedBox(height: 8),
            FilledButton.icon(
              onPressed: savingProgress ? null : onSaveProgress,
              icon: savingProgress
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(Icons.login),
              label: const Text('Save Progress'),
            ),
          ] else if (!milestoneCompleted) ...[
            const Text('Configure your sales shop, make your first sale, then mark this milestone complete.'),
            const SizedBox(height: 8),
            OutlinedButton(
              onPressed: completingMilestone ? null : onMarkMilestoneComplete,
              child: completingMilestone
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Mark milestone complete'),
            ),
            const SizedBox(height: 8),
            TextButton(onPressed: () => context.go('/dashboard'), child: const Text('Go to Dashboard')),
          ] else ...[
            const SizedBox(height: 8),
            FilledButton(onPressed: () => context.go('/dashboard'), child: const Text('Go to Dashboard')),
            const SizedBox(height: 8),
            TextButton(onPressed: () => context.go('/leaderboard'), child: const Text('View Leaderboard')),
            TextButton(onPressed: () => context.go('/encyclopedia'), child: const Text('Browse Encyclopedia')),
          ],
        ],
      ),
    );
  }
}
