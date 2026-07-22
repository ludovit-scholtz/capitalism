import '../../core/config/app_config.dart';
import '../../core/graphql/graphql_service.dart';
import 'game_server_models.dart';

const _gameServersQuery = r'''
  query GetGameServers {
    gameServers {
      id serverKey displayName description region environment
      backendUrl graphqlUrl frontendUrl version
      playerCount companyCount currentTick
      registeredAtUtc lastHeartbeatAtUtc isOnline
    }
  }
''';

/// Fetches the list of registered game servers (shards) from the Master
/// API, matching `fetchGameServers()` in
/// `projects/master-frontend/src/lib/masterApi.ts`. Public — no auth
/// required, matching `Query.GetGameServers` on the backend.
class GameServerService {
  const GameServerService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<GameServerSummary>> fetchGameServers() async {
    final result = await _graphQlService.request(_gameServersQuery, endpoint: AppConfig.masterGraphqlUrl);
    final list = result['gameServers'] as List<dynamic>? ?? const [];
    return list.map((e) => GameServerSummary.fromJson(e as Map<String, dynamic>)).toList();
  }
}
