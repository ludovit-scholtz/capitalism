// Passive-income history panel for the Personal Ledger screen, ported from
// the "passive income history" section of
// `projects/frontend/src/views/PersonalLedgerView.vue` — merges dividend
// and interest payments into one tick-descending list with an
// ALL/INTEREST/DIVIDEND filter.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import 'personal_ledger_models.dart';

enum PersonalIncomeFilter { all, interest, dividend }

class _IncomeRow {
  const _IncomeRow({required this.id, required this.isInterest, required this.source, required this.amount, required this.currencyCode, required this.recordedAtTick, this.description});

  final String id;
  final bool isInterest;
  final String source;
  final double amount;
  final String currencyCode;
  final int recordedAtTick;
  final String? description;
}

class PersonalLedgerIncomePanel extends StatefulWidget {
  const PersonalLedgerIncomePanel({super.key, required this.dividendPayments, required this.interestPayments});

  final List<PersonalDividendPayment> dividendPayments;
  final List<PersonalInterestPayment> interestPayments;

  @override
  State<PersonalLedgerIncomePanel> createState() => _PersonalLedgerIncomePanelState();
}

class _PersonalLedgerIncomePanelState extends State<PersonalLedgerIncomePanel> {
  PersonalIncomeFilter _filter = PersonalIncomeFilter.all;

  List<_IncomeRow> get _rows {
    final rows = [
      for (final payment in widget.dividendPayments)
        _IncomeRow(id: payment.id, isInterest: false, source: payment.companyName, amount: payment.totalAmount, currencyCode: 'EUR', recordedAtTick: payment.recordedAtTick, description: payment.description),
      for (final payment in widget.interestPayments)
        _IncomeRow(
          id: payment.id,
          isInterest: true,
          source: payment.bankBuildingName != null ? '${payment.companyName} · ${payment.bankBuildingName}' : payment.companyName,
          amount: payment.amount,
          currencyCode: payment.currencyCode,
          recordedAtTick: payment.recordedAtTick,
          description: payment.description,
        ),
    ]..sort((a, b) => b.recordedAtTick.compareTo(a.recordedAtTick));
    return switch (_filter) {
      PersonalIncomeFilter.all => rows,
      PersonalIncomeFilter.interest => rows.where((r) => r.isInterest).toList(),
      PersonalIncomeFilter.dividend => rows.where((r) => !r.isInterest).toList(),
    };
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;
    final rows = _rows;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Passive income history', style: theme.textTheme.titleMedium),
            Text(
              'Dividends and bank interest paid into your personal account.',
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              children: [
                ChoiceChip(label: const Text('ALL'), selected: _filter == PersonalIncomeFilter.all, onSelected: (_) => setState(() => _filter = PersonalIncomeFilter.all)),
                ChoiceChip(
                  key: const ValueKey('income-filter-interest'),
                  label: const Text('INTEREST'),
                  selected: _filter == PersonalIncomeFilter.interest,
                  onSelected: (_) => setState(() => _filter = PersonalIncomeFilter.interest),
                ),
                ChoiceChip(
                  key: const ValueKey('income-filter-dividend'),
                  label: const Text('DIVIDEND'),
                  selected: _filter == PersonalIncomeFilter.dividend,
                  onSelected: (_) => setState(() => _filter = PersonalIncomeFilter.dividend),
                ),
              ],
            ),
            const SizedBox(height: 12),
            if (rows.isEmpty)
              const Padding(padding: EdgeInsets.symmetric(vertical: 16), child: Center(child: Text('No passive income recorded yet.')))
            else
              for (var i = 0; i < rows.length; i++) ...[
                _row(theme, languageCode, rows[i]),
                if (i < rows.length - 1) const Divider(height: 12),
              ],
          ],
        ),
      ),
    );
  }

  Widget _row(ThemeData theme, String languageCode, _IncomeRow row) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          margin: const EdgeInsets.only(top: 2),
          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
          decoration: BoxDecoration(
            color: (row.isInterest ? Colors.green : theme.colorScheme.primary).withValues(alpha: 0.15),
            borderRadius: BorderRadius.circular(999),
          ),
          child: Text(
            row.isInterest ? 'INTEREST' : 'DIVIDEND',
            style: TextStyle(color: row.isInterest ? Colors.green.shade700 : theme.colorScheme.primary, fontSize: 10, fontWeight: FontWeight.bold),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(row.source, style: theme.textTheme.bodyMedium),
              Text('Tick #${row.recordedAtTick}', style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
              if (row.description != null && row.description!.isNotEmpty)
                Text(row.description!, style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
            ],
          ),
        ),
        Text(
          AppNumberFormat.money(row.amount, currencyCode: row.currencyCode, languageCode: languageCode),
          style: theme.textTheme.bodyMedium?.copyWith(color: Colors.green.shade600, fontWeight: FontWeight.w600),
        ),
      ],
    );
  }
}
