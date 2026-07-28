// Ported from `projects/frontend/src/views/PersonalLedgerView.vue`: the
// core wealth summary, shareholdings, dividends, and stock trades; the
// "Race to the Top" endgame benchmark card (`personal_ledger_race_card.dart`,
// fed by `LeaderboardService.fetchEndgameStatus()` — reused rather than
// duplicated, matching the web's separate `endgameStore`); milestone toast
// notifications (shown via `SnackBar`, the natural Flutter analogue of the
// web's toast, when net worth crosses 1/10/25/50/75/90% of the winning
// threshold — tracked per-session in `_triggeredMilestones`, mirroring
// `endgameStore.checkMilestones`); and the passive-income history panel
// with its ALL/INTEREST/DIVIDEND filter (`personal_ledger_income_panel.dart`).

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../leaderboard/leaderboard_models.dart';
import '../leaderboard/leaderboard_service.dart';
import 'personal_ledger_income_panel.dart';
import 'personal_ledger_models.dart';
import 'personal_ledger_race_card.dart';
import 'personal_ledger_service.dart';

/// Fraction of the winning threshold → toast message, ported from
/// `MILESTONE_TOAST_KEYS` in `PersonalLedgerView.vue`.
final _milestoneToasts = <double, String>{
  0.01: "🎉 You've reached 1% of the winning threshold!",
  0.1: "🔥 You've crossed 10% of the winning threshold!",
  0.25: "🚀 You're 25% of the way to victory!",
  0.5: '⚡ Halfway there — 50% of the benchmark reached!',
  0.75: "💪 75% of the winning threshold — you're in striking range!",
  0.9: '🏁 90% reached — the finish line is in sight!',
};

class PersonalLedgerScreen extends StatefulWidget {
  const PersonalLedgerScreen({
    super.key,
    GraphQlService? graphQlService,
    PersonalLedgerService? personalLedgerService,
    LeaderboardService? leaderboardService,
  }) : _injectedGraphQlService = graphQlService,
       _injectedPersonalLedgerService = personalLedgerService,
       _injectedLeaderboardService = leaderboardService;

  final GraphQlService? _injectedGraphQlService;
  final PersonalLedgerService? _injectedPersonalLedgerService;
  final LeaderboardService? _injectedLeaderboardService;

  @override
  State<PersonalLedgerScreen> createState() => _PersonalLedgerScreenState();
}

class _PersonalLedgerScreenState extends State<PersonalLedgerScreen> {
  late final PersonalLedgerService _service;
  late final LeaderboardService _leaderboardService;
  late final bool _isAuthenticated;

  bool _loading = true;
  String? _error;
  PersonAccount? _account;
  EndgameStatus? _endgame;
  final Set<double> _triggeredMilestones = {};

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    _isAuthenticated = auth.isAuthenticated;
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedPersonalLedgerService ?? PersonalLedgerService(graphQlService);
    _leaderboardService = widget._injectedLeaderboardService ?? LeaderboardService(graphQlService);
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
      final results = await Future.wait([_service.fetchPersonAccount(), _leaderboardService.fetchEndgameStatus()]);
      if (!mounted) return;
      final account = results[0] as PersonAccount?;
      final endgame = results[1] as EndgameStatus?;
      setState(() {
        _account = account;
        _endgame = endgame;
        _loading = false;
      });
      if (account != null && endgame != null) _checkMilestones(account.totalNetWealth, endgame.winningThresholdUsd);
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load your personal ledger. Please try again.';
        _loading = false;
      });
    }
  }

  void _checkMilestones(double netWorthUsd, double thresholdUsd) {
    if (thresholdUsd <= 0) return;
    final ratio = netWorthUsd / thresholdUsd;
    for (final milestone in _milestoneToasts.keys) {
      if (ratio >= milestone && _triggeredMilestones.add(milestone)) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(_milestoneToasts[milestone]!)));
      }
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
          if (_endgame != null) ...[
            const SizedBox(height: 16),
            PersonalLedgerRaceCard(endgame: _endgame!, playerNetWorthUsd: account.totalNetWealth),
          ],
          const SizedBox(height: 16),
          PersonalLedgerIncomePanel(dividendPayments: account.dividendPayments, interestPayments: account.interestPayments),
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
