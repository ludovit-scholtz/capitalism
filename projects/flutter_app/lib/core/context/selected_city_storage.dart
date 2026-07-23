import 'package:shared_preferences/shared_preferences.dart';

/// Where [AccountContextState] persists the player's chosen "current city".
/// Mirrors the web's `setStoredCityId`/`localStorage` — city selection has
/// no backend concept (see `Api/Data/Entities/Player.cs`'s
/// `AccountContextType`, which only tracks person/company, not city), so
/// this is purely a client-side navigation convenience, same as the web.
abstract class SelectedCityStorage {
  Future<String?> read();
  Future<void> write(String cityId);
  Future<void> clear();
}

class SharedPreferencesSelectedCityStorage implements SelectedCityStorage {
  static const _key = 'selected_city_id';

  @override
  Future<String?> read() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_key);
  }

  @override
  Future<void> write(String cityId) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, cityId);
  }

  @override
  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_key);
  }
}
