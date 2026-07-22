import 'package:capitalism_app/features/servers/game_server_models.dart';
import 'package:capitalism_app/features/servers/game_server_service.dart';

class FakeGameServerService implements GameServerService {
  FakeGameServerService({this.servers = const [], this.error});

  final List<GameServerSummary> servers;
  final Object? error;

  int fetchCallCount = 0;

  @override
  Future<List<GameServerSummary>> fetchGameServers() async {
    fetchCallCount++;
    if (error != null) throw error!;
    return servers;
  }
}
