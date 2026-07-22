// New screen — no direct web equivalent yet (the web's ranking-bounty UI
// lives on `projects/master-frontend`, not `projects/frontend`). Shows only
// *completed* (awarded) bounties, per the ask — the "available bounties to
// claim" dashboard (`myRankingBountyDashboard`) is a separate, not-yet-ported
// screen.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import '../../core/widgets/icon_badge.dart';
import 'bounty_models.dart';
import 'bounty_service.dart';

class BountyHistoryScreen extends StatefulWidget {
  const BountyHistoryScreen({super.key, GraphQlService? graphQlService, BountyService? bountyService})
    : _injectedGraphQlService = graphQlService,
      _injectedBountyService = bountyService;

  final GraphQlService? _injectedGraphQlService;
  final BountyService? _injectedBountyService;

  @override
  State<BountyHistoryScreen> createState() => _BountyHistoryScreenState();
}

class _BountyHistoryScreenState extends State<BountyHistoryScreen> {
  late final BountyService _service;

  bool _loading = true;
  String? _error;
  List<CompletedBounty> _bounties = const [];

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedBountyService ?? BountyService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final bounties = await _service.fetchCompletedBounties();
      if (!mounted) return;
      setState(() {
        _bounties = bounties;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load your bounties. Please try again.';
        _loading = false;
      });
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

    final totalPoints = _bounties.fold<double>(0, (sum, bounty) => sum + bounty.pointsAwarded);

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Completed Bounties', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text(
            '${_bounties.length} bounty(ies) awarded · ${totalPoints.toStringAsFixed(0)} points total',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 16),
          if (_bounties.isEmpty)
            const Text('No bounties completed yet.')
          else
            for (final bounty in _bounties) _CompletedBountyTile(bounty: bounty),
        ],
      ),
    );
  }
}

class _CompletedBountyTile extends StatelessWidget {
  const _CompletedBountyTile({required this.bounty});

  final CompletedBounty bounty;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      key: ValueKey('bounty-${bounty.id}'),
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        leading: const IconBadge(icon: AppIcons.bounty, size: 36, iconSize: 16),
        title: Text(bounty.bountyDisplayName),
        subtitle: Text(
          [bounty.awardedAtUtc, if (bounty.serverKey != null) bounty.serverKey!].join(' · '),
          style: theme.textTheme.bodySmall,
        ),
        trailing: Text('+${bounty.pointsAwarded.toStringAsFixed(0)}', style: theme.textTheme.titleSmall),
      ),
    );
  }
}
