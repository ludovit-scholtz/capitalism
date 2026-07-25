import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../services/app_logger.dart';
import 'biatec_oidc_config.dart';
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

/// `exp`/`iss` pulled out of the Bearer JWT's own payload — deliberately not
/// trusted from anywhere else. See [AuthState] doc comment for why.
class _JwtClaims {
  const _JwtClaims({this.expiresAtUtc, this.issuer});

  final DateTime? expiresAtUtc;
  final String? issuer;

  static _JwtClaims decode(String token) {
    try {
      final parts = token.split('.');
      if (parts.length != 3) return const _JwtClaims();
      final normalized = base64Url.normalize(parts[1]);
      final payload = jsonDecode(utf8.decode(base64Url.decode(normalized))) as Map<String, dynamic>;
      final exp = payload['exp'];
      return _JwtClaims(
        expiresAtUtc: exp is int ? DateTime.fromMillisecondsSinceEpoch(exp * 1000, isUtc: true) : null,
        issuer: payload['iss'] as String?,
      );
    } catch (_) {
      // Not a JWT (e.g. a test fixture string like 'test-token') — treated
      // as "no expiry known, provider unknown" rather than an error.
      return const _JwtClaims();
    }
  }
}

/// Holds the player's Bearer JWT and exposes it to [GraphQlService].
///
/// The backend issues the same JWT via both an HttpOnly session cookie (for
/// the browser frontend) and the raw `token` field in the `login`/`register`
/// GraphQL response (see `projects/Api/Types/Mutation.Auth.cs`). Mobile has
/// no HttpOnly-cookie story, so this app takes the raw token and sends it as
/// `Authorization: Bearer <token>` instead — no backend changes required.
///
/// Also tracks token expiry and which provider issued it, and for
/// Biatec-OIDC sessions transparently renews the token when it's at/near
/// expiry — see [ensureFreshToken] and `docs/oidc-refresh-gap.md` for why
/// local (email/password) sessions can't be renewed the same way.
///
/// Expiry and provider are derived by decoding the JWT's own `exp`/`iss`
/// claims (see [_JwtClaims]) rather than tracked as separate state the
/// caller has to remember to pass in. This matters for sessions stored
/// before this renewal logic existed: on `restoreSession()` there is no
/// separately-persisted expiry/provider to read, but the *token itself*
/// already carries both, so a session restored from a week-old
/// `flutter_secure_storage` value is immediately treated exactly the same
/// as a freshly-issued one — no migration step needed.
///
/// Renewal is checked on demand (once per [GraphQlService] request) rather
/// than on a background `Timer`: this app has no reliable single owner to
/// `dispose()` a long-lived `AuthState` (it's a `provider`-scoped singleton
/// for the app's lifetime, and widget tests construct/discard many
/// short-lived instances), so a background timer would either leak past
/// disposal or need to be threaded through every call site — an on-demand
/// check has no such lifecycle to manage.
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
    final token = await _storage.read();
    _token = token;
    _applyClaims(token);
    if (token != null) {
      AppLogger.instance.info(
        'Session restored: provider=$_provider, expiresAtUtc=${_expiresAtUtc?.toIso8601String() ?? 'unknown'}',
        tag: 'Auth',
      );
    }
    notifyListeners();
  }

  Future<void> setToken(String token) async {
    _token = token;
    _applyClaims(token);
    await _storage.write(token);
    notifyListeners();
  }

  void _applyClaims(String? token) {
    if (token == null) {
      _expiresAtUtc = null;
      _provider = AuthProvider.local;
      return;
    }
    final claims = _JwtClaims.decode(token);
    _expiresAtUtc = claims.expiresAtUtc;
    _provider = claims.issuer != null && BiatecOidcConfig.allowedIssuers.contains(claims.issuer)
        ? AuthProvider.biatecOidc
        : AuthProvider.local;
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
  /// If the session is Biatec-OIDC-backed (per the token's own `iss` claim)
  /// and at/near expiry (per its `exp` claim), attempts one silent
  /// (`prompt=none`) renewal and waits for it, so the request goes out with
  /// a fresh token. Concurrent callers share the same in-flight renewal via
  /// [_pendingRenewal] instead of each starting their own OIDC round trip.
  /// No-ops for local sessions — there is no refresh mechanism for them
  /// yet, see `docs/oidc-refresh-gap.md`.
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
      await setToken(result.token);
      AppLogger.instance.info(
        'Silent token renewal succeeded, new expiry ${result.expiresAtUtc.toIso8601String()}',
        tag: 'OIDC',
      );
    } catch (e) {
      // Silent renewal can legitimately fail (provider session expired, user
      // revoked consent, offline). Deliberately not retried in a loop here —
      // the next GraphQL request will hit AUTH_NOT_AUTHORIZED,
      // GraphQlService will force a logout, and the player is sent back to
      // interactive sign-in instead.
      AppLogger.instance.warning('Silent token renewal failed: $e', tag: 'OIDC');
    }
  }
}
