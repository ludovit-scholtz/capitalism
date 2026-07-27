// Ported from `projects/frontend/src/views/BankManagementView.vue`, folding
// the web's separate owner (`BankManagementTabContent.vue`) and customer
// (`BankCustomerView.vue`) sub-views into one screen driven by a simple
// `lenderCompanyId` ownership check.
//
// Owner view: rate management (immediate `setBankRates` plus a scheduled
// `updateBankDepositRate` + `bankDepositRateHistory` audit trail), a
// liquidity health panel (available cash, reserve requirement/shortfall,
// central bank debt/rate, liquidity status), issued loans, and deposits.
// Customer view: their loans at this bank (with a repay-in-full action for
// overdue/defaulted loans) and a "Request a loan" CTA.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/i18n/locale_state.dart';
import '../../core/utils/game_time.dart';
import 'banking_models.dart';
import 'banking_service.dart';

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
  final _scheduledRateController = TextEditingController();
  bool _submitting = false;

  bool _rateHistoryVisible = false;
  bool _rateHistoryLoading = false;
  List<BankDepositRateHistoryEntry> _rateHistory = const [];

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
    _scheduledRateController.dispose();
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

  Future<void> _scheduleDepositRateChange() async {
    final newRate = double.tryParse(_scheduledRateController.text);
    if (newRate == null) return;
    setState(() => _submitting = true);
    try {
      await _service.updateBankDepositRate(bankBuildingId: widget.bankBuildingId, newRatePercent: newRate);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Deposit rate change scheduled.')));
      }
      _scheduledRateController.clear();
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not schedule the rate change.')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  Future<void> _toggleRateHistory() async {
    if (_rateHistoryVisible) {
      setState(() => _rateHistoryVisible = false);
      return;
    }
    setState(() {
      _rateHistoryVisible = true;
      _rateHistoryLoading = true;
    });
    try {
      final history = await _service.fetchBankDepositRateHistory(widget.bankBuildingId);
      if (!mounted) return;
      setState(() {
        _rateHistory = history;
        _rateHistoryLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _rateHistoryLoading = false);
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
    final languageCode = context.watch<LocaleState>().languageCode;

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text(bankInfo.bankBuildingName, style: theme.textTheme.headlineSmall),
        Text('${bankInfo.totalDeposits.toStringAsFixed(0)} ${bankInfo.cityCurrencyCode} in deposits', style: theme.textTheme.bodyMedium),
        const SizedBox(height: 16),
        if (_isOwner) ..._buildOwnerView(theme, bankInfo, languageCode) else ..._buildCustomerView(theme),
      ],
    );
  }

  List<Widget> _buildOwnerView(ThemeData theme, BankInfo bankInfo, String languageCode) {
    return [
      Text('Liquidity', style: theme.textTheme.titleMedium),
      Card(
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (bankInfo.liquidityStatus != null) Text('Status: ${bankInfo.liquidityStatus}'),
              Text('Available cash: ${bankInfo.availableCash.toStringAsFixed(0)}'),
              Text('Reserve requirement: ${bankInfo.reserveRequirement.toStringAsFixed(0)}'),
              if (bankInfo.reserveShortfall > 0)
                Text('Reserve shortfall: ${bankInfo.reserveShortfall.toStringAsFixed(0)}', style: TextStyle(color: theme.colorScheme.error)),
              if (bankInfo.centralBankDebt > 0)
                Text('Central bank debt: ${bankInfo.centralBankDebt.toStringAsFixed(0)} @ ${bankInfo.centralBankInterestRatePercent.toStringAsFixed(1)}%'),
            ],
          ),
        ),
      ),
      const SizedBox(height: 16),
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
      Text('Schedule deposit rate change', style: theme.textTheme.titleMedium),
      Text(
        'Applies to all existing deposits 24 ticks after scheduling.',
        style: theme.textTheme.bodySmall,
      ),
      if (bankInfo.pendingDepositInterestRatePercent != null)
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 4),
          child: Tooltip(
            message: 'Tick ${bankInfo.pendingDepositRateEffectiveTick ?? '—'}',
            child: Text(
              'Pending: ${bankInfo.pendingDepositInterestRatePercent!.toStringAsFixed(1)}% at '
              '${bankInfo.pendingDepositRateEffectiveTick != null ? formatGameTickTime(bankInfo.pendingDepositRateEffectiveTick!, languageCode) : '—'}',
            ),
          ),
        ),
      TextField(
        controller: _scheduledRateController,
        decoration: const InputDecoration(labelText: 'New deposit rate (%)'),
        keyboardType: const TextInputType.numberWithOptions(decimal: true),
      ),
      const SizedBox(height: 8),
      OutlinedButton(onPressed: _submitting ? null : _scheduleDepositRateChange, child: const Text('Schedule change')),
      const SizedBox(height: 4),
      TextButton(onPressed: _toggleRateHistory, child: Text(_rateHistoryVisible ? 'Hide rate history' : 'View rate history')),
      if (_rateHistoryVisible) ...[
        if (_rateHistoryLoading)
          const Center(child: CircularProgressIndicator())
        else if (_rateHistory.isEmpty)
          const Text('No rate changes yet.')
        else
          for (final entry in _rateHistory)
            ListTile(
              dense: true,
              title: Text('${entry.previousRatePercent.toStringAsFixed(1)}% → ${entry.newRatePercent.toStringAsFixed(1)}%'),
              subtitle: Tooltip(
                message: 'Tick ${entry.effectiveTick}',
                child: Text(
                  '${formatGameTickTime(entry.effectiveTick, languageCode)} · ${entry.isApplied ? 'Applied' : 'Pending'}',
                ),
              ),
            ),
      ],
      const SizedBox(height: 16),
      Text('Issued loans', style: theme.textTheme.titleMedium),
      if (_bankLoans.isEmpty)
        const Text('No loans issued yet.')
      else
        for (final loan in _bankLoans)
          ListTile(title: Text(loan.bankBuildingName), subtitle: Text('${loan.remainingPrincipal.toStringAsFixed(0)} ${loan.loanCurrencyCode} · ${loan.status}')),
      const SizedBox(height: 16),
      Text('Deposits', style: theme.textTheme.titleMedium),
      if (_bankDeposits.isEmpty)
        const Text('No deposits yet.')
      else
        for (final deposit in _bankDeposits) ListTile(title: Text('${deposit.amount.toStringAsFixed(0)} @ ${deposit.depositInterestRatePercent.toStringAsFixed(1)}%')),
    ];
  }

  List<Widget> _buildCustomerView(ThemeData theme) {
    return [
      Text('Your activity here', style: theme.textTheme.titleMedium),
      if (_myLoansHere.isEmpty)
        const Text('No loans at this bank.')
      else
        for (final loan in _myLoansHere)
          ListTile(
            key: ValueKey('customer-loan-${loan.id}'),
            title: Text('${loan.remainingPrincipal.toStringAsFixed(0)} ${loan.loanCurrencyCode}'),
            subtitle: Text(loan.status),
            trailing: loan.isRepayable ? TextButton(onPressed: () => _repayLoan(loan), child: const Text('Repay now')) : null,
          ),
      const SizedBox(height: 16),
      FilledButton(onPressed: () => context.go('/bank/${widget.bankBuildingId}/request-loan'), child: const Text('Request a loan')),
    ];
  }
}
