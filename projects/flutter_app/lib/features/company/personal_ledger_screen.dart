// Ported from `projects/frontend/src/views/PersonalLedgerView.vue`.
// Deliberately trimmed: no endgame race progress bar/leaderboard or
// milestone toast notifications (tied to a separate `endgameStore`), and
// no interest-payments history table with its ALL/INTEREST/DIVIDEND
// filter — the core wealth summary, shareholdings, dividends, and stock
// trades are real and GraphQL-backed.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'personal_ledger_models.dart';
import 'personal_ledger_service.dart';

class PersonalLedgerScreen extends StatefulWidget {
  const PersonalLedgerScreen({super.key, GraphQlService? graphQlService, PersonalLedgerService? personalLedgerService})
    : _injectedGraphQlService = graphQlService,
      _injectedPersonalLedgerService = personalLedgerService;

  final GraphQlService? _injectedGraphQlService;
  final PersonalLedgerService? _injectedPersonalLedgerService;

  @override
  State<PersonalLedgerScreen> createState() => _PersonalLedgerScreenState();
}

class _PersonalLedgerScreenState extends State<PersonalLedgerScreen> {
  late final PersonalLedgerService _service;
  late final bool _isAuthenticated;

  bool _loading = true;
  String? _error;
  PersonAccount? _account;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    _isAuthenticated = auth.isAuthenticated;
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedPersonalLedgerService ?? PersonalLedgerService(graphQlService);
    if (_isAuthenticated) {
      _load();
    } else {
      _loading = false;
    }
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final account = await _service.fetchPersonAccount();
      if (!mounted) return;
      setState(() {
        _account = account;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load your personal ledger. Please try again.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (!_isAuthenticated) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('Sign in to view your personal ledger.'),
              const SizedBox(height: 12),
              FilledButton(onPressed: () => context.go('/login?redirect=%2Fpersonal-ledger'), child: const Text('Sign in')),
            ],
          ),
        ),
      );
    }
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

    final account = _account;
    if (account == null) return const Center(child: Text('No personal account found.'));

    final theme = Theme.of(context);
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Personal Ledger', style: theme.textTheme.headlineSmall),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(child: _WealthCard(label: 'Net wealth', value: account.totalNetWealth)),
              const SizedBox(width: 8),
              Expanded(child: _WealthCard(label: 'Cash', value: account.availableCash)),
            ],
          ),
          const SizedBox(height: 8),
          _WealthCard(label: 'Tax reserve', value: account.taxReserve),
          const SizedBox(height: 16),
          Text('Shareholdings', style: theme.textTheme.titleMedium),
          if (account.shareholdings.isEmpty)
            const Text('No shares owned.')
          else
            for (final holding in account.shareholdings)
              ListTile(
                dense: true,
                title: Text(holding.companyName),
                subtitle: Text('${holding.shareCount.toStringAsFixed(0)} shares · ${(holding.ownershipRatio * 100).toStringAsFixed(1)}%'),
                trailing: Text(holding.marketValue.toStringAsFixed(0)),
              ),
          const SizedBox(height: 16),
          Text('Dividend payments', style: theme.textTheme.titleMedium),
          if (account.dividendPayments.isEmpty)
            const Text('No dividends received yet.')
          else
            for (final payment in account.dividendPayments)
              ListTile(dense: true, title: Text(payment.companyName), trailing: Text('+${payment.totalAmount.toStringAsFixed(2)}')),
          const SizedBox(height: 16),
          Text('Stock trades', style: theme.textTheme.titleMedium),
          if (account.stockTrades.isEmpty)
            const Text('No stock trades yet.')
          else
            for (final trade in account.stockTrades)
              ListTile(
                dense: true,
                title: Text('${trade.direction} ${trade.companyName}'),
                trailing: Text('${trade.shareCount.toStringAsFixed(0)} @ ${trade.totalValue.toStringAsFixed(2)}'),
              ),
        ],
      ),
    );
  }
}

class _WealthCard extends StatelessWidget {
  const _WealthCard({required this.label, required this.value});

  final String label;
  final double value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [Text(label, style: theme.textTheme.labelSmall), Text(value.toStringAsFixed(0), style: theme.textTheme.titleMedium)],
        ),
      ),
    );
  }
}
