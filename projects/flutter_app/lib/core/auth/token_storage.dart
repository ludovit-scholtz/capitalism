import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Where [AuthState] persists the player's JWT. Abstracted so tests can
/// swap in an in-memory fake instead of exercising the real
/// flutter_secure_storage platform channel (which isn't available under
/// `flutter test`).
///
/// Deliberately just the raw token — [AuthState] derives expiry and auth
/// provider by decoding the JWT's own `exp`/`iss` claims rather than
/// tracking them as separate persisted state, so a session stored before
/// that logic existed is handled identically to a freshly-issued one; see
/// `AuthState`'s doc comment.
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
  Future<String?> read() async {
    try {
      return await _storage.read(key: _key);
    } catch (_) {
      // Existing data can become undecryptable (e.g. a token written by an
      // older flutter_secure_storage whose legacy ciphers were removed), or
      // the keystore may be briefly unavailable. Treat both as "signed out"
      // for this launch rather than failing at startup. The stored value is
      // deliberately NOT deleted: a transient error must not wipe a valid
      // session, and a successful sign-in overwrites any unreadable value.
      return null;
    }
  }

  @override
  Future<void> write(String value) => _storage.write(key: _key, value: value);

  @override
  Future<void> delete() => _storage.delete(key: _key);
}
