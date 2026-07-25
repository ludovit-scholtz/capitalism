import 'package:flutter/foundation.dart';

import 'selected_building_storage.dart';

/// Tracks the last building the player opened (`BuildingDetailScreen`), so
/// the bottom nav's "Last Used Building" tab can jump straight back to it.
class RecentBuildingState extends ChangeNotifier {
  RecentBuildingState({SelectedBuildingStorage? storage}) : _storage = storage ?? SharedPreferencesSelectedBuildingStorage();

  final SelectedBuildingStorage _storage;

  String? _lastBuildingId;

  String? get lastBuildingId => _lastBuildingId;

  Future<void> restoreLastBuilding() async {
    final saved = await _storage.read();
    if (saved == null) return;
    _lastBuildingId = saved;
    notifyListeners();
  }

  Future<void> recordVisit(String buildingId) async {
    if (_lastBuildingId == buildingId) return;
    _lastBuildingId = buildingId;
    notifyListeners();
    await _storage.write(buildingId);
  }
}
