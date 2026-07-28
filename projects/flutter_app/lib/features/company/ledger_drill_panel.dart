// Per-category drill-down entries panel for the Ledger screen, ported from
// the "drill-panel" section of `LedgerMainContent.vue` — lists the
// individual ledger entries behind a statement-row total when its ▼ button
// is toggled.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import 'ledger_models.dart';

class LedgerDrillPanel extends StatelessWidget {
  const LedgerDrillPanel({super.key, required this.category, required this.entries, required this.loading, required this.onClose});

  final String category;
  final List<LedgerEntryResult> entries;
  final bool loading;
  final VoidCallback onClose;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text('Drill down: $category', style: theme.textTheme.titleSmall)),
                TextButton(onPressed: onClose, child: const Text('✕ Close')),
              ],
            ),
            const SizedBox(height: 8),
            if (loading)
              const Center(child: Padding(padding: EdgeInsets.symmetric(vertical: 16), child: CircularProgressIndicator()))
            else if (entries.isEmpty)
              const Padding(padding: EdgeInsets.symmetric(vertical: 8), child: Text('No entries recorded for this category yet.'))
            else
              for (var i = 0; i < entries.length; i++) ...[
                _EntryRow(entry: entries[i], languageCode: languageCode),
                if (i < entries.length - 1) const Divider(height: 12),
              ],
          ],
        ),
      ),
    );
  }
}

class _EntryRow extends StatelessWidget {
  const _EntryRow({required this.entry, required this.languageCode});

  final LedgerEntryResult entry;
  final String languageCode;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = entry.amount >= 0 ? Colors.green.shade600 : Colors.red.shade600;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(entry.productName ?? entry.resourceName ?? entry.description, style: theme.textTheme.bodyMedium),
              Text('Tick ${entry.recordedAtTick}', style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
              if (entry.buildingId != null)
                InkWell(
                  onTap: () => context.go(entry.buildingType == 'BANK' ? '/bank/${entry.buildingId}' : '/building/${entry.buildingId}'),
                  child: Text(
                    entry.buildingName ?? 'View building',
                    style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.primary, decoration: TextDecoration.underline),
                  ),
                ),
            ],
          ),
        ),
        Text(
          AppNumberFormat.money(entry.amount, currencyCode: entry.currencyCode, languageCode: languageCode),
          style: theme.textTheme.bodyMedium?.copyWith(color: color, fontWeight: FontWeight.w600),
        ),
      ],
    );
  }
}
