import 'dart:convert';
import 'dart:math';

import 'package:flutter/foundation.dart';

import '../services/app_logger.dart';
import 'biatec_oidc_config.dart';
import 'web_authenticator.dart';

class BiatecOidcResult {
  const BiatecOidcResult({required this.token, required this.expiresAtUtc});

  final String token;
  final DateTime expiresAtUtc;
}

class BiatecOidcException implements Exception {
  BiatecOidcException(this.message);

  final String message;

  @override
  String toString() => message;
}

/// Native complement to `startBiatecOidcSignIn`/`completeBiatecOidcSignIn` in
/// `projects/frontend/src/stores/auth.ts`. Runs the same implicit-flow OIDC
/// exchange (`response_type=id_token`, `response_mode=query`) via an in-app
/// browser tab (Custom Tabs on Android, `ASWebAuthenticationSession` on iOS,
/// system browser + loopback listener on Windows/Linux) instead of a
/// same-origin browser redirect, and performs the same client-side
/// state/nonce/issuer/audience checks the web store does before trusting the
/// returned id_token. The id_token becomes this app's Bearer token directly
/// (see AuthState) — no separate token-exchange call to our backend is
/// needed, matching the web flow.
class BiatecOidcService {
  const BiatecOidcService({WebAuthenticator authenticator = const FlutterWebAuthenticator()})
    : _authenticator = authenticator;

  final WebAuthenticator _authenticator;

  bool get _isDesktop =>
      !kIsWeb && (defaultTargetPlatform == TargetPlatform.windows || defaultTargetPlatform == TargetPlatform.linux);

  String get _redirectUri => _isDesktop
      ? 'http://localhost:${BiatecOidcConfig.desktopCallbackPort}'
      : '${BiatecOidcConfig.mobileCallbackScheme}://oidc-callback';

  /// On Windows/Linux, flutter_web_auth_2 treats `callbackUrlScheme` as the
  /// full `http://localhost:{port}` URL it should bind a loopback listener
  /// to. On Android/iOS it's a bare custom URL scheme name.
  String get _callbackUrlScheme => _isDesktop ? _redirectUri : BiatecOidcConfig.mobileCallbackScheme;

  Future<BiatecOidcResult> signIn() async {
    final state = _randomBase64Url();
    final nonce = _randomBase64Url();

    final authorizeUrl = Uri.parse(BiatecOidcConfig.authorizeUrl).replace(
      queryParameters: {
        'client_id': BiatecOidcConfig.clientId,
        'redirect_uri': _redirectUri,
        'scope': BiatecOidcConfig.scope,
        'response_type': 'id_token',
        'response_mode': 'query',
        'state': state,
        'nonce': nonce,
      },
    );

    final String callbackUrl;
    try {
      callbackUrl = await _authenticator.authenticate(
        url: authorizeUrl.toString(),
        callbackUrlScheme: _callbackUrlScheme,
      );
    } catch (e, stackTrace) {
      AppLogger.instance.error('OIDC authenticate() was cancelled or failed to start', e, stackTrace, 'OIDC');
      throw BiatecOidcException('OIDC sign-in was cancelled or failed to start.');
    }

    final result = _handleCallback(callbackUrl, expectedState: state, expectedNonce: nonce);
    AppLogger.instance.info('OIDC sign-in succeeded, token expires at ${result.expiresAtUtc.toIso8601String()}', tag: 'OIDC');
    return result;
  }

  BiatecOidcResult _handleCallback(String callbackUrl, {required String expectedState, required String expectedNonce}) {
    final params = Uri.parse(callbackUrl).queryParameters;

    final errorCode = params['error'];
    if (errorCode != null) {
      AppLogger.instance.error('OIDC provider returned error: $errorCode', params['error_description'], null, 'OIDC');
      throw BiatecOidcException(params['error_description'] ?? 'OIDC login failed: $errorCode');
    }

    final callbackState = params['state'];
    if (callbackState == null || callbackState != expectedState) {
      AppLogger.instance.error('OIDC state mismatch', null, null, 'OIDC');
      throw BiatecOidcException('OIDC state validation failed. Please try signing in again.');
    }

    final token = params['id_token'] ?? params['access_token'] ?? params['token'] ?? params['jwt'];
    if (token == null) {
      AppLogger.instance.error('OIDC callback carried no token', null, null, 'OIDC');
      throw BiatecOidcException('No token was returned from Biatec authentication.');
    }

    final payload = _decodeJwtPayload(token);

    final tokenNonce = payload['nonce'];
    if (tokenNonce is String && tokenNonce != expectedNonce) {
      AppLogger.instance.error('OIDC nonce mismatch', null, null, 'OIDC');
      throw BiatecOidcException('OIDC nonce validation failed. Please try signing in again.');
    }

    final issuer = payload['iss'];
    if (issuer is! String || !BiatecOidcConfig.allowedIssuers.contains(issuer)) {
      AppLogger.instance.error('OIDC issuer not allowed: $issuer', null, null, 'OIDC');
      throw BiatecOidcException('OIDC issuer validation failed. Please try signing in again.');
    }

    final audienceClaim = payload['aud'];
    final audiences = switch (audienceClaim) {
      List<dynamic> list => list.whereType<String>().toList(),
      String value => [value],
      _ => const <String>[],
    };
    if (!audiences.contains(BiatecOidcConfig.audience)) {
      AppLogger.instance.error('OIDC audience not allowed: $audiences', null, null, 'OIDC');
      throw BiatecOidcException('OIDC audience validation failed. Please try signing in again.');
    }

    final exp = payload['exp'];
    final expiresAtUtc = exp is int
        ? DateTime.fromMillisecondsSinceEpoch(exp * 1000, isUtc: true)
        : DateTime.now().toUtc().add(const Duration(minutes: 120));

    return BiatecOidcResult(token: token, expiresAtUtc: expiresAtUtc);
  }

  String _randomBase64Url() {
    final random = Random.secure();
    final bytes = List<int>.generate(32, (_) => random.nextInt(256));
    return base64Url.encode(bytes).replaceAll('=', '');
  }

  Map<String, dynamic> _decodeJwtPayload(String token) {
    final parts = token.split('.');
    if (parts.length != 3) {
      AppLogger.instance.error('OIDC token was malformed (expected 3 JWT segments, got ${parts.length})', null, null, 'OIDC');
      throw BiatecOidcException('Malformed token returned from Biatec authentication.');
    }
    final normalized = base64Url.normalize(parts[1]);
    final payloadJson = utf8.decode(base64Url.decode(normalized));
    return jsonDecode(payloadJson) as Map<String, dynamic>;
  }
}
