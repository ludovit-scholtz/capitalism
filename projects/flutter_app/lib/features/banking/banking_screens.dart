// Ported from `projects/frontend/src/views/LoanMarketplaceView.vue`,
// `BankManagementView.vue`, `BankLoanRequestView.vue`, and
// `BankStatementView.vue`.
//
// Deliberately trimmed (documented per-screen below): none of these four
// screens is BuildingDetailView-scale, but Bank Management folds the web's
// separate owner (`BankManagementTabContent.vue`) and customer
// (`BankCustomerView.vue`) sub-views into one screen driven by a simple
// ownership check, and skips the dynamic deposit-rate-change-with-history
// UI (`updateBankDepositRate`/`bankDepositRateHistory` are wired in
// `BankingService` but not exposed here) in favor of the simpler
// immediate `setBankRates`.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'banking_models.dart';
import 'banking_service.dart';

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

  Future<void> _closeAccount(PlayerBankAccount account) async {
    try {
      if (account.isDepositAccount) {
        await _service.closeBankAccount(account.id);
      } else {
        await _service.closeCompanyBankAccount(account.id);
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not close this account.')));
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
    return [
      if (_isAuthenticated) ...[
        Text('Your active loans', style: Theme.of(context).textTheme.titleMedium),
        if (_myLoans.isEmpty)
          const Text('You have no active loans.')
        else
          for (final loan in _myLoans)
            ListTile(
              title: Text('${loan.bankBuildingName} · ${loan.remainingPrincipal.toStringAsFixed(0)} ${loan.loanCurrencyCode}'),
              subtitle: Text(loan.status),
            ),
        const SizedBox(height: 16),
      ],
      Text('Banks', style: Theme.of(context).textTheme.titleMedium),
      for (final bank in _banks)
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
    return [
      if (_isAuthenticated) ...[
        Text('Your accounts', style: Theme.of(context).textTheme.titleMedium),
        if (_myAccounts.isEmpty)
          const Text('You have no bank accounts yet.')
        else
          for (final account in _myAccounts)
            ListTile(
              title: Text('${account.companyName ?? 'Personal'} · ${account.balance.toStringAsFixed(0)} ${account.currencyCode}'),
              trailing: account.balance == 0 ? TextButton(onPressed: () => _closeAccount(account), child: const Text('Close')) : null,
            ),
        const SizedBox(height: 16),
      ],
      Text('Banks', style: Theme.of(context).textTheme.titleMedium),
      for (final bank in _banks)
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

class BankManagementScreen extends StatefulWidget {
  const BankManagementScreen({
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
  State<BankManagementScreen> createState() => _BankManagementScreenState();
}

class _BankManagementScreenState extends State<BankManagementScreen> {
  late final BankingService _service;

  bool _loading = true;
  String? _error;
  BankInfo? _bankInfo;
  List<Map<String, String>> _myCompanies = const [];
  List<LoanSummary> _bankLoans = const [];
  List<BankDeposit> _bankDeposits = const [];
  List<LoanSummary> _myLoansHere = const [];
  final _depositRateController = TextEditingController();
  final _lendingRateController = TextEditingController();
  bool _submitting = false;

  bool get _isOwner => _bankInfo != null && _myCompanies.any((c) => c['id'] == _bankInfo!.lenderCompanyId);

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
    _depositRateController.dispose();
    _lendingRateController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([_service.fetchBankInfo(widget.bankBuildingId), _service.fetchMyCompanies(), _service.fetchMyLoans()]);
      if (!mounted) return;
      final bankInfo = results[0] as BankInfo;
      final myCompanies = results[1] as List<Map<String, String>>;
      final owner = myCompanies.any((c) => c['id'] == bankInfo.lenderCompanyId);

      List<LoanSummary> bankLoans = const [];
      List<BankDeposit> bankDeposits = const [];
      if (owner) {
        final ownerResults = await Future.wait([_service.fetchBankLoans(widget.bankBuildingId), _service.fetchBankDeposits(widget.bankBuildingId)]);
        bankLoans = ownerResults[0] as List<LoanSummary>;
        bankDeposits = ownerResults[1] as List<BankDeposit>;
      }

      if (!mounted) return;
      setState(() {
        _bankInfo = bankInfo;
        _myCompanies = myCompanies;
        _bankLoans = bankLoans;
        _bankDeposits = bankDeposits;
        _myLoansHere = (results[2] as List<LoanSummary>).where((l) => l.bankBuildingId == widget.bankBuildingId).toList();
        _depositRateController.text = bankInfo.depositInterestRatePercent.toStringAsFixed(1);
        _lendingRateController.text = bankInfo.lendingInterestRatePercent.toStringAsFixed(1);
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load this bank. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _saveRates() async {
    setState(() => _submitting = true);
    try {
      await _service.setBankRates(
        bankBuildingId: widget.bankBuildingId,
        depositRate: double.tryParse(_depositRateController.text) ?? 0,
        lendingRate: double.tryParse(_lendingRateController.text) ?? 0,
      );
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not update rates.')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  Future<void> _activateBaseDeposit() async {
    setState(() => _submitting = true);
    try {
      await _service.initiateBaseDeposit(widget.bankBuildingId);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not activate base capital.')));
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
        Text(bankInfo.bankBuildingName, style: theme.textTheme.headlineSmall),
        Text('${bankInfo.totalDeposits.toStringAsFixed(0)} ${bankInfo.cityCurrencyCode} in deposits', style: theme.textTheme.bodyMedium),
        const SizedBox(height: 16),
        if (_isOwner) ...[
          Text('Manage rates', style: theme.textTheme.titleMedium),
          TextField(controller: _depositRateController, decoration: const InputDecoration(labelText: 'Deposit rate (%)'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
          TextField(controller: _lendingRateController, decoration: const InputDecoration(labelText: 'Lending rate (%)'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
          const SizedBox(height: 8),
          FilledButton(onPressed: _submitting ? null : _saveRates, child: const Text('Save rates')),
          if (!bankInfo.baseCapitalDeposited) ...[
            const SizedBox(height: 8),
            OutlinedButton(onPressed: _submitting ? null : _activateBaseDeposit, child: const Text('Activate base capital')),
          ],
          const SizedBox(height: 16),
          Text('Issued loans', style: theme.textTheme.titleMedium),
          if (_bankLoans.isEmpty) const Text('No loans issued yet.') else for (final loan in _bankLoans) ListTile(title: Text(loan.bankBuildingName), subtitle: Text('${loan.remainingPrincipal.toStringAsFixed(0)} ${loan.loanCurrencyCode} · ${loan.status}')),
          const SizedBox(height: 16),
          Text('Deposits', style: theme.textTheme.titleMedium),
          if (_bankDeposits.isEmpty) const Text('No deposits yet.') else for (final deposit in _bankDeposits) ListTile(title: Text('${deposit.amount.toStringAsFixed(0)} @ ${deposit.depositInterestRatePercent.toStringAsFixed(1)}%')),
        ] else ...[
          Text('Your activity here', style: theme.textTheme.titleMedium),
          if (_myLoansHere.isEmpty) const Text('No loans at this bank.') else for (final loan in _myLoansHere) ListTile(title: Text('${loan.remainingPrincipal.toStringAsFixed(0)} ${loan.loanCurrencyCode}'), subtitle: Text(loan.status)),
          const SizedBox(height: 16),
          FilledButton(onPressed: () => context.go('/bank/${widget.bankBuildingId}/request-loan'), child: const Text('Request a loan')),
        ],
      ],
    );
  }
}

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
    setState(() => _submitting = true);
    try {
      await _service.acceptLoan(
        bankBuildingId: widget.bankBuildingId,
        borrowerCompanyId: _companyId!,
        principalAmount: double.tryParse(_principalController.text) ?? 0,
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

class BankStatementScreen extends StatefulWidget {
  const BankStatementScreen({
    super.key,
    this.companyId,
    GraphQlService? graphQlService,
    BankingService? bankingService,
  }) : _injectedGraphQlService = graphQlService,
       _injectedBankingService = bankingService;

  final String? companyId;
  final GraphQlService? _injectedGraphQlService;
  final BankingService? _injectedBankingService;

  @override
  State<BankStatementScreen> createState() => _BankStatementScreenState();
}

class _BankStatementScreenState extends State<BankStatementScreen> {
  late final BankingService _service;

  bool _loading = true;
  String? _error;
  BankStatementResult? _statement;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedBankingService ?? BankingService(graphQlService);
    WidgetsBinding.instance.addPostFrameCallback((_) => _bootstrap());
  }

  Future<void> _bootstrap() async {
    if (!context.read<AuthState>().isAuthenticated) {
      context.go('/login?redirect=%2Fbank-statement');
      return;
    }
    await _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final statement = await _service.fetchBankStatement(companyId: widget.companyId);
      if (!mounted) return;
      setState(() {
        _statement = statement;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load your bank statement. Please try again.';
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

    final statement = _statement!;
    final theme = Theme.of(context);

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Bank Statement', style: theme.textTheme.headlineSmall),
          Text('${statement.companyName} · Balance ${statement.currentBalance.toStringAsFixed(2)} ${statement.currencyCode}', style: theme.textTheme.bodyMedium),
          const SizedBox(height: 16),
          if (statement.rows.isEmpty)
            const Text('No transactions yet.')
          else
            for (final row in statement.rows)
              ListTile(
                title: Text(row.description ?? row.category ?? 'Transaction'),
                trailing: Text('${row.amount >= 0 ? '+' : ''}${row.amount.toStringAsFixed(2)}'),
              ),
        ],
      ),
    );
  }
}
