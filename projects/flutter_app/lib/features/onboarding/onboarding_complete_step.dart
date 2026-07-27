import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_icons.dart';
import '../../core/widgets/game_tick_time.dart';
import '../../core/widgets/icon_badge.dart';
import 'onboarding_models.dart';

/// Mirrors the core of step 7 in `OnboardingView.vue`: a completion summary,
/// the auto-polled first-sale-mission celebration panel, and navigation
/// onward. Deliberately trimmed from the web version: no per-industry
/// price-benchmark "configure guide" cards. The "mark complete" button still
/// calls the real `completeFirstSaleMilestone` mutation as a manual
/// fallback, even though `OnboardingScreen` now auto-fires it once
/// [missionStatus] reports `FIRST_SALE_RECORDED`.
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
    required this.formatMoney,
    this.missionStatus,
    this.error,
  });

  final bool isGuestMode;
  final OnboardingCompletionResult? result;
  final VoidCallback onSaveProgress;
  final VoidCallback onMarkMilestoneComplete;
  final bool savingProgress;
  final bool completingMilestone;
  final bool milestoneCompleted;

  /// Formats an amount already denominated in the given currency code (the
  /// completion result's `companyCash`/`cityCurrencyCode` are already the
  /// real, backend-computed local-currency balance — no conversion needed
  /// here, only symbol/decimals formatting).
  final String Function(double amount, String currencyCode) formatMoney;

  /// Latest tick-polled first-sale-mission status, if any has been fetched
  /// yet (`OnboardingScreen._refreshFirstSaleMission`). `null` until the
  /// first tick after arriving on this step.
  final FirstSaleMissionStatus? missionStatus;
  final String? error;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Center(child: IconBadge(icon: AppIcons.celebration, size: 64, iconSize: 28)),
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
                      formatMoney(result!.companyCash, result!.cityCurrencyCode),
                      style: theme.textTheme.titleMedium,
                    ),
                  ],
                ),
              ),
            ),
          if (missionStatus?.firstSaleRevenue != null) ...[
            const SizedBox(height: 12),
            Card(
              key: const Key('first-sale-celebration'),
              color: theme.colorScheme.primaryContainer,
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        const FaIcon(AppIcons.celebration, size: 18),
                        const SizedBox(width: 8),
                        Text('First sale recorded!', style: theme.textTheme.titleMedium),
                      ],
                    ),
                    const SizedBox(height: 8),
                    if (missionStatus!.firstSaleProductName != null)
                      Text(
                        '${missionStatus!.firstSaleProductName}'
                        '${missionStatus!.firstSaleQuantity != null ? ' · ${missionStatus!.firstSaleQuantity!.toStringAsFixed(0)} units' : ''}',
                      ),
                    Text('Revenue: ${formatMoney(missionStatus!.firstSaleRevenue!, result?.cityCurrencyCode ?? 'USD')}'),
                    if (missionStatus!.firstSalePricePerUnit != null)
                      Text(
                        'Price per unit: ${formatMoney(missionStatus!.firstSalePricePerUnit!, result?.cityCurrencyCode ?? 'USD')}',
                      ),
                    if (missionStatus!.firstSaleTick != null) GameTickTime(missionStatus!.firstSaleTick!),
                  ],
                ),
              ),
            ),
          ],
          const SizedBox(height: 16),
          if (isGuestMode) ...[
            const Text('Sign in with Biatec to save this progress to a real account.'),
            const SizedBox(height: 8),
            FilledButton.icon(
              onPressed: savingProgress ? null : onSaveProgress,
              icon: savingProgress
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const FaIcon(AppIcons.login, size: 16),
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
