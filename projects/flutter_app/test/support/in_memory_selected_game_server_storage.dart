import 'package:capitalism_app/core/config/selected_game_server_storage.dart';

/// [SelectedGameServerStorage] fake for widget tests — avoids exercising the
/// real shared_preferences platform channel, which isn't wired up under
/// `flutter test`.
class InMemorySelectedGameServerStorage implements SelectedGameServerStorage {
  SelectedGameServer? _value;

  @override
  Future<SelectedGameServer?> read() async => _value;

  @override
  Future<void> write(SelectedGameServer server) async => _value = server;

  @override
  Future<void> clear() async => _value = null;
}
