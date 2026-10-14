import 'package:capitalism_app/core/auth/token_storage.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

/// Stand-in for a secure store whose existing data became unreadable (e.g. a
/// token written by flutter_secure_storage 9 after jumping straight to 11,
/// which dropped the legacy Android ciphers).
class _UnreadableStorage implements FlutterSecureStorage {
  bool deleted = false;

  @override
  Future<String?> read(
          {required String key,
          dynamic iOptions,
          dynamic aOptions,
          dynamic lOptions,
          dynamic webOptions,
          dynamic mOptions,
          dynamic wOptions}) =>
      throw Exception('cannot decrypt');

  @override
  Future<void> delete(
      {required String key,
      dynamic iOptions,
      dynamic aOptions,
      dynamic lOptions,
      dynamic webOptions,
      dynamic mOptions,
      dynamic wOptions}) async {
    deleted = true;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  test('an unreadable stored token reads as signed out and is cleared, not thrown', () async {
    final raw = _UnreadableStorage();
    final storage = SecureTokenStorage(storage: raw);

    expect(await storage.read(), isNull);
    expect(raw.deleted, isTrue);
  });
}
