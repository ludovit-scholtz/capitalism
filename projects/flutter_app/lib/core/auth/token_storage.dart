import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Where [AuthState] persists the player's JWT, its expiry, and which auth
/// provider issued it. The latter two are what let [AuthState] decide
/// whether/when a silent renewal is possible across app restarts.
/// Abstracted so tests can swap in an in-memory fake instead of exercising
/// the real flutter_secure_storage platform channel (which isn't available
/// under `flutter test`).
abstract class TokenStorage {
  Future<String?> read();
  Future<void> write(String value);
  Future<void> delete();

  Future<DateTime?> readExpiresAtUtc();
  Future<void> writeExpiresAtUtc(DateTime value);

  Future<String?> readProvider();
  Future<void> writeProvider(String value);
}

class SecureTokenStorage implements TokenStorage {
  SecureTokenStorage({FlutterSecureStorage? storage}) : _storage = storage ?? const FlutterSecureStorage();

  static const _key = 'auth_token';
  static const _expiresAtKey = 'auth_token_expires_at';
  static const _providerKey = 'auth_provider';

  final FlutterSecureStorage _storage;

  @override
  Future<String?> read() => _storage.read(key: _key);

  @override
  Future<void> write(String value) => _storage.write(key: _key, value: value);

  @override
  Future<void> delete() => Future.wait([
        _storage.delete(key: _key),
        _storage.delete(key: _expiresAtKey),
        _storage.delete(key: _providerKey),
      ]);

  @override
  Future<DateTime?> readExpiresAtUtc() async {
    final raw = await _storage.read(key: _expiresAtKey);
    if (raw == null) return null;
    return DateTime.tryParse(raw);
  }

  @override
  Future<void> writeExpiresAtUtc(DateTime value) =>
      _storage.write(key: _expiresAtKey, value: value.toIso8601String());

  @override
  Future<String?> readProvider() => _storage.read(key: _providerKey);

  @override
  Future<void> writeProvider(String value) => _storage.write(key: _providerKey, value: value);
}
