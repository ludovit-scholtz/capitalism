import 'package:flutter/foundation.dart';

import 'app_config.dart';
import 'app_environment.dart';
import 'selected_environment_storage.dart';

/// Holds which deployment ([AppEnvironment]) the app talks to and keeps
/// [AppConfig.environment] in sync with it — the runtime-switchable
/// counterpart to the `--dart-define=APP_ENVIRONMENT=...` build seed,
/// surfaced on the About screen. Mirrors [GameServerState]'s
/// restore-then-notify shape.
///
/// Switching environments only updates which Master API the app talks to
/// and resets the active game endpoint to that environment's default (see
/// [AppConfig.setEnvironment]) — it does not clear the previously selected
/// game server on its own. Callers (the About screen) are expected to also
/// call `GameServerState.clearSelection()` in the same action, since a
/// shard selected under one environment's Master API registry is not valid
/// under another's.
class AppEnvironmentState extends ChangeNotifier {
  AppEnvironmentState({SelectedEnvironmentStorage? storage})
    : _storage = storage ?? SharedPreferencesSelectedEnvironmentStorage();

  final SelectedEnvironmentStorage _storage;

  AppEnvironment get environment => AppConfig.environment;

  Future<void> restoreSelection() async {
    final saved = await _storage.read();
    if (saved == null) return;
    AppConfig.setEnvironment(saved);
    notifyListeners();
  }

  Future<void> setEnvironment(AppEnvironment value) async {
    AppConfig.setEnvironment(value);
    await _storage.write(value);
    notifyListeners();
  }
}
