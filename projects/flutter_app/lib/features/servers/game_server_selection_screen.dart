// New screen — the web frontend has no equivalent (only
// `projects/master-frontend/src/views/GameServersView.vue` does, which just
// links out to each server's own frontend). Mobile has no per-shard build
// flavor story, so this screen lets the player pick which registered game
// server (shard) to play on; the choice is persisted by `GameServerState`
// and becomes the active `AppConfig.graphqlUrl` for all subsequent game API
// calls (see `core/graphql/graphql_service.dart`).

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/config/game_server_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/icon_badge.dart';
import 'game_server_models.dart';
import 'game_server_service.dart';

class GameServerSelectionScreen extends StatefulWidget {
  const GameServerSelectionScreen({super.key, GraphQlService? graphQlService, GameServerService? gameServerService})
    : _injectedGraphQlService = graphQlService,
      _injectedGameServerService = gameServerService;

  final GraphQlService? _injectedGraphQlService;
  final GameServerService? _injectedGameServerService;

  @override
  State<GameServerSelectionScreen> createState() => _GameServerSelectionScreenState();
}

class _GameServerSelectionScreenState extends State<GameServerSelectionScreen> {
  late final GameServerService _service;

  bool _loading = true;
  String? _error;
  List<GameServerSummary> _servers = const [];

  @override
  void initState() {
    super.initState();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(context.read<AuthState>());
    _service = widget._injectedGameServerService ?? GameServerService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final servers = await _service.fetchGameServers();
      if (!mounted) return;
      setState(() {
        _servers = servers;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the server list. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _select(GameServerSummary server) async {
    await context.read<GameServerState>().selectServer(server);
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Connected to ${server.displayName}.')));
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

    final selectedKey = context.watch<GameServerState>().selectedServerKey;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Game Servers', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text(
            'Choose which server (shard) to play on.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 16),
          if (_servers.isEmpty) const Text('No game servers are registered right now.'),
          for (final server in _servers)
            _GameServerCard(
              server: server,
              isSelected: server.serverKey == selectedKey,
              onSelect: () => _select(server),
            ),
        ],
      ),
    );
  }
}

class _GameServerCard extends StatelessWidget {
  const _GameServerCard({required this.server, required this.isSelected, required this.onSelect});

  final GameServerSummary server;
  final bool isSelected;
  final VoidCallback onSelect;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      key: ValueKey('game-server-${server.serverKey}'),
      margin: const EdgeInsets.only(bottom: 12),
      color: isSelected ? theme.colorScheme.primaryContainer.withValues(alpha: 0.35) : null,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                IconBadge(
                  icon: AppIcons.server,
                  size: 36,
                  iconSize: 16,
                  color: server.isOnline ? AppTheme.neonGreen : AppTheme.mutedText,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(server.displayName, style: theme.textTheme.titleMedium),
                      if (server.description.isNotEmpty) Text(server.description, style: theme.textTheme.bodySmall),
                    ],
                  ),
                ),
                if (isSelected)
                  const Chip(label: Text('Connected'), avatar: FaIcon(AppIcons.check, size: 12)),
              ],
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                Chip(label: Text(server.isOnline ? 'Online' : 'Offline')),
                if (server.region.isNotEmpty) Chip(label: Text(server.region)),
                if (server.environment.isNotEmpty) Chip(label: Text(server.environment)),
                if (server.version.isNotEmpty) Chip(label: Text('v${server.version}')),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              '${server.playerCount} players · ${server.companyCount} companies · tick ${server.currentTick}',
              style: theme.textTheme.bodySmall,
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: isSelected
                  ? const OutlinedButton(onPressed: null, child: Text('Currently connected'))
                  : FilledButton(onPressed: onSelect, child: const Text('Connect')),
            ),
          ],
        ),
      ),
    );
  }
}
