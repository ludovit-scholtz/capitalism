import 'package:flutter/foundation.dart';

import '../../features/servers/game_server_models.dart';
import '../../features/servers/game_server_service.dart';
import '../services/app_logger.dart';
import 'app_config.dart';
import 'selected_game_server_storage.dart';

/// Holds which game server (shard) the player is currently connected to and
/// keeps [AppConfig.graphqlUrl] in sync with it. Mirrors the web's
/// per-container `VITE_GRAPHQL_URL` — since mobile has no equivalent
/// build-per-shard story, the player instead picks a server on
/// [GameServerSelectionScreen] (fetched from the Master API's `gameServers`
/// query) and that choice is persisted here.
class GameServerState extends ChangeNotifier {
  GameServerState({SelectedGameServerStorage? storage}) : _storage = storage ?? SharedPreferencesSelectedGameServerStorage();

  final SelectedGameServerStorage _storage;

  String? _selectedServerKey;
  String? _selectedDisplayName;

  String? get selectedServerKey => _selectedServerKey;
  String? get selectedDisplayName => _selectedDisplayName;
  bool get hasSelection => _selectedServerKey != null;

  Future<void> restoreSelection() async {
    final saved = await _storage.read();
    if (saved == null) return;
    _selectedServerKey = saved.serverKey;
    _selectedDisplayName = saved.displayName;
    AppConfig.setGraphqlUrl(saved.graphqlUrl);
    notifyListeners();
  }

  Future<void> selectServer(GameServerSummary server) async {
    _selectedServerKey = server.serverKey;
    _selectedDisplayName = server.displayName;
    AppConfig.setGraphqlUrl(server.graphqlUrl);
    await _storage.write(
      SelectedGameServer(serverKey: server.serverKey, displayName: server.displayName, graphqlUrl: server.graphqlUrl),
    );
    notifyListeners();
  }

  Future<void> clearSelection() async {
    _selectedServerKey = null;
    _selectedDisplayName = null;
    AppConfig.resetGraphqlUrl();
    await _storage.clear();
    notifyListeners();
  }

  /// Fetches the registered server list from the current environment's
  /// Master API and, if no shard is already selected, connects to the
  /// first online one (or just the first, if none report online) — so the
  /// app has a working game endpoint out of the box instead of falling
  /// back to a build-time default that may not exist in this environment
  /// (see `main.dart` and `AboutScreen`'s environment switcher). A no-op if
  /// a shard is already selected, unless [force] is set (used right after
  /// switching environments, when any existing selection belongs to the
  /// *previous* environment's registry and must be replaced).
  Future<void> autoSelectFirstAvailable(GameServerService service, {bool force = false}) async {
    if (hasSelection && !force) return;
    try {
      final servers = await service.fetchGameServers().timeout(const Duration(seconds: 8));
      if (servers.isEmpty) {
        AppLogger.instance.warning('No game servers registered for this environment', tag: 'GameServer');
        return;
      }
      final chosen = servers.firstWhere((s) => s.isOnline, orElse: () => servers.first);
      await selectServer(chosen);
      AppLogger.instance.info('Auto-connected to ${chosen.displayName} (${chosen.graphqlUrl})', tag: 'GameServer');
    } catch (e, stackTrace) {
      AppLogger.instance.error('Auto-select of a game server failed', e, stackTrace, 'GameServer');
    }
  }
}
