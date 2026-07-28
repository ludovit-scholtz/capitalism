// Ported from `projects/frontend/src/views/CompanyContractsView.vue` and
// `CompanyResearchView.vue`.
//
// The Ledger screen (`LedgerView.vue`) lives in its own `ledger_screen.dart`
// and Company Settings (`CompanySettingsView.vue`) in its own
// `company_settings_screen.dart` — both large enough to warrant the same
// dedicated-file treatment as the City Economy/Market tabs.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'company_models.dart';
import 'company_service.dart';

export 'company_settings_screen.dart' show CompanySettingsScreen;
export 'ledger_screen.dart' show LedgerScreen;

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
