import 'package:capitalism_app/core/auth/token_storage.dart';

/// [TokenStorage] fake for widget tests — avoids exercising the real
/// flutter_secure_storage platform channel, which isn't wired up under
/// `flutter test`.
class InMemoryTokenStorage implements TokenStorage {
  String? _value;

  @override
  Future<String?> read() async => _value;

  @override
  Future<void> write(String value) async => _value = value;

  @override
  Future<void> delete() async => _value = null;
}
