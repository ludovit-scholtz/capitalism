import 'package:capitalism_app/core/config/app_environment.dart';
import 'package:capitalism_app/core/config/selected_environment_storage.dart';

/// [SelectedEnvironmentStorage] fake for widget tests — avoids exercising
/// the real shared_preferences platform channel, which isn't wired up under
/// `flutter test`.
class InMemorySelectedEnvironmentStorage implements SelectedEnvironmentStorage {
  AppEnvironment? _value;

  @override
  Future<AppEnvironment?> read() async => _value;

  @override
  Future<void> write(AppEnvironment environment) async => _value = environment;

  @override
  Future<void> clear() async => _value = null;
}
