import 'package:flutter/foundation.dart';

import 'game_state_model.dart';
import 'game_state_service.dart';

/// Holds the shared server tick clock. Refresh scheduling is owned by
/// `GameStatusBar`'s own lifecycle-bound timer (not a `Timer` here) so
/// polling stops the moment nothing is displaying it — e.g. on logout, or
/// when a widget test tears down its tree — rather than running forever in
/// the background once started, the way `stores/gameState.ts`'s
/// app-lifetime store does on the web.
class GameStateState extends ChangeNotifier {
  GameStateModel? gameState;
  bool loading = false;
  String? error;

  Future<void>? _inFlight;

  /// Fetches the latest game state. Concurrent calls share the same
  /// in-flight request unless [force] is set.
  Future<void> refresh(GameStateService service, {bool force = false}) {
    final inFlight = _inFlight;
    if (inFlight != null && !force) return inFlight;

    loading = true;
    error = null;
    notifyListeners();

    final future = _doRefresh(service);
    _inFlight = future;
    return future;
  }

  Future<void> _doRefresh(GameStateService service) async {
    try {
      gameState = await service.fetchGameState();
    } catch (_) {
      error = 'Could not load game state.';
    } finally {
      loading = false;
      _inFlight = null;
      notifyListeners();
    }
  }
}
