import 'package:flutter/foundation.dart';

import '../../features/servers/game_server_models.dart';
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
}
