// "Race to the Top" endgame benchmark card for the Personal Ledger screen,
// ported from the race-panel section of
// `projects/frontend/src/views/PersonalLedgerView.vue` — progress toward
// beating the #1 real-world billionaire's wealth, plus the top-5
// real-world wealth leaderboard used as the benchmark.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import '../leaderboard/leaderboard_models.dart';

class PersonalLedgerRaceCard extends StatelessWidget {
  const PersonalLedgerRaceCard({super.key, required this.endgame, required this.playerNetWorthUsd});

  final EndgameStatus endgame;
  final double playerNetWorthUsd;

  int get _progressPercent {
    final threshold = endgame.winningThresholdUsd;
    if (threshold <= 0) return 0;
    return ((playerNetWorthUsd / threshold) * 100).round().clamp(0, 100);
  }

  double get _gapUsd => (endgame.winningThresholdUsd - playerNetWorthUsd).clamp(0, double.infinity);

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;
    final topFive = endgame.topRealWorldRichest.take(5).toList();

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Race to the Top', style: theme.textTheme.titleMedium),
                      Text(
                        'Beat the #1 richest real-world billionaire to end the game and claim victory.',
                        style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                    ],
                  ),
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text('YOUR PROGRESS', style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
                    Text('$_progressPercent%', style: theme.textTheme.titleLarge?.copyWith(color: theme.colorScheme.primary)),
                  ],
                ),
              ],
            ),
            const SizedBox(height: 12),
            ClipRRect(
              borderRadius: BorderRadius.circular(4),
              child: LinearProgressIndicator(
                value: _progressPercent / 100,
                minHeight: 10,
                backgroundColor: theme.colorScheme.surfaceContainerHighest,
                color: theme.colorScheme.primary,
              ),
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 16,
              runSpacing: 4,
              children: [
                _metric(theme, 'Target wealth (#1 benchmark)', AppNumberFormat.money(endgame.winningThresholdUsd, currencyCode: 'USD', languageCode: languageCode)),
                _metric(theme, 'Your net worth (USD)', AppNumberFormat.money(playerNetWorthUsd, currencyCode: 'USD', languageCode: languageCode)),
                _metric(theme, 'Gap to victory', AppNumberFormat.money(_gapUsd, currencyCode: 'USD', languageCode: languageCode)),
              ],
            ),
            if (topFive.isNotEmpty) ...[
              const SizedBox(height: 16),
              for (final entry in topFive)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    children: [
                      SizedBox(width: 32, child: Text('#${entry.rank}', style: theme.textTheme.bodySmall?.copyWith(fontWeight: FontWeight.bold))),
                      Expanded(child: Text(entry.name, style: theme.textTheme.bodySmall)),
                      Text(
                        AppNumberFormat.money(entry.wealthUsd, currencyCode: 'USD', languageCode: languageCode),
                        style: theme.textTheme.bodySmall?.copyWith(fontWeight: FontWeight.w600),
                      ),
                    ],
                  ),
                ),
            ],
            const SizedBox(height: 8),
            Text(
              'Approximate values, updated periodically by game administration.',
              style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
        ),
      ),
    );
  }

  Widget _metric(ThemeData theme, String label, String value) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
        Text(value, style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600)),
      ],
    );
  }
}
