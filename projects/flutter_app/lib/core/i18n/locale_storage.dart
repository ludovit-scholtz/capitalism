import 'package:shared_preferences/shared_preferences.dart';

/// Where [LocaleState] persists the player's chosen app language so the
/// selection survives app restarts. Abstracted so tests can swap in an
/// in-memory fake instead of exercising the real shared_preferences platform
/// channel (mirrors `SelectedEnvironmentStorage`). Uses the same
/// `SharedPreferences` key name as the web's `LOCALE_STORAGE_KEY`
/// (`localStorage["app_locale"]`) purely for cross-client convention, not
/// because state is shared between them.
abstract class SelectedLocaleStorage {
  Future<String?> read();
  Future<void> write(String languageCode);
  Future<void> clear();
}

class SharedPreferencesSelectedLocaleStorage implements SelectedLocaleStorage {
  static const _localeKey = 'app_locale';

  @override
  Future<String?> read() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_localeKey);
  }

  @override
  Future<void> write(String languageCode) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_localeKey, languageCode);
  }

  @override
  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_localeKey);
  }
}
