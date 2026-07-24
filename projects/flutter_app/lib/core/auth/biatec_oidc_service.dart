import 'dart:convert';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

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
/// `projects/frontend/src/stores/auth.ts`. Runs the same authorization-code +
/// PKCE flow (`response_type=code`, `code_challenge_method=S256`) via an
/// in-app browser tab (Custom Tabs on Android, `ASWebAuthenticationSession`
/// on iOS, system browser + loopback listener on Windows/Linux) instead of a
/// same-origin browser redirect, then exchanges the returned code directly
/// from the device against `BiatecOidcConfig.tokenUrl` (public client, no
/// client_secret — the code_verifier proves possession of the original
/// request), and performs the same client-side state/nonce/issuer/audience
/// checks the web store does before trusting the returned id_token. The
/// id_token becomes this app's Bearer token directly (see AuthState) — no
/// separate token-exchange call to our backend is needed, matching the web
/// flow.
class BiatecOidcService {
  const BiatecOidcService({WebAuthenticator authenticator = const FlutterWebAuthenticator(), http.Client? httpClient})
    : _authenticator = authenticator,
      _httpClient = httpClient;

  final WebAuthenticator _authenticator;
  final http.Client? _httpClient;

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
    final codeVerifier = _randomBase64Url();
    final codeChallenge = _codeChallenge(codeVerifier);

    final authorizeUrl = Uri.parse(BiatecOidcConfig.authorizeUrl).replace(
      queryParameters: {
        'client_id': BiatecOidcConfig.clientId,
        'redirect_uri': _redirectUri,
        'scope': BiatecOidcConfig.scope,
        'response_type': 'code',
        'state': state,
        'nonce': nonce,
        'code_challenge': codeChallenge,
        'code_challenge_method': 'S256',
      },
    );

    final String callbackUrl;
    try {
      callbackUrl = await _authenticator.authenticate(
        url: authorizeUrl.toString(),
        callbackUrlScheme: _callbackUrlScheme,
        // flutter_web_auth_2 defaults to an embedded-WebView flow on
        // Windows/Linux, which matches the callback by comparing
        // Uri.scheme (e.g. "http") against callbackUrlScheme verbatim — but
        // ours is the full "http://localhost:{port}" loopback URL required
        // by the server-based flow below, so under the WebView flow that
        // comparison never matches and the embedded browser instead tries
        // to actually load http://localhost:{port}, which nothing is
        // listening on (ERR_CONNECTION_REFUSED). useWebview: false forces
        // the loopback-HttpServer implementation the desktop redirect URI
        // was designed for, opening the system browser instead.
        useWebview: false,
      );
    } catch (e, stackTrace) {
      AppLogger.instance.error('OIDC authenticate() was cancelled or failed to start', e, stackTrace, 'OIDC');
      throw BiatecOidcException('OIDC sign-in was cancelled or failed to start.');
    }

    final code = _extractAuthorizationCode(callbackUrl, expectedState: state);
    final result = await _exchangeAuthorizationCode(code, codeVerifier: codeVerifier, expectedNonce: nonce);
    AppLogger.instance.info('OIDC sign-in succeeded, token expires at ${result.expiresAtUtc.toIso8601String()}', tag: 'OIDC');
    return result;
  }

  String _extractAuthorizationCode(String callbackUrl, {required String expectedState}) {
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

    final code = params['code'];
    if (code == null) {
      AppLogger.instance.error('OIDC callback carried no authorization code', null, null, 'OIDC');
      throw BiatecOidcException('No authorization code was returned from Biatec authentication.');
    }

    return code;
  }

  // Public client (PKCE) token exchange: no client_secret, client authentication
  // happens via code_verifier proving possession of the original request.
  Future<BiatecOidcResult> _exchangeAuthorizationCode(String code, {required String codeVerifier, required String expectedNonce}) async {
    final client = _httpClient ?? http.Client();
    http.Response response;
    try {
      response = await client.post(
        Uri.parse(BiatecOidcConfig.tokenUrl),
        headers: const {'Content-Type': 'application/x-www-form-urlencoded'},
        body: {
          'grant_type': 'authorization_code',
          'code': code,
          'redirect_uri': _redirectUri,
          'client_id': BiatecOidcConfig.clientId,
          'code_verifier': codeVerifier,
        },
      );
    } finally {
      if (_httpClient == null) {
        client.close();
      }
    }

    if (response.statusCode != 200) {
      AppLogger.instance.error('OIDC token exchange failed with status ${response.statusCode}', response.body, null, 'OIDC');
      throw BiatecOidcException('Failed to exchange the Biatec authorization code for a token.');
    }

    final tokenResponse = jsonDecode(response.body) as Map<String, dynamic>;
    final token = tokenResponse['idToken'] as String?;
    if (token == null) {
      AppLogger.instance.error('OIDC token response carried no idToken', null, null, 'OIDC');
      throw BiatecOidcException('No ID token was returned from Biatec authentication.');
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
    final expiresIn = tokenResponse['expiresIn'];
    final expiresAtUtc = exp is int
        ? DateTime.fromMillisecondsSinceEpoch(exp * 1000, isUtc: true)
        : expiresIn is int
            ? DateTime.now().toUtc().add(Duration(seconds: expiresIn))
            : DateTime.now().toUtc().add(const Duration(minutes: 120));

    return BiatecOidcResult(token: token, expiresAtUtc: expiresAtUtc);
  }

  String _randomBase64Url() {
    final random = Random.secure();
    final bytes = List<int>.generate(32, (_) => random.nextInt(256));
    return base64Url.encode(bytes).replaceAll('=', '');
  }

  // PKCE (RFC 7636): code_challenge = BASE64URL(SHA256(code_verifier)).
  String _codeChallenge(String codeVerifier) {
    final digest = sha256.convert(utf8.encode(codeVerifier));
    return base64Url.encode(digest.bytes).replaceAll('=', '');
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
