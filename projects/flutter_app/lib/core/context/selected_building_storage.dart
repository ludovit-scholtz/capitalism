import 'package:shared_preferences/shared_preferences.dart';

/// Where [RecentBuildingState] persists the player's most recently visited
/// building, so the bottom nav's "Last Used Building" tab still points
/// somewhere useful after an app restart. Purely a client-side navigation
/// convenience — there is no backend concept of a "current" building.
abstract class SelectedBuildingStorage {
  Future<String?> read();
  Future<void> write(String buildingId);
}

class SharedPreferencesSelectedBuildingStorage implements SelectedBuildingStorage {
  static const _key = 'last_visited_building_id';

  @override
  Future<String?> read() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_key);
  }

  @override
  Future<void> write(String buildingId) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, buildingId);
  }
}
