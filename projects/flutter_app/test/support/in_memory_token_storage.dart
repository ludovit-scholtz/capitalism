import 'package:capitalism_app/core/auth/token_storage.dart';

/// [TokenStorage] fake for widget tests — avoids exercising the real
/// flutter_secure_storage platform channel, which isn't wired up under
/// `flutter test`.
class InMemoryTokenStorage implements TokenStorage {
  String? _value;
  DateTime? _expiresAtUtc;
  String? _provider;

  @override
  Future<String?> read() async => _value;

  @override
  Future<void> write(String value) async => _value = value;

  @override
  Future<void> delete() async {
    _value = null;
    _expiresAtUtc = null;
    _provider = null;
  }

  @override
  Future<DateTime?> readExpiresAtUtc() async => _expiresAtUtc;

  @override
  Future<void> writeExpiresAtUtc(DateTime value) async => _expiresAtUtc = value;

  @override
  Future<String?> readProvider() async => _provider;

  @override
  Future<void> writeProvider(String value) async => _provider = value;
}
