import 'dart:convert';

import 'package:capitalism_app/core/auth/biatec_oidc_service.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'support/fake_id_token.dart';
import 'support/fake_web_authenticator.dart';

int _futureExpirySeconds() => DateTime.now().add(const Duration(hours: 1)).millisecondsSinceEpoch ~/ 1000;

/// Mock token endpoint standing in for `BiatecOidcConfig.tokenUrl`. Returns
/// the id_token supplied by [idTokenForRequest] for every POST, mirroring the
/// server exchanging a PKCE authorization code for a token.
http.Client _mockTokenClient(String Function(Map<String, String> body) idTokenForRequest, {int statusCode = 200, int? expiresIn}) {
  return MockClient((request) async {
    final body = Uri.splitQueryString(request.body);
    if (statusCode != 200) {
      return http.Response('token exchange failed', statusCode);
    }
    final idToken = idTokenForRequest(body);
    return http.Response(
      jsonEncode({'idToken': idToken, 'accessToken': idToken, if (expiresIn != null) 'expiresIn': expiresIn}),
      200,
      headers: {'content-type': 'application/json'},
    );
  });
}

void main() {
  group('BiatecOidcService', () {
    test('resolves the token when state, nonce, issuer and audience all check out', () async {
      late Uri capturedUrl;
      String? capturedNonce;
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        capturedUrl = authorizeUrl;
        final state = authorizeUrl.queryParameters['state']!;
        capturedNonce = authorizeUrl.queryParameters['nonce']!;
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&code=auth-code-123';
      });
      final httpClient = _mockTokenClient(
        (_) => buildFakeIdToken({
          'nonce': capturedNonce,
          'iss': 'https://google.biatec.io',
          'aud': 'capitalism-pkce',
          'exp': _futureExpirySeconds(),
        }),
      );

      final result = await BiatecOidcService(authenticator: authenticator, httpClient: httpClient).signIn();

      expect(result.token, isNotEmpty);
      expect(capturedUrl.queryParameters['response_type'], 'code');
      expect(capturedUrl.queryParameters['code_challenge'], isNotEmpty);
      expect(capturedUrl.queryParameters['code_challenge_method'], 'S256');
      expect(capturedUrl.queryParameters['client_id'], 'capitalism-pkce');
      expect(result.expiresAtUtc.isAfter(DateTime.now().toUtc()), isTrue);
    });

    test('sends the code_verifier matching the code_challenge to the token endpoint', () async {
      String? capturedCodeVerifier;
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        final state = authorizeUrl.queryParameters['state']!;
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&code=auth-code-123';
      });
      final httpClient = _mockTokenClient((body) {
        capturedCodeVerifier = body['code_verifier'];
        expect(body['grant_type'], 'authorization_code');
        expect(body['code'], 'auth-code-123');
        expect(body['client_id'], 'capitalism-pkce');
        return buildFakeIdToken({'iss': 'https://google.biatec.io', 'aud': 'capitalism-pkce'});
      });

      await BiatecOidcService(authenticator: authenticator, httpClient: httpClient).signIn();

      expect(capturedCodeVerifier, isNotEmpty);
    });

    test('throws when the callback state does not match', () async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=wrong-state&code=auth-code-123';
      });
      final httpClient = _mockTokenClient((_) => buildFakeIdToken({'iss': 'https://google.biatec.io', 'aud': 'capitalism-pkce'}));

      await expectLater(
        BiatecOidcService(authenticator: authenticator, httpClient: httpClient).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', contains('state validation'))),
      );
    });

    test('throws when the id_token nonce does not match', () async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        final state = authorizeUrl.queryParameters['state']!;
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&code=auth-code-123';
      });
      final httpClient = _mockTokenClient(
        (_) => buildFakeIdToken({'nonce': 'wrong-nonce', 'iss': 'https://google.biatec.io', 'aud': 'capitalism-pkce'}),
      );

      await expectLater(
        BiatecOidcService(authenticator: authenticator, httpClient: httpClient).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', contains('nonce validation'))),
      );
    });

    test('throws when the issuer is not in the allow-list', () async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        final state = authorizeUrl.queryParameters['state']!;
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&code=auth-code-123';
      });
      final httpClient = _mockTokenClient((_) => buildFakeIdToken({'iss': 'https://evil.example', 'aud': 'capitalism-pkce'}));

      await expectLater(
        BiatecOidcService(authenticator: authenticator, httpClient: httpClient).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', contains('issuer validation'))),
      );
    });

    test('throws when the audience does not match', () async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        final state = authorizeUrl.queryParameters['state']!;
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&code=auth-code-123';
      });
      final httpClient = _mockTokenClient((_) => buildFakeIdToken({'iss': 'https://google.biatec.io', 'aud': 'someone-else'}));

      await expectLater(
        BiatecOidcService(authenticator: authenticator, httpClient: httpClient).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', contains('audience validation'))),
      );
    });

    test('throws when the token endpoint rejects the code exchange', () async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        final state = authorizeUrl.queryParameters['state']!;
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&code=auth-code-123';
      });
      final httpClient = _mockTokenClient((_) => '', statusCode: 400);

      await expectLater(
        BiatecOidcService(authenticator: authenticator, httpClient: httpClient).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', contains('exchange'))),
      );
    });

    test('surfaces the provider error_description when the IdP redirects with an error', () async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        return '${authorizeUrl.queryParameters['redirect_uri']}?error=access_denied&error_description=User+cancelled';
      });

      await expectLater(
        BiatecOidcService(authenticator: authenticator).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', 'User cancelled')),
      );
    });

    test('wraps an authenticator failure (e.g. the user closing the browser tab)', () async {
      final authenticator = FakeWebAuthenticator((_) => throw Exception('cancelled'));

      await expectLater(
        BiatecOidcService(authenticator: authenticator).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', contains('cancelled or failed'))),
      );
    });

    test('uses a custom URL scheme redirect on Android/iOS but a loopback URL on Windows', () async {
      Future<String> captureRedirectUri(TargetPlatform platform) async {
        final previous = debugDefaultTargetPlatformOverride;
        debugDefaultTargetPlatformOverride = platform;
        try {
          late Uri capturedUrl;
          final authenticator = FakeWebAuthenticator((authorizeUrl) {
            capturedUrl = authorizeUrl;
            final state = authorizeUrl.queryParameters['state']!;
            return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&code=auth-code-123';
          });
          final httpClient = _mockTokenClient((_) => buildFakeIdToken({'iss': 'https://google.biatec.io', 'aud': 'capitalism-pkce'}));
          await BiatecOidcService(authenticator: authenticator, httpClient: httpClient).signIn();
          return capturedUrl.queryParameters['redirect_uri']!;
        } finally {
          debugDefaultTargetPlatformOverride = previous;
        }
      }

      expect(await captureRedirectUri(TargetPlatform.android), 'io.biatec.capitalism://oidc-callback');
      expect(await captureRedirectUri(TargetPlatform.iOS), 'io.biatec.capitalism://oidc-callback');
      expect(await captureRedirectUri(TargetPlatform.windows), 'http://localhost:42815');
    });
  });
}
