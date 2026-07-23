import 'package:capitalism_app/core/context/selected_city_storage.dart';

/// [SelectedCityStorage] fake for widget tests — avoids exercising the real
/// shared_preferences platform channel, which isn't wired up under
/// `flutter test`.
class InMemorySelectedCityStorage implements SelectedCityStorage {
  String? _value;

  @override
  Future<String?> read() async => _value;

  @override
  Future<void> write(String cityId) async => _value = cityId;

  @override
  Future<void> clear() async => _value = null;
}
