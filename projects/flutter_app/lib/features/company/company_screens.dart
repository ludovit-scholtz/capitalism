// Ported from `projects/frontend/src/views/LedgerView.vue`,
// `CompanyContractsView.vue`, `CompanySettingsView.vue`, and
// `CompanyResearchView.vue`.
//
// Ledger is the one screen with a real trim here: the web delegates almost
// all rendering to `LedgerMainContent.vue` (1002 lines), which adds
// per-category drill-down rows (`ledgerDrillDown` query), cross-city
// shipment tracking, a city-unlock expansion panel, and multi-year history
// tables. This port covers the core P&L summary and per-city financial
// breakdown only — documented, not an oversight.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'company_models.dart';
import 'company_service.dart';

class LedgerScreen extends StatefulWidget {
  const LedgerScreen({super.key, required this.companyId, GraphQlService? graphQlService, CompanyService? companyService})
    : _injectedGraphQlService = graphQlService,
      _injectedCompanyService = companyService;

  final String companyId;
  final GraphQlService? _injectedGraphQlService;
  final CompanyService? _injectedCompanyService;

  @override
  State<LedgerScreen> createState() => _LedgerScreenState();
}

class _LedgerScreenState extends State<LedgerScreen> {
  late final CompanyService _service;

  bool _loading = true;
  String? _error;
  CompanyLedger? _ledger;
  List<CityFinancialBreakdown> _cityBreakdown = const [];

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedCompanyService ?? CompanyService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final (ledger, breakdown) = await _service.fetchLedger(widget.companyId);
      if (!mounted) return;
      setState(() {
        _ledger = ledger;
        _cityBreakdown = breakdown;
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

    final ledger = _ledger!;
    final theme = Theme.of(context);
    String money(double value) => '${value.toStringAsFixed(0)} ${ledger.primaryCurrencyCode}';

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('${ledger.companyName} Ledger', style: theme.textTheme.headlineSmall),
          Text('Game year ${ledger.gameYear}', style: theme.textTheme.bodyMedium),
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Profit & Loss', style: theme.textTheme.titleMedium),
                  const SizedBox(height: 8),
                  _LedgerLine(label: 'Revenue', value: money(ledger.totalRevenue)),
                  _LedgerLine(label: 'Purchasing', value: '-${money(ledger.totalPurchasingCosts)}'),
                  _LedgerLine(label: 'Shipping', value: '-${money(ledger.totalShippingCosts)}'),
                  _LedgerLine(label: 'Labor', value: '-${money(ledger.totalLaborCosts)}'),
                  _LedgerLine(label: 'Energy', value: '-${money(ledger.totalEnergyCosts)}'),
                  _LedgerLine(label: 'Marketing', value: '-${money(ledger.totalMarketingCosts)}'),
                  _LedgerLine(label: 'Tax', value: '-${money(ledger.totalTaxPaid)}'),
                  _LedgerLine(label: 'Other', value: '-${money(ledger.totalOtherCosts)}'),
                  const Divider(),
                  _LedgerLine(label: 'Net income', value: money(ledger.netIncome), bold: true),
                  _LedgerLine(label: 'Cash on hand', value: money(ledger.currentCash)),
                  _LedgerLine(label: 'Total assets', value: money(ledger.totalAssets)),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Text('By city', style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          if (_cityBreakdown.isEmpty)
            const Text('No city-level activity recorded yet.')
          else
            for (final city in _cityBreakdown)
              Card(
                margin: const EdgeInsets.only(bottom: 8),
                child: ListTile(
                  title: Text(city.cityName),
                  subtitle: Text('Revenue ${city.revenue.toStringAsFixed(0)} · Costs ${city.costs.toStringAsFixed(0)}'),
                  trailing: Text('${city.profit.toStringAsFixed(0)} ${city.currencyCode}'),
                ),
              ),
        ],
      ),
    );
  }
}

class _LedgerLine extends StatelessWidget {
  const _LedgerLine({required this.label, required this.value, this.bold = false});

  final String label;
  final String value;
  final bool bold;

  @override
  Widget build(BuildContext context) {
    final style = bold ? Theme.of(context).textTheme.titleSmall : Theme.of(context).textTheme.bodyMedium;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [Text(label, style: style), Text(value, style: style)],
      ),
    );
  }
}

class CompanyContractsScreen extends StatefulWidget {
  const CompanyContractsScreen({super.key, required this.companyId, GraphQlService? graphQlService, CompanyService? companyService})
    : _injectedGraphQlService = graphQlService,
      _injectedCompanyService = companyService;

  final String companyId;
  final GraphQlService? _injectedGraphQlService;
  final CompanyService? _injectedCompanyService;

  @override
  State<CompanyContractsScreen> createState() => _CompanyContractsScreenState();
}

class _CompanyContractsScreenState extends State<CompanyContractsScreen> {
  late final CompanyService _service;

  bool _loading = true;
  String? _error;
  List<CompanyContractCard> _contracts = const [];
  List<ContractBid> _bids = const [];
  final Map<String, TextEditingController> _quantityControllers = {};
  final Set<String> _shippingIds = {};

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedCompanyService ?? CompanyService(graphQlService);
    _load();
  }

  @override
  void dispose() {
    for (final controller in _quantityControllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final (contracts, bids) = await _service.fetchCompanyContracts(widget.companyId);
      if (!mounted) return;
      setState(() {
        _contracts = contracts;
        _bids = bids;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load contracts. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _ship(CompanyContractCard contract) async {
    final controller = _quantityControllers.putIfAbsent(contract.id, () => TextEditingController());
    final quantity = double.tryParse(controller.text) ?? 0;
    if (quantity <= 0) return;
    setState(() => _shippingIds.add(contract.id));
    try {
      await _service.fulfillShipment(contractId: contract.id, quantity: quantity);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not ship this delivery.')));
      }
    } finally {
      if (mounted) setState(() => _shippingIds.remove(contract.id));
    }
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

    final theme = Theme.of(context);
    final awarded = _contracts.where((c) => c.status == 'AWARDED').toList();
    final other = _contracts.where((c) => c.status != 'AWARDED').toList();

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Company Contracts', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 16),
        if (awarded.isNotEmpty) ...[
          Text('Awarded — ready to ship', style: theme.textTheme.titleMedium),
          for (final contract in awarded)
            Card(
              key: ValueKey('contract-${contract.id}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(contract.title, style: theme.textTheme.titleSmall),
                    Text('${contract.productName} · ${(contract.fulfillmentPercent ?? 0).toStringAsFixed(0)}% fulfilled'),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        Expanded(
                          child: TextField(
                            controller: _quantityControllers.putIfAbsent(contract.id, () => TextEditingController()),
                            decoration: const InputDecoration(labelText: 'Quantity to ship'),
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                          ),
                        ),
                        const SizedBox(width: 8),
                        FilledButton(
                          onPressed: _shippingIds.contains(contract.id) ? null : () => _ship(contract),
                          child: const Text('Ship'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          const SizedBox(height: 16),
        ],
        Text('Other contracts', style: theme.textTheme.titleMedium),
        if (other.isEmpty)
          const Text('No other contracts.')
        else
          for (final contract in other)
            ListTile(
              title: Text(contract.title),
              subtitle: Text(contract.status),
            ),
        const SizedBox(height: 16),
        Text('Bid history', style: theme.textTheme.titleMedium),
        if (_bids.isEmpty)
          const Text('No bids submitted yet.')
        else
          for (final bid in _bids)
            ListTile(dense: true, title: Text('Bid ${bid.bidPricePerUnit.toStringAsFixed(2)} per unit'), trailing: Text(bid.contractStatus)),
      ],
    );
  }
}

class CompanySettingsScreen extends StatefulWidget {
  const CompanySettingsScreen({super.key, required this.companyId, GraphQlService? graphQlService, CompanyService? companyService})
    : _injectedGraphQlService = graphQlService,
      _injectedCompanyService = companyService;

  final String companyId;
  final GraphQlService? _injectedGraphQlService;
  final CompanyService? _injectedCompanyService;

  @override
  State<CompanySettingsScreen> createState() => _CompanySettingsScreenState();
}

class _CompanySettingsScreenState extends State<CompanySettingsScreen> {
  late final CompanyService _service;

  bool _loading = true;
  String? _error;
  CompanySettings? _settings;
  final _nameController = TextEditingController();
  final _dividendController = TextEditingController();
  final Map<String, double> _salaryMultipliers = {};
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedCompanyService ?? CompanyService(graphQlService);
    _load();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _dividendController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final settings = await _service.fetchCompanySettings(widget.companyId);
      if (!mounted) return;
      setState(() {
        _settings = settings;
        _nameController.text = settings.companyName;
        _dividendController.text = settings.dividendPayoutRatio.toStringAsFixed(2);
        _salaryMultipliers.clear();
        for (final city in settings.citySalarySettings) {
          _salaryMultipliers[city.cityId] = city.salaryMultiplier;
        }
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load company settings. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _save() async {
    setState(() => _saving = true);
    try {
      await _service.updateCompanySettings(
        companyId: widget.companyId,
        name: _nameController.text.trim(),
        dividendPayoutRatio: double.tryParse(_dividendController.text) ?? 0,
        citySalarySettings: [for (final entry in _salaryMultipliers.entries) {'cityId': entry.key, 'salaryMultiplier': entry.value}],
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Settings saved.')));
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not save settings.')));
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _voteDividend(bool approve) async {
    try {
      await _service.voteDividend(companyId: widget.companyId, approve: approve);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not record your vote.')));
      }
    }
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

    final settings = _settings!;
    final theme = Theme.of(context);
    final proposal = settings.pendingDividendProposal;

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Company Settings', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 16),
        TextField(controller: _nameController, decoration: const InputDecoration(labelText: 'Company name')),
        const SizedBox(height: 8),
        TextField(
          controller: _dividendController,
          decoration: const InputDecoration(labelText: 'Dividend payout ratio (0-1)'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
        ),
        const SizedBox(height: 16),
        Text('Administration', style: theme.textTheme.titleMedium),
        Text('Overhead rate: ${(settings.administrationOverheadRate * 100).toStringAsFixed(1)}%'),
        Text('Age factor: ${settings.ageFactor.toStringAsFixed(2)} · Asset factor: ${settings.assetFactor.toStringAsFixed(2)}'),
        const SizedBox(height: 16),
        Text('Salaries by city', style: theme.textTheme.titleMedium),
        for (final city in settings.citySalarySettings)
          Row(
            children: [
              Expanded(child: Text(city.cityName)),
              SizedBox(
                width: 120,
                child: Slider(
                  value: (_salaryMultipliers[city.cityId] ?? city.salaryMultiplier).clamp(0.5, 2.0),
                  min: 0.5,
                  max: 2.0,
                  divisions: 15,
                  label: (_salaryMultipliers[city.cityId] ?? city.salaryMultiplier).toStringAsFixed(2),
                  onChanged: (value) => setState(() => _salaryMultipliers[city.cityId] = value),
                ),
              ),
            ],
          ),
        const SizedBox(height: 16),
        FilledButton(onPressed: _saving ? null : _save, child: Text(_saving ? 'Saving…' : 'Save changes')),
        const SizedBox(height: 24),
        Text('Dividend', style: theme.textTheme.titleMedium),
        if (proposal == null)
          const Text('No pending dividend proposal.')
        else
          Card(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Proposed ${proposal.dividendPercent.toStringAsFixed(1)}% — ${proposal.ticksRemaining} ticks left'),
                  Text('For: ${proposal.forVotes} · Against: ${proposal.againstVotes}'),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      FilledButton(onPressed: () => _voteDividend(true), child: const Text('Approve')),
                      const SizedBox(width: 8),
                      OutlinedButton(onPressed: () => _voteDividend(false), child: const Text('Reject')),
                    ],
                  ),
                ],
              ),
            ),
          ),
      ],
    );
  }
}

class CompanyResearchScreen extends StatefulWidget {
  const CompanyResearchScreen({super.key, required this.companyId, GraphQlService? graphQlService, CompanyService? companyService})
    : _injectedGraphQlService = graphQlService,
      _injectedCompanyService = companyService;

  final String companyId;
  final GraphQlService? _injectedGraphQlService;
  final CompanyService? _injectedCompanyService;

  @override
  State<CompanyResearchScreen> createState() => _CompanyResearchScreenState();
}

class _CompanyResearchScreenState extends State<CompanyResearchScreen> {
  late final CompanyService _service;

  bool _loading = true;
  String? _error;
  BrandQualityOverview? _overview;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedCompanyService ?? CompanyService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final overview = await _service.fetchBrandQualityOverview(widget.companyId);
      if (!mounted) return;
      setState(() {
        _overview = overview;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load research data. Please try again.';
        _loading = false;
      });
    }
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

    final overview = _overview!;
    final theme = Theme.of(context);

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Research & Brand Quality', style: theme.textTheme.headlineSmall),
          Text('Total research budget: ${overview.totalResearchBudgetUsd.toStringAsFixed(0)} USD', style: theme.textTheme.bodyMedium),
          const SizedBox(height: 16),
          if (overview.brands.isEmpty)
            const Text('No brands with research data yet.')
          else
            for (final brand in overview.brands)
              Card(
                margin: const EdgeInsets.only(bottom: 8),
                child: ListTile(
                  title: Text(brand.name),
                  subtitle: Text(brand.productName ?? 'Company-wide'),
                  trailing: Text('${(brand.combinedBrandQuality * 100).round()}%'),
                ),
              ),
        ],
      ),
    );
  }
}
