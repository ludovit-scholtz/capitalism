import 'package:shared_preferences/shared_preferences.dart';

/// Where [GameServerState] persists the player's chosen game server so the
/// selection survives app restarts. Abstracted so tests can swap in an
/// in-memory fake instead of exercising the real shared_preferences platform
/// channel (mirrors the [TokenStorage] pattern used for the auth token).
abstract class SelectedGameServerStorage {
  Future<SelectedGameServer?> read();
  Future<void> write(SelectedGameServer server);
  Future<void> clear();
}

class SelectedGameServer {
  const SelectedGameServer({required this.serverKey, required this.displayName, required this.graphqlUrl});

  final String serverKey;
  final String displayName;
  final String graphqlUrl;
}

class SharedPreferencesSelectedGameServerStorage implements SelectedGameServerStorage {
  static const _serverKeyKey = 'selected_game_server_key';
  static const _displayNameKey = 'selected_game_server_display_name';
  static const _graphqlUrlKey = 'selected_game_server_graphql_url';

  @override
  Future<SelectedGameServer?> read() async {
    final prefs = await SharedPreferences.getInstance();
    final serverKey = prefs.getString(_serverKeyKey);
    final displayName = prefs.getString(_displayNameKey);
    final graphqlUrl = prefs.getString(_graphqlUrlKey);
    if (serverKey == null || displayName == null || graphqlUrl == null) return null;
    return SelectedGameServer(serverKey: serverKey, displayName: displayName, graphqlUrl: graphqlUrl);
  }

  @override
  Future<void> write(SelectedGameServer server) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_serverKeyKey, server.serverKey);
    await prefs.setString(_displayNameKey, server.displayName);
    await prefs.setString(_graphqlUrlKey, server.graphqlUrl);
  }

  @override
  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_serverKeyKey);
    await prefs.remove(_displayNameKey);
    await prefs.remove(_graphqlUrlKey);
  }
}
