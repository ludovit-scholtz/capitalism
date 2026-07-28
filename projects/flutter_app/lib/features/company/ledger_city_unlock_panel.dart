// City-unlock progress panel for the Ledger screen, ported from
// `projects/frontend/src/components/ledger/CityExpansionPanel.vue` — shows
// each city's net-worth requirement and progress toward unlocking it for
// this company.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import 'ledger_models.dart';

class LedgerCityUnlockPanel extends StatelessWidget {
  const LedgerCityUnlockPanel({super.key, required this.statuses});

  final List<CityUnlockStatus> statuses;

  @override
  Widget build(BuildContext context) {
    if (statuses.isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('CITY EXPANSION', style: theme.textTheme.labelLarge?.copyWith(fontWeight: FontWeight.bold, letterSpacing: 0.5)),
            const SizedBox(height: 4),
            Text(
              'Unlock progress toward operating in new cities.',
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [for (final status in statuses) _CityUnlockCard(status: status, languageCode: languageCode)],
            ),
          ],
        ),
      ),
    );
  }
}

class _CityUnlockCard extends StatelessWidget {
  const _CityUnlockCard({required this.status, required this.languageCode});

  final CityUnlockStatus status;
  final String languageCode;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final progress = status.progressPercentClamped;

    return Container(
      width: 260,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(border: Border.all(color: theme.colorScheme.outlineVariant), borderRadius: BorderRadius.circular(10)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(status.cityName, style: theme.textTheme.titleSmall),
                    Text('${status.countryCode} · ${status.currency}', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
                  ],
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                decoration: BoxDecoration(
                  color: (status.isUnlocked ? Colors.green : Colors.amber).withValues(alpha: 0.15),
                  borderRadius: BorderRadius.circular(4),
                ),
                child: Text(
                  status.isUnlocked ? 'UNLOCKED' : 'LOCKED',
                  style: TextStyle(color: status.isUnlocked ? Colors.green.shade700 : Colors.amber.shade800, fontSize: 10, fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          ClipRRect(
            borderRadius: BorderRadius.circular(3),
            child: LinearProgressIndicator(
              value: progress / 100,
              minHeight: 8,
              backgroundColor: theme.colorScheme.surfaceContainerHighest,
              color: theme.colorScheme.primary,
            ),
          ),
          const SizedBox(height: 4),
          Text(status.isUnlocked ? 'Complete' : '$progress%', style: theme.textTheme.labelSmall),
          const SizedBox(height: 8),
          _metric(theme, 'Net worth', AppNumberFormat.money(status.currentNetWorth, currencyCode: status.currency, languageCode: languageCode)),
          _metric(theme, 'Required', AppNumberFormat.money(status.requiredNetWorth, currencyCode: status.currency, languageCode: languageCode)),
          _metric(
            theme,
            'ETA',
            status.isUnlocked
                ? 'Available now'
                : status.estimatedTicksToUnlock != null && status.estimatedTicksToUnlock! > 0
                ? '${AppNumberFormat.number(status.estimatedTicksToUnlock!, languageCode: languageCode)} ticks'
                : '—',
          ),
        ],
      ),
    );
  }

  Widget _metric(ThemeData theme, String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 1),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
          Text(value, style: theme.textTheme.bodySmall?.copyWith(fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}
