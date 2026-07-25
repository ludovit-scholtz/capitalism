import 'package:capitalism_app/core/game_state/game_state_model.dart';
import 'package:capitalism_app/core/game_state/game_state_service.dart';

class FakeGameStateService implements GameStateService {
  FakeGameStateService({
    GameStateModel? gameState,
    this.error,
  }) : gameState =
           gameState ??
           GameStateModel(currentTick: 1234, lastTickAtUtc: DateTime.now().toUtc(), tickIntervalSeconds: 10, taxRate: 15);

  GameStateModel? gameState;
  final Object? error;

  int fetchCount = 0;

  @override
  Future<GameStateModel?> fetchGameState() async {
    fetchCount++;
    if (error != null) throw error!;
    return gameState;
  }
}
