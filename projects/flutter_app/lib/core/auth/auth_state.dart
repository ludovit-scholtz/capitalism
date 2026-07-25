import 'package:flutter/foundation.dart';

import '../services/app_logger.dart';
import 'biatec_oidc_service.dart';
import 'token_storage.dart';

/// Mirrors the web store's `auth_provider` values (`stores/auth.ts`,
/// `AUTH_PROVIDER_LOCAL` / `AUTH_PROVIDER_BIATEC`) — only Biatec-OIDC
/// sessions currently support silent renewal, see [AuthState.ensureFreshToken].
abstract class AuthProvider {
  static const local = 'local';
  static const biatecOidc = 'biatec_oidc';
}

/// How long before expiry a renewal is attempted, mirroring the web store's
/// `TOKEN_RENEW_BEFORE_MS`.
const _renewBeforeExpiry = Duration(seconds: 60);

/// Holds the player's Bearer JWT and exposes it to [GraphQlService].
///
/// The backend issues the same JWT via both an HttpOnly session cookie (for
/// the browser frontend) and the raw `token` field in the `login`/`register`
/// GraphQL response (see `projects/Api/Types/Mutation.Auth.cs`). Mobile has
/// no HttpOnly-cookie story, so this app takes the raw token and sends it as
/// `Authorization: Bearer <token>` instead — no backend changes required.
///
/// Also tracks token expiry and, for Biatec-OIDC sessions, transparently
/// renews the token when it's at/near expiry — see [ensureFreshToken] and
/// `docs/oidc-refresh-gap.md` for why local (email/password) sessions can't
/// be renewed the same way. Renewal is checked on demand (once per
/// [GraphQlService] request) rather than on a background `Timer`: this app
/// has no reliable single owner to `dispose()` a long-lived `AuthState`
/// (it's a `provider`-scoped singleton for the app's lifetime, and widget
/// tests construct/discard many short-lived instances), so a background
/// timer would either leak past disposal or need to be threaded through
/// every call site — an on-demand check has no such lifecycle to manage.
class AuthState extends ChangeNotifier {
  AuthState({TokenStorage? storage, BiatecOidcService? oidcService})
      : _storage = storage ?? SecureTokenStorage(),
        _oidcService = oidcService ?? const BiatecOidcService();

  final TokenStorage _storage;
  final BiatecOidcService _oidcService;

  String? _token;
  bool _isAdmin = false;
  DateTime? _expiresAtUtc;
  String _provider = AuthProvider.local;
  Future<void>? _pendingRenewal;

  bool get isAuthenticated => _token != null && _token!.isNotEmpty;
  bool get isAdmin => _isAdmin;
  String? get token => _token;
  DateTime? get expiresAtUtc => _expiresAtUtc;
  String get provider => _provider;

  bool get isTokenExpiringSoon {
    final expiry = _expiresAtUtc;
    if (expiry == null) return false;
    return !expiry.isAfter(DateTime.now().toUtc().add(_renewBeforeExpiry));
  }

  Future<void> restoreSession() async {
    _token = await _storage.read();
    _expiresAtUtc = await _storage.readExpiresAtUtc();
    _provider = await _storage.readProvider() ?? AuthProvider.local;
    notifyListeners();
  }

  /// [expiresAtUtc]/[provider] should always be supplied for real sessions
  /// (the GraphQL `login`/`register`/OIDC responses all carry an expiry);
  /// they default to "no expiry tracked, local provider" only to keep
  /// existing single-argument call sites (mostly tests) compiling.
  Future<void> setToken(String token, {DateTime? expiresAtUtc, String provider = AuthProvider.local}) async {
    _token = token;
    _expiresAtUtc = expiresAtUtc;
    _provider = provider;
    await _storage.write(token);
    if (expiresAtUtc != null) {
      await _storage.writeExpiresAtUtc(expiresAtUtc);
    }
    await _storage.writeProvider(provider);
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
    _expiresAtUtc = null;
    _provider = AuthProvider.local;
    await _storage.delete();
    notifyListeners();
  }

  /// Awaited by [GraphQlService] immediately before sending every request.
  /// If the session is Biatec-OIDC-backed and at/near expiry, attempts one
  /// silent (`prompt=none`) renewal and waits for it, so the request goes
  /// out with a fresh token. Concurrent callers share the same in-flight
  /// renewal via [_pendingRenewal] instead of each starting their own OIDC
  /// round trip. No-ops for local sessions — there is no refresh mechanism
  /// for them yet, see `docs/oidc-refresh-gap.md`.
  Future<void> ensureFreshToken() async {
    if (_provider != AuthProvider.biatecOidc || !isAuthenticated) return;
    if (!isTokenExpiringSoon) return;
    await _renewSilently();
  }

  Future<void> _renewSilently() {
    return _pendingRenewal ??= _doRenewSilently().whenComplete(() => _pendingRenewal = null);
  }

  Future<void> _doRenewSilently() async {
    try {
      final result = await _oidcService.signIn(silent: true);
      await setToken(result.token, expiresAtUtc: result.expiresAtUtc, provider: AuthProvider.biatecOidc);
      AppLogger.instance.info(
        'Silent token renewal succeeded, new expiry ${result.expiresAtUtc.toIso8601String()}',
        tag: 'OIDC',
      );
    } catch (e) {
      // Silent renewal can legitimately fail (provider session expired, user
      // revoked consent, offline). Deliberately not retried in a loop here —
      // the next GraphQL request will hit UNAUTHENTICATED, GraphQlService
      // will force a logout, and the player is sent back to interactive
      // sign-in instead.
      AppLogger.instance.warning('Silent token renewal failed: $e', tag: 'OIDC');
    }
  }
}
