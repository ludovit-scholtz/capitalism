// Ported from `projects/frontend/src/views/BankLoanRequestView.vue`.
// Simplified from the web's 4-step wizard into one flat form, but the real
// `acceptLoan` contract — including `durationTicks` (default 8760, matching
// the web's `durationTicks` ref) — is fully wired.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'banking_models.dart';
import 'banking_service.dart';

class BankLoanRequestScreen extends StatefulWidget {
  const BankLoanRequestScreen({
    super.key,
    required this.bankBuildingId,
    GraphQlService? graphQlService,
    BankingService? bankingService,
  }) : _injectedGraphQlService = graphQlService,
       _injectedBankingService = bankingService;

  final String bankBuildingId;
  final GraphQlService? _injectedGraphQlService;
  final BankingService? _injectedBankingService;

  @override
  State<BankLoanRequestScreen> createState() => _BankLoanRequestScreenState();
}

class _BankLoanRequestScreenState extends State<BankLoanRequestScreen> {
  late final BankingService _service;

  bool _loading = true;
  String? _error;
  BankInfo? _bankInfo;
  List<Map<String, String>> _myCompanies = const [];
  List<CollateralBuilding> _collateralBuildings = const [];
  List<Map<String, String>> _bankAccounts = const [];

  String? _companyId;
  String? _collateralBuildingId;
  String? _bankAccountId;
  final _principalController = TextEditingController();
  final _durationController = TextEditingController(text: '8760');
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedBankingService ?? BankingService(graphQlService);
    _load();
  }

  @override
  void dispose() {
    _principalController.dispose();
    _durationController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        _service.fetchBankInfo(widget.bankBuildingId),
        _service.fetchMyCompanies(),
        _service.fetchMyCollateralBuildings(bankBuildingId: widget.bankBuildingId),
      ]);
      if (!mounted) return;
      final companies = results[1] as List<Map<String, String>>;
      final companyId = companies.isNotEmpty ? companies.first['id'] : null;
      List<Map<String, String>> accounts = const [];
      if (companyId != null) {
        accounts = await _service.fetchCompanyBankAccounts(companyId);
      }
      if (!mounted) return;
      setState(() {
        _bankInfo = results[0] as BankInfo;
        _myCompanies = companies;
        _collateralBuildings = results[2] as List<CollateralBuilding>;
        _companyId = companyId;
        _bankAccounts = accounts;
        _bankAccountId = accounts.isNotEmpty ? accounts.first['id'] : null;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load loan options. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _submit() async {
    if (_companyId == null) return;
    final durationTicks = int.tryParse(_durationController.text)?.clamp(1, 87600) ?? 8760;
    setState(() => _submitting = true);
    try {
      await _service.acceptLoan(
        bankBuildingId: widget.bankBuildingId,
        borrowerCompanyId: _companyId!,
        principalAmount: double.tryParse(_principalController.text) ?? 0,
        durationTicks: durationTicks,
        collateralBuildingId: _collateralBuildingId,
        bankAccountId: _bankAccountId,
      );
      if (mounted) context.go('/banking');
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not accept the loan.')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
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

    final bankInfo = _bankInfo!;
    final theme = Theme.of(context);

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Request a loan from ${bankInfo.bankBuildingName}', style: theme.textTheme.headlineSmall),
        Text('Lending rate: ${bankInfo.lendingInterestRatePercent.toStringAsFixed(1)}%', style: theme.textTheme.bodyMedium),
        const SizedBox(height: 16),
        DropdownButtonFormField<String>(
          initialValue: _companyId,
          decoration: const InputDecoration(labelText: 'Borrowing company'),
          items: [for (final company in _myCompanies) DropdownMenuItem(value: company['id'], child: Text(company['name']!))],
          onChanged: (value) => setState(() => _companyId = value),
        ),
        TextField(controller: _principalController, decoration: const InputDecoration(labelText: 'Principal amount'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
        TextField(
          controller: _durationController,
          decoration: const InputDecoration(labelText: 'Duration (ticks, 1–87600)'),
          keyboardType: TextInputType.number,
        ),
        DropdownButtonFormField<String?>(
          initialValue: _collateralBuildingId,
          decoration: const InputDecoration(labelText: 'Collateral (optional)'),
          items: [
            const DropdownMenuItem(value: null, child: Text('No collateral')),
            for (final building in _collateralBuildings.where((b) => b.isEligible))
              DropdownMenuItem(value: building.buildingId, child: Text('${building.buildingName} (up to ${building.remainingBorrowingCapacity.toStringAsFixed(0)})')),
          ],
          onChanged: (value) => setState(() => _collateralBuildingId = value),
        ),
        DropdownButtonFormField<String?>(
          initialValue: _bankAccountId,
          decoration: const InputDecoration(labelText: 'Settlement account'),
          items: [for (final account in _bankAccounts) DropdownMenuItem(value: account['id'], child: Text(account['currencyCode']!))],
          onChanged: (value) => setState(() => _bankAccountId = value),
        ),
        const SizedBox(height: 16),
        FilledButton(onPressed: (_submitting || _companyId == null) ? null : _submit, child: Text(_submitting ? 'Submitting…' : 'Accept loan')),
      ],
    );
  }
}
