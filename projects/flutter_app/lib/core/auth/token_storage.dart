import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Where [AuthState] persists the player's JWT. Abstracted so tests can
/// swap in an in-memory fake instead of exercising the real
/// flutter_secure_storage platform channel (which isn't available under
/// `flutter test`).
abstract class TokenStorage {
  Future<String?> read();
  Future<void> write(String value);
  Future<void> delete();
}

class SecureTokenStorage implements TokenStorage {
  SecureTokenStorage({FlutterSecureStorage? storage}) : _storage = storage ?? const FlutterSecureStorage();

  static const _key = 'auth_token';

  final FlutterSecureStorage _storage;

  @override
  Future<String?> read() => _storage.read(key: _key);

  @override
  Future<void> write(String value) => _storage.write(key: _key, value: value);

  @override
  Future<void> delete() => _storage.delete(key: _key);
}
