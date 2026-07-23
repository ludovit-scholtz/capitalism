import 'package:shared_preferences/shared_preferences.dart';

import 'app_environment.dart';

/// Where [AppEnvironmentState] persists the player's chosen environment so
/// the selection survives app restarts. Abstracted so tests can swap in an
/// in-memory fake instead of exercising the real shared_preferences platform
/// channel (mirrors [SelectedGameServerStorage]).
abstract class SelectedEnvironmentStorage {
  Future<AppEnvironment?> read();
  Future<void> write(AppEnvironment environment);
  Future<void> clear();
}

class SharedPreferencesSelectedEnvironmentStorage implements SelectedEnvironmentStorage {
  static const _environmentKey = 'selected_app_environment';

  @override
  Future<AppEnvironment?> read() async {
    final prefs = await SharedPreferences.getInstance();
    final name = prefs.getString(_environmentKey);
    if (name == null) return null;
    for (final environment in AppEnvironment.values) {
      if (environment.name == name) return environment;
    }
    return null;
  }

  @override
  Future<void> write(AppEnvironment environment) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_environmentKey, environment.name);
  }

  @override
  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_environmentKey);
  }
}
