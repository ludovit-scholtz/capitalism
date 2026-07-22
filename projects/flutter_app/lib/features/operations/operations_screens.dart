// Ported from `projects/frontend/src/views/OperationsOverviewView.vue`,
// `OperationsStatisticsView.vue`, `OperationsAnalyticsView.vue`,
// `OperationsNewsView.vue`, `OperationsPlayersView.vue`, and
// `OperationsPlayerDetailView.vue`.
//
// Deliberately, extensively trimmed to **read-only** dashboards — these
// are sensitive server administration screens (player impersonation,
// granting global admin roles, ending the game shard, publishing news to
// every player), and a mobile first pass covers viewing operational data
// only. Not ported, all gated behind `[Authorize(Roles = "Admin")]` or
// stronger on the backend and left for a future pass if genuinely needed
// on mobile:
// - Player impersonation (`startAdminImpersonation`/`stopImpersonation`).
// - Granting/removing global admin roles (`assignGlobalGameAdminRole`/
//   `removeGlobalGameAdminRole`) and per-player local admin toggling
//   (`setLocalGameAdminRole`).
// - NPC competitor pause/resume (`pauseNpcCompany`/`resumeNpcCompany`) and
//   the NPC decision log panel.
// - Manually ending the game shard (`endShardManually`).
// - Player chat-visibility toggling (`setPlayerInvisibleInChat`).
// - News composing/editing/publishing (`upsertGamesEntry`) — the feed is
//   shown read-only (drafts included) instead of the web's full CMS form.
// - CSV export of product analytics.
//
// All six screens gate on `gameAdminSession.canAccessAdminDashboard`
// (verified against `Api/Types/Query.Admin.cs`) rather than relying only
// on the drawer hiding the nav section for non-admins.

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import 'operations_models.dart';
import 'operations_service.dart';

abstract class _OperationsScreen extends StatefulWidget {
  const _OperationsScreen({super.key, this.graphQlService, this.operationsService});

  final GraphQlService? graphQlService;
  final OperationsService? operationsService;
}

abstract class _OperationsScreenState<T extends _OperationsScreen> extends State<T> {
  late final OperationsService service;

  bool loading = true;
  String? error;
  bool canAccess = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget.graphQlService ?? GraphQlService(auth);
    service = widget.operationsService ?? OperationsService(graphQlService);
    load();
  }

  Future<void> loadOperationsData();

  Future<void> load() async {
    setState(() {
      loading = true;
      error = null;
    });
    try {
      final access = await service.fetchCanAccessAdminDashboard();
      if (!access) {
        if (!mounted) return;
        setState(() {
          canAccess = false;
          loading = false;
        });
        return;
      }
      await loadOperationsData();
      if (!mounted) return;
      setState(() {
        canAccess = true;
        loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        error = 'Could not load operations data. Please try again.';
        loading = false;
      });
    }
  }

  Widget buildScaffold(Widget Function() buildContent) {
    if (loading) return const Center(child: CircularProgressIndicator());
    if (error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [Text(error!), const SizedBox(height: 12), OutlinedButton(onPressed: load, child: const Text('Try again'))],
          ),
        ),
      );
    }
    if (!canAccess) {
      return const Center(child: Text('Administrators only.'));
    }
    return buildContent();
  }
}

class OperationsOverviewScreen extends _OperationsScreen {
  const OperationsOverviewScreen({super.key, super.graphQlService, super.operationsService});

  @override
  State<OperationsOverviewScreen> createState() => _OperationsOverviewScreenState();
}

class _OperationsOverviewScreenState extends _OperationsScreenState<OperationsOverviewScreen> {
  GameAdminDashboard? _dashboard;

  @override
  Future<void> loadOperationsData() async {
    _dashboard = await service.fetchDashboard();
  }

  @override
  Widget build(BuildContext context) {
    return buildScaffold(() {
      final dashboard = _dashboard!;
      final theme = Theme.of(context);
      Widget metric(String label, double value) => Card(
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [Text(label, style: theme.textTheme.labelSmall), Text(value.toStringAsFixed(0), style: theme.textTheme.titleMedium)],
          ),
        ),
      );

      return ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Operations Overview', style: theme.textTheme.headlineSmall),
          const SizedBox(height: 16),
          GridView.count(
            crossAxisCount: 2,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            mainAxisSpacing: 8,
            crossAxisSpacing: 8,
            childAspectRatio: 2,
            children: [
              metric('Money supply', dashboard.moneySupply),
              metric('Personal cash', dashboard.totalPersonalCash),
              metric('Company cash', dashboard.totalCompanyCash),
              metric('External inflow (100t)', dashboard.externalMoneyInflowLast100Ticks),
              metric('Shipping costs (100t)', dashboard.totalShippingCostsLast100Ticks),
              metric('Players', dashboard.players.length.toDouble()),
            ],
          ),
        ],
      );
    });
  }
}

class OperationsMoneyFlowScreen extends _OperationsScreen {
  const OperationsMoneyFlowScreen({super.key, super.graphQlService, super.operationsService});

  @override
  State<OperationsMoneyFlowScreen> createState() => _OperationsMoneyFlowScreenState();
}

class _OperationsMoneyFlowScreenState extends _OperationsScreenState<OperationsMoneyFlowScreen> {
  String _range = 'LAST_7_DAYS';
  OperationsStatistics? _stats;

  static const _rangeOptions = ['LAST_24_HOURS', 'LAST_7_DAYS', 'LAST_30_DAYS', 'ALL_TIME'];

  @override
  Future<void> loadOperationsData() async {
    _stats = await service.fetchStatistics(_range);
  }

  @override
  Widget build(BuildContext context) {
    return buildScaffold(() {
      final stats = _stats!;
      final theme = Theme.of(context);
      return ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Money Flow', style: theme.textTheme.headlineSmall),
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            initialValue: _range,
            decoration: const InputDecoration(labelText: 'Range'),
            items: [for (final range in _rangeOptions) DropdownMenuItem(value: range, child: Text(range))],
            onChanged: (value) {
              setState(() => _range = value ?? _range);
              load();
            },
          ),
          const SizedBox(height: 12),
          Text('Inflow: ${stats.totalInflow.toStringAsFixed(0)} · Outflow: ${stats.totalOutflow.toStringAsFixed(0)} · Net: ${stats.netFlow.toStringAsFixed(0)}'),
          Text('${stats.totalPlayerCount} players · ${stats.totalCompanyCount} companies'),
          const SizedBox(height: 16),
          Text('Inflow sources', style: theme.textTheme.titleMedium),
          for (final item in stats.inflowItems) ListTile(dense: true, title: Text(item.label), trailing: Text(item.amount.toStringAsFixed(0))),
          const SizedBox(height: 16),
          Text('Outflow categories', style: theme.textTheme.titleMedium),
          for (final item in stats.outflowItems) ListTile(dense: true, title: Text(item.label), trailing: Text(item.amount.toStringAsFixed(0))),
        ],
      );
    });
  }
}

class OperationsProductAnalyticsScreen extends _OperationsScreen {
  const OperationsProductAnalyticsScreen({super.key, super.graphQlService, super.operationsService});

  @override
  State<OperationsProductAnalyticsScreen> createState() => _OperationsProductAnalyticsScreenState();
}

class _OperationsProductAnalyticsScreenState extends _OperationsScreenState<OperationsProductAnalyticsScreen> {
  List<ProductAnalyticsRow> _rows = const [];

  @override
  Future<void> loadOperationsData() async {
    _rows = await service.fetchProductAnalytics();
  }

  @override
  Widget build(BuildContext context) {
    return buildScaffold(() {
      final theme = Theme.of(context);
      return ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Product Analytics', style: theme.textTheme.headlineSmall),
          const SizedBox(height: 12),
          for (final row in _rows)
            Card(
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(row.productName),
                subtitle: Text('${row.industry ?? '—'} · ${row.activeSellerCount} sellers'),
                trailing: Text('Rev ${row.totalRevenue.toStringAsFixed(0)}'),
              ),
            ),
        ],
      );
    });
  }
}

class OperationsNewsScreen extends _OperationsScreen {
  const OperationsNewsScreen({super.key, super.graphQlService, super.operationsService});

  @override
  State<OperationsNewsScreen> createState() => _OperationsNewsScreenState();
}

class _OperationsNewsScreenState extends _OperationsScreenState<OperationsNewsScreen> {
  List<AdminNewsEntry> _entries = const [];

  @override
  Future<void> loadOperationsData() async {
    _entries = await service.fetchNewsFeed();
  }

  @override
  Widget build(BuildContext context) {
    return buildScaffold(() {
      final theme = Theme.of(context);
      return ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('News Manager', style: theme.textTheme.headlineSmall),
          const SizedBox(height: 12),
          for (final entry in _entries)
            Card(
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(entry.localizationFor('en')?.title ?? '(untitled)'),
                subtitle: Text(entry.entryType),
                trailing: Chip(label: Text(entry.status)),
              ),
            ),
        ],
      );
    });
  }
}

class OperationsPlayersScreen extends _OperationsScreen {
  const OperationsPlayersScreen({super.key, super.graphQlService, super.operationsService});

  @override
  State<OperationsPlayersScreen> createState() => _OperationsPlayersScreenState();
}

class _OperationsPlayersScreenState extends _OperationsScreenState<OperationsPlayersScreen> {
  List<GameAdminPlayer> _players = const [];
  String _search = '';

  @override
  Future<void> loadOperationsData() async {
    final dashboard = await service.fetchDashboard();
    _players = dashboard.players;
  }

  @override
  Widget build(BuildContext context) {
    return buildScaffold(() {
      final theme = Theme.of(context);
      final filtered = _search.isEmpty
          ? _players
          : _players.where((p) => p.displayName.toLowerCase().contains(_search.toLowerCase())).toList();

      return ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Players', style: theme.textTheme.headlineSmall),
          const SizedBox(height: 12),
          TextField(
            decoration: const InputDecoration(labelText: 'Search', prefixIcon: FaIcon(AppIcons.search, size: 16)),
            onChanged: (value) => setState(() => _search = value),
          ),
          const SizedBox(height: 12),
          for (final player in filtered)
            ListTile(
              key: ValueKey('ops-player-${player.id}'),
              title: Text(player.displayName),
              subtitle: Text('${player.companyCount} companies · ${player.cityNames.join(', ')}'),
              trailing: Text(player.personalCash.toStringAsFixed(0)),
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => Scaffold(appBar: AppBar(title: Text(player.displayName)), body: _PlayerDetailBody(player: player))),
              ),
            ),
        ],
      );
    });
  }
}

class OperationsPlayerDetailScreen extends _OperationsScreen {
  const OperationsPlayerDetailScreen({super.key, required this.playerId, super.graphQlService, super.operationsService});

  final String playerId;

  @override
  State<OperationsPlayerDetailScreen> createState() => _OperationsPlayerDetailScreenState();
}

class _OperationsPlayerDetailScreenState extends _OperationsScreenState<OperationsPlayerDetailScreen> {
  GameAdminPlayer? _player;

  @override
  Future<void> loadOperationsData() async {
    final dashboard = await service.fetchDashboard();
    for (final player in dashboard.players) {
      if (player.id == widget.playerId) {
        _player = player;
        break;
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return buildScaffold(() {
      final player = _player;
      if (player == null) return const Center(child: Text('Player not found.'));
      return _PlayerDetailBody(player: player);
    });
  }
}

class _PlayerDetailBody extends StatelessWidget {
  const _PlayerDetailBody({required this.player});

  final GameAdminPlayer player;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text(player.displayName, style: theme.textTheme.headlineSmall),
        Text(player.email, style: theme.textTheme.bodyMedium),
        const SizedBox(height: 16),
        Text('Role: ${player.role}'),
        Text('Personal cash: ${player.personalCash.toStringAsFixed(0)}'),
        Text('Company cash: ${player.totalCompanyCash.toStringAsFixed(0)}'),
        Text('Companies: ${player.companyCount}'),
        Text('Cities: ${player.cityNames.isEmpty ? '—' : player.cityNames.join(', ')}'),
        Text('Last login: ${player.lastLoginAtUtc ?? 'Never'}'),
      ],
    );
  }
}
