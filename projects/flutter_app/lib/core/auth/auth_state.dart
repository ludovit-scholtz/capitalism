import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Holds the player's Bearer JWT and exposes it to [GraphQlService].
///
/// The backend issues the same JWT via both an HttpOnly session cookie (for
/// the browser frontend) and the raw `token` field in the `login`/`register`
/// GraphQL response (see `projects/Api/Types/Mutation.Auth.cs`). Mobile has
/// no HttpOnly-cookie story, so this app takes the raw token and sends it as
/// `Authorization: Bearer <token>` instead — no backend changes required.
class AuthState extends ChangeNotifier {
  AuthState({FlutterSecureStorage? storage}) : _storage = storage ?? const FlutterSecureStorage();

  static const _tokenStorageKey = 'auth_token';

  final FlutterSecureStorage _storage;

  String? _token;
  bool _isAdmin = false;

  bool get isAuthenticated => _token != null && _token!.isNotEmpty;
  bool get isAdmin => _isAdmin;
  String? get token => _token;

  Future<void> restoreSession() async {
    _token = await _storage.read(key: _tokenStorageKey);
    notifyListeners();
  }

  Future<void> setToken(String token) async {
    _token = token;
    await _storage.write(key: _tokenStorageKey, value: token);
    notifyListeners();
  }

  /// Set once `gameAdminSession.canAccessAdminDashboard` is wired up to a
  /// real query; defaults to false so the Administration nav section stays
  /// hidden until that call is implemented.
  void setIsAdmin(bool value) {
    _isAdmin = value;
    notifyListeners();
  }

  Future<void> logout() async {
    _token = null;
    _isAdmin = false;
    await _storage.delete(key: _tokenStorageKey);
    notifyListeners();
  }
}
