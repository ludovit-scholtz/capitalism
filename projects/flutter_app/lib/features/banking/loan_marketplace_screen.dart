// Ported from `projects/frontend/src/views/LoanMarketplaceView.vue`.
//
// Borrow tab: active/overdue loans (with a repay-in-full action for
// OVERDUE/DEFAULTED loans via `repayLoanDebt`) and a bank list restricted to
// `baseCapitalDeposited` banks, sorted by lending rate ascending — matching
// the web's implicit filter/sort there. Deposit tab: accounts (with a
// withdraw dialog supporting partial or full closure, not just zero-balance
// closes) and the full bank list with a city filter, "available only"
// (base-capital-deposited) checkbox, and a 4-way sort toggle
// (deposit/lending/capacity/city), matching the web exactly.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'banking_models.dart';
import 'banking_service.dart';

enum _BankSort { depositRate, lendingRate, capacity, city }

class LoanMarketplaceScreen extends StatefulWidget {
  const LoanMarketplaceScreen({super.key, GraphQlService? graphQlService, BankingService? bankingService})
    : _injectedGraphQlService = graphQlService,
      _injectedBankingService = bankingService;

  final GraphQlService? _injectedGraphQlService;
  final BankingService? _injectedBankingService;

  @override
  State<LoanMarketplaceScreen> createState() => _LoanMarketplaceScreenState();
}

class _LoanMarketplaceScreenState extends State<LoanMarketplaceScreen> {
  late final BankingService _service;
  late final bool _isAuthenticated;

  String _tab = 'borrow';
  bool _loading = true;
  String? _error;
  List<BankListing> _banks = const [];
  List<LoanSummary> _myLoans = const [];
  List<PlayerBankAccount> _myAccounts = const [];
  List<Map<String, String>> _myCompanies = const [];

  String _cityFilter = 'ALL';
  bool _availableOnly = false;
  _BankSort _sortBy = _BankSort.depositRate;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    _isAuthenticated = auth.isAuthenticated;
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedBankingService ?? BankingService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final futures = <Future>[_service.fetchAllBanks()];
      if (_isAuthenticated) {
        futures.addAll([_service.fetchMyLoans(), _service.fetchMyBankAccounts(), _service.fetchMyCompanies()]);
      }
      final results = await Future.wait(futures);
      if (!mounted) return;
      setState(() {
        _banks = results[0] as List<BankListing>;
        if (_isAuthenticated) {
          _myLoans = results[1] as List<LoanSummary>;
          _myAccounts = results[2] as List<PlayerBankAccount>;
          _myCompanies = results[3] as List<Map<String, String>>;
        }
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load banking data. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _openDepositDialog(BankListing bank) async {
    if (_myCompanies.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('You need a company to open a deposit.')));
      return;
    }
    final amountController = TextEditingController(text: '1000');
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text('Deposit at ${bank.bankBuildingName}'),
        content: TextField(controller: amountController, decoration: const InputDecoration(labelText: 'Amount'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Deposit')),
        ],
      ),
    );
    if (confirmed != true) return;

    try {
      await _service.openBankAccount(bankBuildingId: bank.bankBuildingId, depositorCompanyId: _myCompanies.first['id'], amount: double.tryParse(amountController.text) ?? 0);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not open the deposit.')));
      }
    }
  }

  Future<void> _openWithdrawDialog(PlayerBankAccount account) async {
    final amountController = TextEditingController(text: account.balance.toStringAsFixed(0));
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Withdraw'),
        content: TextField(
          controller: amountController,
          decoration: InputDecoration(labelText: 'Amount (up to ${account.balance.toStringAsFixed(2)})'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Withdraw')),
        ],
      ),
    );
    if (confirmed != true) return;

    try {
      final amount = double.tryParse(amountController.text) ?? 0;
      if (account.isDepositAccount) {
        await _service.closeBankAccount(account.id, amount: amount);
      } else {
        await _service.closeCompanyBankAccount(account.id);
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not withdraw from this account.')));
      }
    }
  }

  Future<void> _repayLoan(LoanSummary loan) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Repay loan in full?'),
        content: Text('Pay off the remaining ${loan.remainingPrincipal.toStringAsFixed(0)} ${loan.loanCurrencyCode} immediately?'),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Repay')),
        ],
      ),
    );
    if (confirmed != true) return;

    try {
      await _service.repayLoanDebt(loanId: loan.id);
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Loan repaid.')));
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not repay this loan.')));
      }
    }
  }

  List<String> get _cityOptions => _banks.map((b) => b.cityName).where((c) => c.isNotEmpty).toSet().toList()..sort();

  List<BankListing> get _filteredSortedBanks {
    var banks = _banks.where((b) {
      if (_cityFilter != 'ALL' && b.cityName != _cityFilter) return false;
      if (_availableOnly && !b.baseCapitalDeposited) return false;
      return true;
    }).toList();
    switch (_sortBy) {
      case _BankSort.depositRate:
        banks.sort((a, b) => b.depositInterestRatePercent.compareTo(a.depositInterestRatePercent));
      case _BankSort.lendingRate:
        banks.sort((a, b) => a.lendingInterestRatePercent.compareTo(b.lendingInterestRatePercent));
      case _BankSort.capacity:
        banks.sort((a, b) => b.availableLendingCapacity.compareTo(a.availableLendingCapacity));
      case _BankSort.city:
        banks.sort((a, b) => a.cityName.compareTo(b.cityName));
    }
    return banks;
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

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Banking', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(child: ChoiceChip(label: const Text('Borrow'), selected: _tab == 'borrow', onSelected: (_) => setState(() => _tab = 'borrow'))),
            const SizedBox(width: 8),
            Expanded(child: ChoiceChip(label: const Text('Deposit'), selected: _tab == 'deposit', onSelected: (_) => setState(() => _tab = 'deposit'))),
          ],
        ),
        const SizedBox(height: 16),
        if (_tab == 'borrow') ..._buildBorrowTab() else ..._buildDepositTab(),
      ],
    );
  }

  List<Widget> _buildBorrowTab() {
    final eligibleBanks = _banks.where((b) => b.baseCapitalDeposited).toList()
      ..sort((a, b) => a.lendingInterestRatePercent.compareTo(b.lendingInterestRatePercent));
    return [
      if (_isAuthenticated) ...[
        Text('Your active loans', style: Theme.of(context).textTheme.titleMedium),
        if (_myLoans.isEmpty)
          const Text('You have no active loans.')
        else
          for (final loan in _myLoans)
            ListTile(
              key: ValueKey('my-loan-${loan.id}'),
              title: Text('${loan.bankBuildingName} · ${loan.remainingPrincipal.toStringAsFixed(0)} ${loan.loanCurrencyCode}'),
              subtitle: Text(loan.status),
              trailing: loan.isRepayable ? TextButton(onPressed: () => _repayLoan(loan), child: const Text('Repay now')) : null,
            ),
        const SizedBox(height: 16),
      ],
      Text('Banks', style: Theme.of(context).textTheme.titleMedium),
      if (eligibleBanks.isEmpty) const Text('No banks are open for lending yet.'),
      for (final bank in eligibleBanks)
        Card(
          key: ValueKey('bank-borrow-${bank.bankBuildingId}'),
          margin: const EdgeInsets.only(bottom: 8),
          child: ListTile(
            title: Text('${bank.bankBuildingName} (${bank.cityName})'),
            subtitle: Text('Lending rate ${bank.lendingInterestRatePercent.toStringAsFixed(1)}% · Capacity ${bank.availableLendingCapacity.toStringAsFixed(0)}'),
            trailing: FilledButton(
              onPressed: () => context.go('/bank/${bank.bankBuildingId}/request-loan'),
              child: const Text('Borrow'),
            ),
          ),
        ),
    ];
  }

  List<Widget> _buildDepositTab() {
    final banks = _filteredSortedBanks;
    return [
      if (_isAuthenticated) ...[
        Text('Your accounts', style: Theme.of(context).textTheme.titleMedium),
        if (_myAccounts.isEmpty)
          const Text('You have no bank accounts yet.')
        else
          for (final account in _myAccounts)
            ListTile(
              key: ValueKey('my-account-${account.id}'),
              title: Text('${account.companyName ?? 'Personal'} · ${account.balance.toStringAsFixed(0)} ${account.currencyCode}'),
              trailing: TextButton(onPressed: () => _openWithdrawDialog(account), child: const Text('Withdraw')),
            ),
        const SizedBox(height: 16),
      ],
      Text('Banks', style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 8),
      DropdownButtonFormField<String>(
        key: const Key('deposit-city-filter'),
        initialValue: _cityFilter,
        decoration: const InputDecoration(labelText: 'City'),
        items: [
          const DropdownMenuItem(value: 'ALL', child: Text('All cities')),
          for (final city in _cityOptions) DropdownMenuItem(value: city, child: Text(city)),
        ],
        onChanged: (value) => setState(() => _cityFilter = value ?? 'ALL'),
      ),
      CheckboxListTile(
        key: const Key('deposit-available-only'),
        contentPadding: EdgeInsets.zero,
        title: const Text('Available only (base capital deposited)'),
        value: _availableOnly,
        onChanged: (value) => setState(() => _availableOnly = value ?? false),
      ),
      DropdownButtonFormField<_BankSort>(
        key: const Key('deposit-sort-by'),
        initialValue: _sortBy,
        decoration: const InputDecoration(labelText: 'Sort by'),
        items: const [
          DropdownMenuItem(value: _BankSort.depositRate, child: Text('Deposit rate')),
          DropdownMenuItem(value: _BankSort.lendingRate, child: Text('Lending rate')),
          DropdownMenuItem(value: _BankSort.capacity, child: Text('Capacity')),
          DropdownMenuItem(value: _BankSort.city, child: Text('City')),
        ],
        onChanged: (value) => setState(() => _sortBy = value ?? _BankSort.depositRate),
      ),
      const SizedBox(height: 8),
      if (banks.isEmpty) const Text('No banks match your filters.'),
      for (final bank in banks)
        Card(
          key: ValueKey('bank-deposit-${bank.bankBuildingId}'),
          margin: const EdgeInsets.only(bottom: 8),
          child: ListTile(
            title: Text('${bank.bankBuildingName} (${bank.cityName})'),
            subtitle: Text('Deposit rate ${bank.depositInterestRatePercent.toStringAsFixed(1)}%'),
            trailing: FilledButton(onPressed: () => _openDepositDialog(bank), child: const Text('Deposit')),
          ),
        ),
    ];
  }
}
