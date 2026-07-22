// Ported from `projects/frontend/src/views/BankStatementView.vue`
// (+ `BankStatementSummaryCard.vue`/`BankStatementTable.vue`), including the
// account selector, page-size selector (20/50/100/200), fromTick/toTick
// range filter, and prev/next pagination.

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import 'banking_models.dart';
import 'banking_service.dart';

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
  List<PlayerBankAccount> _accounts = const [];

  String? _accountId;
  int _pageSize = 50;
  int _offset = 0;
  final _fromTickController = TextEditingController();
  final _toTickController = TextEditingController();

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedBankingService ?? BankingService(graphQlService);
    WidgetsBinding.instance.addPostFrameCallback((_) => _bootstrap());
  }

  @override
  void dispose() {
    _fromTickController.dispose();
    _toTickController.dispose();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    if (!context.read<AuthState>().isAuthenticated) {
      context.go('/login?redirect=%2Fbank-statement');
      return;
    }
    try {
      final accounts = await _service.fetchMyBankAccounts();
      if (!mounted) return;
      setState(() => _accounts = accounts);
    } catch (_) {
      // The account selector just stays on "All accounts" if this fails —
      // the statement itself still loads independently below.
    }
    await _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final statement = await _service.fetchBankStatement(
        companyId: widget.companyId,
        accountId: _accountId,
        limit: _pageSize,
        offset: _offset,
        fromTick: int.tryParse(_fromTickController.text),
        toTick: int.tryParse(_toTickController.text),
      );
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

  void _applyFilters() {
    setState(() => _offset = 0);
    _load();
  }

  @override
  Widget build(BuildContext context) {
    if (_loading && _statement == null) return const Center(child: CircularProgressIndicator());
    if (_error != null && _statement == null) {
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
    final hasNextPage = statement.rows.length >= _pageSize && (_offset + _pageSize) < statement.totalEntries;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Bank Statement', style: theme.textTheme.headlineSmall),
          Text('${statement.companyName} · Balance ${statement.currentBalance.toStringAsFixed(2)} ${statement.currencyCode}', style: theme.textTheme.bodyMedium),
          const SizedBox(height: 16),
          if (_accounts.isNotEmpty)
            DropdownButtonFormField<String?>(
              key: const Key('statement-account-selector'),
              initialValue: _accountId,
              decoration: const InputDecoration(labelText: 'Account'),
              items: [
                const DropdownMenuItem(value: null, child: Text('All accounts')),
                for (final account in _accounts)
                  DropdownMenuItem(value: account.id, child: Text('${account.companyName ?? 'Personal'} · ${account.currencyCode}')),
              ],
              onChanged: (value) {
                setState(() => _accountId = value);
                _applyFilters();
              },
            ),
          const SizedBox(height: 8),
          DropdownButtonFormField<int>(
            key: const Key('statement-page-size'),
            initialValue: _pageSize,
            decoration: const InputDecoration(labelText: 'Page size'),
            items: const [
              DropdownMenuItem(value: 20, child: Text('20')),
              DropdownMenuItem(value: 50, child: Text('50')),
              DropdownMenuItem(value: 100, child: Text('100')),
              DropdownMenuItem(value: 200, child: Text('200')),
            ],
            onChanged: (value) {
              if (value == null) return;
              setState(() => _pageSize = value);
              _applyFilters();
            },
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _fromTickController,
                  decoration: const InputDecoration(labelText: 'From tick'),
                  keyboardType: TextInputType.number,
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: TextField(
                  controller: _toTickController,
                  decoration: const InputDecoration(labelText: 'To tick'),
                  keyboardType: TextInputType.number,
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          OutlinedButton(onPressed: _applyFilters, child: const Text('Apply filters')),
          const SizedBox(height: 16),
          if (statement.rows.isEmpty)
            const Text('No transactions yet.')
          else
            for (final row in statement.rows)
              ListTile(
                title: Text(row.description ?? row.category ?? 'Transaction'),
                trailing: Text('${row.amount >= 0 ? '+' : ''}${row.amount.toStringAsFixed(2)}'),
              ),
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              IconButton(
                onPressed: _offset > 0
                    ? () {
                        setState(() => _offset = (_offset - _pageSize).clamp(0, 1 << 30));
                        _load();
                      }
                    : null,
                icon: const FaIcon(AppIcons.chevronLeft, size: 16),
              ),
              Text('${(_offset ~/ _pageSize) + 1}'),
              IconButton(
                onPressed: hasNextPage
                    ? () {
                        setState(() => _offset += _pageSize);
                        _load();
                      }
                    : null,
                icon: const FaIcon(AppIcons.chevronRight, size: 16),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
