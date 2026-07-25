import 'package:capitalism_app/core/context/selected_building_storage.dart';

/// [SelectedBuildingStorage] fake for widget tests — avoids exercising the
/// real shared_preferences platform channel, which isn't wired up under
/// `flutter test`.
class InMemorySelectedBuildingStorage implements SelectedBuildingStorage {
  String? _value;

  @override
  Future<String?> read() async => _value;

  @override
  Future<void> write(String buildingId) async => _value = buildingId;
}
