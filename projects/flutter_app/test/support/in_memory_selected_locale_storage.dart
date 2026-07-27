import 'package:capitalism_app/core/i18n/locale_storage.dart';

/// [SelectedLocaleStorage] fake for widget tests — avoids exercising the
/// real shared_preferences platform channel, which isn't wired up under
/// `flutter test`.
class InMemorySelectedLocaleStorage implements SelectedLocaleStorage {
  String? _value;

  @override
  Future<String?> read() async => _value;

  @override
  Future<void> write(String languageCode) async => _value = languageCode;

  @override
  Future<void> clear() async => _value = null;
}
