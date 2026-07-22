import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';

/// Mirrors `HomeView.vue`: hero section, game status cards (tick, tax
/// rate), a leaderboard preview, and an auth-state-dependent CTA. The web
/// view's three-way CTA ("Get Started" / "Start Your Empire" / "Go to
/// Dashboard") additionally depends on `player.onboardingCompletedAtUtc`,
/// which isn't available yet — `AuthState` only tracks whether a token
/// exists, not the player profile (see ROADMAP.md for the `me` query
/// follow-up) — so this collapses to a two-way CTA for now.
class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key, GraphQlService? graphQlService}) : _injectedGraphQlService = graphQlService;

  final GraphQlService? _injectedGraphQlService;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeStatus {
  const _HomeStatus({required this.currentTick, required this.taxRate, required this.topPlayers});

  final int currentTick;
  final double taxRate;
  final List<_RankedPlayer> topPlayers;
}

class _RankedPlayer {
  const _RankedPlayer({required this.displayName, required this.totalWealthUsd});

  final String displayName;
  final double totalWealthUsd;
}

const _homeStatusQuery = '''
  query HomeStatus {
    gameState { currentTick taxRate }
    rankings { displayName totalWealthUsd }
  }
''';

class _HomeScreenState extends State<HomeScreen> {
  late final GraphQlService _graphQlService;
  Future<_HomeStatus>? _statusFuture;

  @override
  void initState() {
    super.initState();
    _graphQlService = widget._injectedGraphQlService ?? GraphQlService(context.read<AuthState>());
    _statusFuture = _loadStatus();
  }

  Future<_HomeStatus> _loadStatus() async {
    final data = await _graphQlService.request(_homeStatusQuery);

    final gameState = data['gameState'] as Map<String, dynamic>?;
    final rankings = (data['rankings'] as List<dynamic>?) ?? const [];

    final topPlayers = rankings
        .cast<Map<String, dynamic>>()
        .take(5)
        .map(
          (entry) => _RankedPlayer(
            displayName: entry['displayName'] as String? ?? '—',
            totalWealthUsd: (entry['totalWealthUsd'] as num?)?.toDouble() ?? 0,
          ),
        )
        .toList();

    return _HomeStatus(
      currentTick: (gameState?['currentTick'] as num?)?.toInt() ?? 0,
      taxRate: (gameState?['taxRate'] as num?)?.toDouble() ?? 0,
      topPlayers: topPlayers,
    );
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();

    return RefreshIndicator(
      onRefresh: () async {
        final next = _loadStatus();
        setState(() => _statusFuture = next);
        await next;
      },
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          _Hero(isAuthenticated: auth.isAuthenticated),
          const SizedBox(height: 24),
          FutureBuilder<_HomeStatus>(
            future: _statusFuture,
            builder: (context, snapshot) {
              if (snapshot.connectionState != ConnectionState.done) {
                return const Padding(
                  padding: EdgeInsets.symmetric(vertical: 32),
                  child: Center(child: CircularProgressIndicator()),
                );
              }
              if (snapshot.hasError) {
                return _StatusError(
                  onRetry: () => setState(() => _statusFuture = _loadStatus()),
                );
              }
              final status = snapshot.data!;
              return Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _StatusCards(status: status),
                  const SizedBox(height: 24),
                  _LeaderboardPreview(topPlayers: status.topPlayers),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

class _Hero extends StatelessWidget {
  const _Hero({required this.isAuthenticated});

  final bool isAuthenticated;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Capitalism', style: theme.textTheme.headlineMedium),
        const SizedBox(height: 8),
        Text(
          'Build your economic empire — mine resources, run factories, trade on the exchange.',
          style: theme.textTheme.bodyLarge,
        ),
        const SizedBox(height: 16),
        FilledButton(
          onPressed: () => context.go(isAuthenticated ? '/dashboard' : '/login'),
          child: Text(isAuthenticated ? 'Go to Dashboard' : 'Get Started'),
        ),
      ],
    );
  }
}

class _StatusCards extends StatelessWidget {
  const _StatusCards({required this.status});

  final _HomeStatus status;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(child: _StatCard(label: 'Tick', value: '${status.currentTick}')),
        const SizedBox(width: 12),
        Expanded(child: _StatCard(label: 'Tax Rate', value: '${status.taxRate.toStringAsFixed(1)}%')),
      ],
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label, style: theme.textTheme.labelMedium),
            const SizedBox(height: 4),
            Text(value, style: theme.textTheme.headlineSmall),
          ],
        ),
      ),
    );
  }
}

class _LeaderboardPreview extends StatelessWidget {
  const _LeaderboardPreview({required this.topPlayers});

  final List<_RankedPlayer> topPlayers;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Text('Leaderboard', style: theme.textTheme.titleMedium),
                const Spacer(),
                TextButton(onPressed: () => context.go('/leaderboard'), child: const Text('View all')),
              ],
            ),
            if (topPlayers.isEmpty) Text('No rankings yet.', style: theme.textTheme.bodyMedium),
            for (final player in topPlayers)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const FaIcon(AppIcons.leaderboard, size: 18),
                title: Text(player.displayName),
                trailing: Text('\$${player.totalWealthUsd.toStringAsFixed(0)}'),
              ),
          ],
        ),
      ),
    );
  }
}

class _StatusError extends StatelessWidget {
  const _StatusError({required this.onRetry});

  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 24),
      child: Column(
        children: [
          const FaIcon(AppIcons.wifiOff, size: 28),
          const SizedBox(height: 8),
          const Text('Could not load game status.'),
          const SizedBox(height: 8),
          OutlinedButton(onPressed: onRetry, child: const Text('Retry')),
        ],
      ),
    );
  }
}
