// Ported from `projects/frontend/src/views/OperationsOverviewView.vue`,
// `OperationsStatisticsView.vue`, `OperationsAnalyticsView.vue`,
// `OperationsNewsView.vue`, `OperationsPlayersView.vue`, and
// `OperationsPlayerDetailView.vue`.
//
// These are sensitive server administration screens. All write actions
// below are real and GraphQL-backed (see `operations_service.dart`):
// player impersonation (`startAdminImpersonation`/`stopAdminImpersonation`,
// with a persistent "Impersonating X" banner shown on every Operations
// screen while active — `_ImpersonationBanner` below), granting/removing
// global admin roles and per-player local admin toggling (both gated on
// `session.isRootAdministrator`, matching the backend's
// `RequireRootAccessAsync`), NPC competitor pause/resume (on the Overview
// screen), manually ending the game shard (with a confirmation dialog and
// optional reason), player chat-visibility toggling, and a News
// compose/edit/publish form (`OperationsNewsScreen`/`operations_news_form.dart`).
//
// Still trimmed: no NPC decision-log panel, no CSV export of product
// analytics — both read-only conveniences with no write-safety concerns,
// left for a future pass.
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
import 'operations_end_shard_card.dart';
import 'operations_models.dart';
import 'operations_news_form.dart';
import 'operations_npc_panel.dart';
import 'operations_player_detail_body.dart';
import 'operations_service.dart';

abstract class OperationsScreen extends StatefulWidget {
  const OperationsScreen({super.key, this.graphQlService, this.operationsService});

  final GraphQlService? graphQlService;
  final OperationsService? operationsService;
}

abstract class OperationsScreenState<T extends OperationsScreen> extends State<T> {
  late final OperationsService service;
  late final AuthState authState;

  bool loading = true;
  String? error;
  bool canAccess = false;
  GameAdminSessionInfo? session;

  @override
  void initState() {
    super.initState();
    authState = context.read<AuthState>();
    final graphQlService = widget.graphQlService ?? GraphQlService(authState);
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
      final fetchedSession = await service.fetchSession();
      if (!fetchedSession.canAccessAdminDashboard) {
        if (!mounted) return;
        setState(() {
          canAccess = false;
          session = fetchedSession;
          loading = false;
        });
        return;
      }
      await loadOperationsData();
      if (!mounted) return;
      setState(() {
        canAccess = true;
        session = fetchedSession;
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

  Future<void> _stopImpersonating() async {
    try {
      final token = await service.stopImpersonation();
      await authState.setToken(token);
      if (mounted) await load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not stop impersonating.')));
      }
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
    final activeSession = session;
    return Column(
      children: [
        if (activeSession != null && activeSession.isImpersonating)
          _ImpersonationBanner(session: activeSession, onStop: _stopImpersonating),
        Expanded(child: buildContent()),
      ],
    );
  }
}

class _ImpersonationBanner extends StatelessWidget {
  const _ImpersonationBanner({required this.session, required this.onStop});

  final GameAdminSessionInfo session;
  final VoidCallback onStop;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.amber.shade700,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        child: Row(
          children: [
            const Icon(Icons.visibility, size: 16, color: Colors.black),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                'Impersonating ${session.effectivePlayerDisplayName ?? 'player'} as ${session.adminActorDisplayName ?? 'admin'}',
                style: const TextStyle(color: Colors.black, fontWeight: FontWeight.w600),
              ),
            ),
            TextButton(
              key: const ValueKey('stop-impersonating-button'),
              onPressed: onStop,
              child: const Text('Stop', style: TextStyle(color: Colors.black, fontWeight: FontWeight.bold)),
            ),
          ],
        ),
      ),
    );
  }
}

class OperationsOverviewScreen extends OperationsScreen {
  const OperationsOverviewScreen({super.key, super.graphQlService, super.operationsService});

  @override
  State<OperationsOverviewScreen> createState() => _OperationsOverviewScreenState();
}

class _OperationsOverviewScreenState extends OperationsScreenState<OperationsOverviewScreen> {
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
          const SizedBox(height: 16),
          OperationsNpcPanel(service: service),
          const SizedBox(height: 16),
          OperationsEndShardCard(service: service),
        ],
      );
    });
  }
}

class OperationsMoneyFlowScreen extends OperationsScreen {
  const OperationsMoneyFlowScreen({super.key, super.graphQlService, super.operationsService});

  @override
  State<OperationsMoneyFlowScreen> createState() => _OperationsMoneyFlowScreenState();
}

class _OperationsMoneyFlowScreenState extends OperationsScreenState<OperationsMoneyFlowScreen> {
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

class OperationsProductAnalyticsScreen extends OperationsScreen {
  const OperationsProductAnalyticsScreen({super.key, super.graphQlService, super.operationsService});

  @override
  State<OperationsProductAnalyticsScreen> createState() => _OperationsProductAnalyticsScreenState();
}

class _OperationsProductAnalyticsScreenState extends OperationsScreenState<OperationsProductAnalyticsScreen> {
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

class OperationsNewsScreen extends OperationsScreen {
  const OperationsNewsScreen({super.key, super.graphQlService, super.operationsService});

  @override
  State<OperationsNewsScreen> createState() => _OperationsNewsScreenState();
}

class _OperationsNewsScreenState extends OperationsScreenState<OperationsNewsScreen> {
  List<AdminNewsEntry> _entries = const [];

  @override
  Future<void> loadOperationsData() async {
    _entries = await service.fetchNewsFeed();
  }

  Future<void> _openEditor({AdminNewsEntry? entry}) async {
    final saved = await showNewsEntryEditor(context, service: service, entry: entry);
    if (saved == true) await load();
  }

  @override
  Widget build(BuildContext context) {
    return buildScaffold(() {
      final theme = Theme.of(context);
      return ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Row(
            children: [
              Expanded(child: Text('News Manager', style: theme.textTheme.headlineSmall)),
              FilledButton.icon(
                key: const ValueKey('new-news-entry-button'),
                onPressed: () => _openEditor(),
                icon: const Icon(Icons.add),
                label: const Text('New entry'),
              ),
            ],
          ),
          const SizedBox(height: 12),
          for (final entry in _entries)
            Card(
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(entry.localizationFor('en')?.title ?? '(untitled)'),
                subtitle: Text(entry.entryType),
                trailing: Chip(label: Text(entry.status)),
                onTap: () => _openEditor(entry: entry),
              ),
            ),
        ],
      );
    });
  }
}

class OperationsPlayersScreen extends OperationsScreen {
  const OperationsPlayersScreen({super.key, super.graphQlService, super.operationsService});

  @override
  State<OperationsPlayersScreen> createState() => _OperationsPlayersScreenState();
}

class _OperationsPlayersScreenState extends OperationsScreenState<OperationsPlayersScreen> {
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
                MaterialPageRoute(
                  builder: (_) => Scaffold(
                    appBar: AppBar(title: Text(player.displayName)),
                    body: PlayerDetailBody(
                      player: player,
                      service: service,
                      authState: authState,
                      isRootAdministrator: session?.isRootAdministrator ?? false,
                      onChanged: load,
                    ),
                  ),
                ),
              ),
            ),
        ],
      );
    });
  }
}

class OperationsPlayerDetailScreen extends OperationsScreen {
  const OperationsPlayerDetailScreen({super.key, required this.playerId, super.graphQlService, super.operationsService});

  final String playerId;

  @override
  State<OperationsPlayerDetailScreen> createState() => _OperationsPlayerDetailScreenState();
}

class _OperationsPlayerDetailScreenState extends OperationsScreenState<OperationsPlayerDetailScreen> {
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
      return PlayerDetailBody(
        player: player,
        service: service,
        authState: authState,
        isRootAdministrator: session?.isRootAdministrator ?? false,
        onChanged: load,
      );
    });
  }
}
