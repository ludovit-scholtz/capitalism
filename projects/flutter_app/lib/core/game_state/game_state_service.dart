import '../graphql/graphql_service.dart';
import 'game_state_model.dart';

const _gameStateQuery = r'''
  query NavGameState {
    gameState { currentTick lastTickAtUtc tickIntervalSeconds taxRate }
  }
''';

/// GraphQL call backing [GameStateState]'s polling, matching
/// `stores/gameState.ts`'s query (a subset of its fields — only what the
/// header's tick-progress indicator needs).
class GameStateService {
  const GameStateService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<GameStateModel?> fetchGameState() async {
    final result = await _graphQlService.request(_gameStateQuery);
    final gameState = result['gameState'] as Map<String, dynamic>?;
    if (gameState == null) return null;
    return GameStateModel.fromJson(gameState);
  }
}
