// Overview tab content, mirroring `DashboardMainContent.vue`'s
// `FinancialSummaryCard` + `StarterGuidance` (Overview tab). Web's Overview
// tab only ever shows one active company; this port keeps this app's
// existing multi-company support (a deliberate prior deviation from web,
// not undone here) by showing one financial card per company instead.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import '../company/company_models.dart';
import 'dashboard_models.dart';

class DashboardOverviewTab extends StatelessWidget {
  const DashboardOverviewTab({
    super.key,
    required this.companies,
    required this.ledgers,
    required this.ledgersLoading,
    this.newCompanyCard,
  });

  final List<DashboardCompany> companies;
  final Map<String, CompanyLedger> ledgers;
  final bool ledgersLoading;

  /// Slot for the Launch-New-Company CTA card, wired in a later phase.
  final Widget? newCompanyCard;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final totalBuildings = companies.fold<int>(0, (sum, c) => sum + c.buildings.length);

    return ListView(
      // Required for pull-to-refresh to trigger via `RefreshIndicator` when
      // this tab's content doesn't overflow the viewport (e.g. a single
      // company with no ledger data yet) — without this, a short list
      // reports no scrollable extent and never emits the overscroll
      // notification `RefreshIndicator` listens for.
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(24),
      children: [
        Text('Financial summary', style: theme.textTheme.titleMedium),
        const SizedBox(height: 12),
        if (ledgersLoading)
          const Center(child: Padding(padding: EdgeInsets.all(24), child: CircularProgressIndicator()))
        else
          for (final company in companies) ...[
            _CompanyFinancialCard(key: ValueKey('overview-ledger-${company.id}'), company: company, ledger: ledgers[company.id]),
            const SizedBox(height: 12),
          ],
        _StarterGuidanceCard(totalBuildings: totalBuildings),
        if (newCompanyCard != null) ...[const SizedBox(height: 16), newCompanyCard!],
      ],
    );
  }
}

double _totalCosts(CompanyLedger ledger) =>
    ledger.totalPurchasingCosts +
    ledger.totalShippingCosts +
    ledger.totalLaborCosts +
    ledger.totalEnergyCosts +
    ledger.totalMarketingCosts +
    ledger.totalTaxPaid +
    ledger.totalOtherCosts;

class _CompanyFinancialCard extends StatelessWidget {
  const _CompanyFinancialCard({super.key, required this.company, required this.ledger});

  final DashboardCompany company;
  final CompanyLedger? ledger;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final ledgerValue = ledger;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(company.name, style: theme.textTheme.titleMedium),
            const SizedBox(height: 8),
            if (ledgerValue == null)
              Text('No financial data yet.', style: theme.textTheme.bodySmall)
            else
              Row(
                children: [
                  Expanded(
                    child: _Metric(label: 'Revenue', value: ledgerValue.totalRevenue, currencyCode: ledgerValue.primaryCurrencyCode),
                  ),
                  Expanded(
                    child: _Metric(label: 'Costs', value: _totalCosts(ledgerValue), currencyCode: ledgerValue.primaryCurrencyCode),
                  ),
                  Expanded(
                    child: _Metric(
                      label: 'Profit',
                      value: ledgerValue.netIncome,
                      currencyCode: ledgerValue.primaryCurrencyCode,
                      emphasize: true,
                    ),
                  ),
                ],
              ),
          ],
        ),
      ),
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value, required this.currencyCode, this.emphasize = false});

  final String label;
  final double value;
  final String currencyCode;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;
    final formatted = AppNumberFormat.money(value, currencyCode: currencyCode, languageCode: languageCode);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: theme.textTheme.labelSmall),
        Text(
          formatted,
          style: emphasize
              ? theme.textTheme.titleMedium?.copyWith(color: value >= 0 ? Colors.green : theme.colorScheme.error)
              : theme.textTheme.titleSmall,
        ),
      ],
    );
  }
}

class _StarterGuidanceCard extends StatelessWidget {
  const _StarterGuidanceCard({required this.totalBuildings});

  final int totalBuildings;

  String get _message {
    if (totalBuildings == 0) return 'Buy your first building to get started.';
    if (totalBuildings == 1) return 'Add a second building to start building a supply chain.';
    return 'Review your ledger and buildings tab regularly to keep growing.';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Next steps', style: theme.textTheme.titleMedium),
            const SizedBox(height: 8),
            Text(_message),
          ],
        ),
      ),
    );
  }
}
