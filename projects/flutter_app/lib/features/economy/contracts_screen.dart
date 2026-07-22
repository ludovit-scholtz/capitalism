// Ported from `projects/frontend/src/views/ContractsView.vue`. Mirrors the
// web's free-text UUID inputs for seller building unit / resource / product
// type rather than adding dropdown lookups the web itself doesn't have.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'contracts_models.dart';
import 'contracts_service.dart';

class ContractsScreen extends StatefulWidget {
  const ContractsScreen({super.key, GraphQlService? graphQlService, ContractsService? contractsService})
    : _injectedGraphQlService = graphQlService,
      _injectedContractsService = contractsService;

  final GraphQlService? _injectedGraphQlService;
  final ContractsService? _injectedContractsService;

  @override
  State<ContractsScreen> createState() => _ContractsScreenState();
}

class _ContractsScreenState extends State<ContractsScreen> {
  late final ContractsService _service;

  bool _loading = true;
  String? _error;
  List<SupplyContract> _contracts = const [];
  List<ContractCompanyOption> _myCompanies = const [];
  List<ContractCompanyOption> _allCompanies = const [];
  final Set<String> _actionLoadingIds = {};

  final _formKey = GlobalKey<FormState>();
  String? _sellerCompanyId;
  String? _buyerCompanyId;
  final _sellerUnitController = TextEditingController();
  final _resourceTypeController = TextEditingController();
  final _productTypeController = TextEditingController();
  final _quantityController = TextEditingController(text: '100');
  final _priceController = TextEditingController(text: '10');
  int _durationTicks = 100;
  final _penaltyController = TextEditingController(text: '10');
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedContractsService ?? ContractsService(graphQlService);
    _load();
  }

  @override
  void dispose() {
    _sellerUnitController.dispose();
    _resourceTypeController.dispose();
    _productTypeController.dispose();
    _quantityController.dispose();
    _priceController.dispose();
    _penaltyController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([_service.fetchContracts(), _service.fetchMyCompanies(), _service.fetchAllCompanies()]);
      if (!mounted) return;
      final myCompanies = results[1] as List<ContractCompanyOption>;
      setState(() {
        _contracts = results[0] as List<SupplyContract>;
        _myCompanies = myCompanies;
        _allCompanies = results[2] as List<ContractCompanyOption>;
        _sellerCompanyId ??= myCompanies.isNotEmpty ? myCompanies.first.id : null;
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

  bool get _canSubmit {
    final hasResource = _resourceTypeController.text.trim().isNotEmpty;
    final hasProduct = _productTypeController.text.trim().isNotEmpty;
    final hasExactlyOneItem = hasResource != hasProduct;
    return _sellerCompanyId != null &&
        _buyerCompanyId != null &&
        _sellerUnitController.text.trim().isNotEmpty &&
        hasExactlyOneItem;
  }

  Future<void> _proposeContract() async {
    if (!_canSubmit) return;
    setState(() => _submitting = true);
    try {
      await _service.proposeContract(
        sellerCompanyId: _sellerCompanyId!,
        buyerCompanyId: _buyerCompanyId!,
        sellerBuildingUnitId: _sellerUnitController.text.trim(),
        resourceTypeId: _resourceTypeController.text.trim().isEmpty ? null : _resourceTypeController.text.trim(),
        productTypeId: _productTypeController.text.trim().isEmpty ? null : _productTypeController.text.trim(),
        quantityPerTick: double.tryParse(_quantityController.text) ?? 0,
        pricePerUnit: double.tryParse(_priceController.text) ?? 0,
        durationTicks: _durationTicks,
        penaltyRatePercent: double.tryParse(_penaltyController.text) ?? 0,
      );
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not create the contract offer.')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  Future<void> _runAction(String action, String id) async {
    setState(() => _actionLoadingIds.add(id));
    try {
      switch (action) {
        case 'accept':
          await _service.acceptContract(id);
        case 'reject':
          await _service.rejectContract(id);
        case 'cancel':
          await _service.cancelContract(id);
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Action failed. Please try again.')));
      }
    } finally {
      if (mounted) setState(() => _actionLoadingIds.remove(id));
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _load, child: const Text('Try again')),
            ],
          ),
        ),
      );
    }

    final pending = _contracts.where((c) => c.status == 'PENDING').toList();
    final active = _contracts.where((c) => c.status == 'ACTIVE').toList();
    final history = _contracts.where((c) => c.status != 'PENDING' && c.status != 'ACTIVE').toList();

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Contracts', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text('Long-term supply agreements between companies.', style: Theme.of(context).textTheme.bodyMedium),
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Create contract offer', style: Theme.of(context).textTheme.titleMedium),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      key: const ValueKey('seller-company'),
                      initialValue: _sellerCompanyId,
                      decoration: const InputDecoration(labelText: 'Seller company'),
                      items: [
                        for (final company in _myCompanies) DropdownMenuItem(value: company.id, child: Text(company.name)),
                      ],
                      onChanged: (value) => setState(() => _sellerCompanyId = value),
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      key: const ValueKey('buyer-company'),
                      initialValue: _buyerCompanyId,
                      decoration: const InputDecoration(labelText: 'Buyer company'),
                      items: [
                        for (final company in _allCompanies) DropdownMenuItem(value: company.id, child: Text(company.name)),
                      ],
                      onChanged: (value) => setState(() => _buyerCompanyId = value),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      key: const ValueKey('seller-unit'),
                      controller: _sellerUnitController,
                      decoration: const InputDecoration(labelText: 'Seller B2B sales unit ID'),
                      onChanged: (_) => setState(() {}),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      key: const ValueKey('resource-type'),
                      controller: _resourceTypeController,
                      decoration: const InputDecoration(labelText: 'Resource type ID (or product below)'),
                      onChanged: (_) => setState(() {}),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      key: const ValueKey('product-type'),
                      controller: _productTypeController,
                      decoration: const InputDecoration(labelText: 'Product type ID (or resource above)'),
                      onChanged: (_) => setState(() {}),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _quantityController,
                      decoration: const InputDecoration(labelText: 'Quantity per tick'),
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _priceController,
                      decoration: const InputDecoration(labelText: 'Price per unit'),
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<int>(
                      initialValue: _durationTicks,
                      decoration: const InputDecoration(labelText: 'Duration (ticks)'),
                      items: [
                        for (final duration in contractDurationOptions)
                          DropdownMenuItem(value: duration, child: Text('$duration')),
                      ],
                      onChanged: (value) => setState(() => _durationTicks = value ?? _durationTicks),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _penaltyController,
                      decoration: const InputDecoration(labelText: 'Penalty rate (%)'),
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    ),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: (!_canSubmit || _submitting) ? null : _proposeContract,
                      child: Text(_submitting ? 'Creating…' : 'Create offer'),
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(height: 24),
          _ContractColumn(
            title: 'Pending',
            emptyLabel: 'No pending offers.',
            contracts: pending,
            actionLoadingIds: _actionLoadingIds,
            onAction: _runAction,
          ),
          const SizedBox(height: 24),
          _ContractColumn(
            title: 'Active',
            emptyLabel: 'No active contracts.',
            contracts: active,
            actionLoadingIds: _actionLoadingIds,
            onAction: _runAction,
          ),
          const SizedBox(height: 24),
          _ContractColumn(
            title: 'History',
            emptyLabel: 'No contract history yet.',
            contracts: history,
            actionLoadingIds: _actionLoadingIds,
            onAction: _runAction,
          ),
        ],
      ),
    );
  }
}

class _ContractColumn extends StatelessWidget {
  const _ContractColumn({
    required this.title,
    required this.emptyLabel,
    required this.contracts,
    required this.actionLoadingIds,
    required this.onAction,
  });

  final String title;
  final String emptyLabel;
  final List<SupplyContract> contracts;
  final Set<String> actionLoadingIds;
  final Future<void> Function(String action, String id) onAction;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 8),
        if (contracts.isEmpty)
          Text(emptyLabel, style: Theme.of(context).textTheme.bodyMedium)
        else
          for (final contract in contracts)
            _ContractCard(
              key: ValueKey('contract-${contract.id}'),
              contract: contract,
              status: title,
              busy: actionLoadingIds.contains(contract.id),
              onAction: onAction,
            ),
      ],
    );
  }
}

class _ContractCard extends StatelessWidget {
  const _ContractCard({super.key, required this.contract, required this.status, required this.busy, required this.onAction});

  final SupplyContract contract;
  final String status;
  final bool busy;
  final Future<void> Function(String action, String id) onAction;

  Color _healthColor(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    switch (contract.healthBadge) {
      case 'error':
        return scheme.error;
      case 'warning':
        return scheme.tertiary;
      default:
        return scheme.primary;
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(contract.itemName, style: theme.textTheme.titleSmall),
            Text(
              '${contract.sellerCompanyName} → ${contract.buyerCompanyName}',
              style: theme.textTheme.bodySmall,
            ),
            if (status == 'Pending')
              Text('${contract.pricePerUnit.toStringAsFixed(2)} ${contract.currencyCode} / ${contract.quantityPerTick}'),
            if (status == 'Active') ...[
              Text('${contract.remainingTicks} ticks remaining'),
              Text('Delivered: ${contract.totalDeliveredQuantity.toStringAsFixed(1)}'),
              const SizedBox(height: 4),
              Chip(
                label: Text(contract.healthBadge.toUpperCase()),
                backgroundColor: _healthColor(context).withValues(alpha: 0.15),
                labelStyle: TextStyle(color: _healthColor(context)),
              ),
            ],
            if (status == 'History') ...[
              Text(contract.status),
              Text('Delivered: ${contract.totalDeliveredQuantity.toStringAsFixed(1)}'),
              Text('Penalties: ${contract.totalPenaltyAmount.toStringAsFixed(2)} ${contract.currencyCode}'),
            ],
            const SizedBox(height: 8),
            if (status == 'Pending')
              Row(
                children: [
                  FilledButton(
                    onPressed: busy ? null : () => onAction('accept', contract.id),
                    child: const Text('Accept'),
                  ),
                  const SizedBox(width: 8),
                  OutlinedButton(
                    onPressed: busy ? null : () => onAction('reject', contract.id),
                    child: const Text('Reject'),
                  ),
                ],
              ),
            if (status == 'Active')
              OutlinedButton(
                onPressed: busy ? null : () => onAction('cancel', contract.id),
                child: const Text('Cancel'),
              ),
          ],
        ),
      ),
    );
  }
}
