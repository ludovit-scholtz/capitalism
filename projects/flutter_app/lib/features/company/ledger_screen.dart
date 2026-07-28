// Ported from `projects/frontend/src/views/LedgerView.vue` and
// `projects/frontend/src/components/ledger/LedgerMainContent.vue` — the
// full company ledger: KPI row, income-tax schedule, multi-year history
// selector, city-unlock progress, income statement/balance sheet/cash flow
// with per-category drill-down, cross-city shipment tracking, per-city
// financial breakdown, and per-building performance. Trimmed from the web:
// no scroll-position preservation around tick-driven refreshes (the web's
// `useScrollPreservation` composable has no direct Flutter analogue; a
// pull-to-refresh `RefreshIndicator` is used instead of automatic
// tick-based polling).

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import 'company_models.dart';
import 'ledger_buildings_panel.dart';
import 'ledger_city_breakdown_panel.dart';
import 'ledger_city_unlock_panel.dart';
import 'ledger_drill_panel.dart';
import 'ledger_logistics_panel.dart';
import 'ledger_models.dart';
import 'ledger_service.dart';
import 'ledger_statement_cards.dart';

class LedgerScreen extends StatefulWidget {
  const LedgerScreen({super.key, required this.companyId, GraphQlService? graphQlService, LedgerService? ledgerService})
    : _injectedGraphQlService = graphQlService,
      _injectedLedgerService = ledgerService;

  final String companyId;
  final GraphQlService? _injectedGraphQlService;
  final LedgerService? _injectedLedgerService;

  @override
  State<LedgerScreen> createState() => _LedgerScreenState();
}

class _LedgerScreenState extends State<LedgerScreen> {
  late final LedgerService _service;

  bool _loading = true;
  String? _error;
  LedgerPageData? _page;
  String? _drillCategory;
  List<LedgerEntryResult> _drillEntries = const [];
  bool _drillLoading = false;
  int? _selectedGameYear;

  int? get _resolvedGameYear => _selectedGameYear ?? _page?.ledger?.gameYear;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedLedgerService ?? LedgerService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final page = await _service.fetchLedgerPage(widget.companyId, gameYear: _selectedGameYear);
      if (!mounted) return;
      if (page.ledger == null) {
        setState(() {
          _error = 'Ledger not found.';
          _loading = false;
        });
        return;
      }
      setState(() {
        _page = page;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the ledger. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _loadDrillEntries(String category) async {
    setState(() => _drillLoading = true);
    try {
      final entries = await _service.fetchDrillDown(widget.companyId, category: category, gameYear: _resolvedGameYear);
      if (!mounted) return;
      setState(() {
        _drillEntries = entries;
        _drillLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _drillEntries = const [];
        _drillLoading = false;
      });
    }
  }

  void _toggleDrill(String category) {
    if (_drillCategory == category) {
      setState(() {
        _drillCategory = null;
        _drillEntries = const [];
      });
      return;
    }
    setState(() => _drillCategory = category);
    _loadDrillEntries(category);
  }

  Future<void> _selectGameYear(int? gameYear) async {
    setState(() {
      _selectedGameYear = gameYear;
      _drillCategory = null;
      _drillEntries = const [];
    });
    await _load();
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [Text(_error!), const SizedBox(height: 12), OutlinedButton(onPressed: _load, child: const Text('Try again'))],
          ),
        ),
      );
    }

    final page = _page!;
    final ledger = page.ledger!;
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text(ledger.companyName, style: theme.textTheme.headlineSmall),
          Text('Game year ${ledger.gameYear}', style: theme.textTheme.bodyMedium),
          const SizedBox(height: 16),
          _KpiRow(ledger: ledger, languageCode: languageCode),
          const SizedBox(height: 16),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Card(
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('🧾 Income tax schedule', style: theme.textTheme.titleSmall),
                        const SizedBox(height: 4),
                        Text(
                          ledger.isIncomeTaxSettled ? 'Income tax settled for year ${ledger.incomeTaxDueGameYear}.' : 'Income tax due for year ${ledger.incomeTaxDueGameYear}.',
                          style: theme.textTheme.bodySmall,
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 12),
              if (ledger.history.length > 1)
                Expanded(
                  child: Card(
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('🗂️ History', style: theme.textTheme.titleSmall),
                          const SizedBox(height: 4),
                          Wrap(
                            spacing: 6,
                            runSpacing: 6,
                            children: [
                              for (final yearItem in ledger.history)
                                ChoiceChip(
                                  key: ValueKey('history-year-${yearItem.gameYear}'),
                                  label: Text('Y${yearItem.gameYear}'),
                                  selected: yearItem.gameYear == _resolvedGameYear,
                                  onSelected: (_) => _selectGameYear(yearItem.isCurrentGameYear ? null : yearItem.gameYear),
                                ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 16),
          LedgerCityUnlockPanel(statuses: page.cityUnlockStatuses),
          if (page.cityUnlockStatuses.isNotEmpty) const SizedBox(height: 16),
          if (!ledger.isCurrentGameYear)
            Padding(
              padding: const EdgeInsets.only(bottom: 16),
              child: Card(
                color: theme.colorScheme.surfaceContainerHighest,
                child: const Padding(padding: EdgeInsets.all(12), child: Text('🕰️ You are viewing a past game year.')),
              ),
            ),
          LedgerStatementsGrid(ledger: ledger, activeCategory: _drillCategory, onDrillToggle: _toggleDrill),
          const SizedBox(height: 16),
          LedgerLogisticsPanel(shipments: page.logisticsShipments, currentTick: page.currentTick),
          if (page.cityFinancialBreakdown.isNotEmpty) ...[
            const SizedBox(height: 16),
            LedgerCityBreakdownPanel(breakdown: page.cityFinancialBreakdown),
          ],
          if (_drillCategory != null) ...[
            const SizedBox(height: 16),
            LedgerDrillPanel(
              category: _drillCategory!,
              entries: _drillEntries,
              loading: _drillLoading,
              onClose: () => _toggleDrill(_drillCategory!),
            ),
          ],
          if (ledger.buildingSummaries.isNotEmpty) ...[
            const SizedBox(height: 16),
            LedgerBuildingsPanel(buildings: ledger.buildingSummaries),
          ],
          const SizedBox(height: 24),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  const Text('🏆', style: TextStyle(fontSize: 28)),
                  const SizedBox(width: 12),
                  const Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Race to the Top', style: TextStyle(fontWeight: FontWeight.bold)),
                        Text('See how you rank against other players.'),
                      ],
                    ),
                  ),
                  OutlinedButton(onPressed: () => context.go('/personal-ledger'), child: const Text('View')),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _KpiRow extends StatelessWidget {
  const _KpiRow({required this.ledger, required this.languageCode});

  final CompanyLedger ledger;
  final String languageCode;

  @override
  Widget build(BuildContext context) {
    final code = ledger.primaryCurrencyCode;
    String money(double value) => AppNumberFormat.money(value, currencyCode: code, languageCode: languageCode);

    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: [
        _kpi(context, 'Cash', money(ledger.currentCash)),
        _kpi(context, 'Net income', money(ledger.netIncome)),
        _kpi(context, 'Taxable income', money(ledger.taxableIncome)),
        _kpi(context, 'Estimated income tax', money(-ledger.estimatedIncomeTax)),
        _kpi(context, 'Total assets', money(ledger.totalAssets)),
        _kpi(context, 'Currency', ledger.hasMixedCurrencies ? '$code (mixed)' : code),
      ],
    );
  }

  Widget _kpi(BuildContext context, String label, String value) {
    final theme = Theme.of(context);
    return SizedBox(
      width: 160,
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label, style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
              const SizedBox(height: 4),
              Text(value, style: theme.textTheme.titleMedium),
            ],
          ),
        ),
      ),
    );
  }
}
