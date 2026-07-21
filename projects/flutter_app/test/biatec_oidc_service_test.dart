import 'package:capitalism_app/core/auth/biatec_oidc_service.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_id_token.dart';
import 'support/fake_web_authenticator.dart';

int _futureExpirySeconds() => DateTime.now().add(const Duration(hours: 1)).millisecondsSinceEpoch ~/ 1000;

void main() {
  group('BiatecOidcService', () {
    test('resolves the token when state, nonce, issuer and audience all check out', () async {
      late Uri capturedUrl;
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        capturedUrl = authorizeUrl;
        final state = authorizeUrl.queryParameters['state']!;
        final nonce = authorizeUrl.queryParameters['nonce']!;
        final token = buildFakeIdToken({
          'nonce': nonce,
          'iss': 'https://google.biatec.io',
          'aud': 'capitalism',
          'exp': _futureExpirySeconds(),
        });
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&id_token=$token';
      });

      final result = await BiatecOidcService(authenticator: authenticator).signIn();

      expect(result.token, isNotEmpty);
      expect(capturedUrl.queryParameters['response_type'], 'id_token');
      expect(capturedUrl.queryParameters['response_mode'], 'query');
      expect(capturedUrl.queryParameters['client_id'], 'capitalism');
      expect(result.expiresAtUtc.isAfter(DateTime.now().toUtc()), isTrue);
    });

    test('throws when the callback state does not match', () async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        final nonce = authorizeUrl.queryParameters['nonce']!;
        final token = buildFakeIdToken({'nonce': nonce, 'iss': 'https://google.biatec.io', 'aud': 'capitalism'});
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=wrong-state&id_token=$token';
      });

      await expectLater(
        BiatecOidcService(authenticator: authenticator).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', contains('state validation'))),
      );
    });

    test('throws when the id_token nonce does not match', () async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        final state = authorizeUrl.queryParameters['state']!;
        final token = buildFakeIdToken({
          'nonce': 'wrong-nonce',
          'iss': 'https://google.biatec.io',
          'aud': 'capitalism',
        });
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&id_token=$token';
      });

      await expectLater(
        BiatecOidcService(authenticator: authenticator).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', contains('nonce validation'))),
      );
    });

    test('throws when the issuer is not in the allow-list', () async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        final state = authorizeUrl.queryParameters['state']!;
        final nonce = authorizeUrl.queryParameters['nonce']!;
        final token = buildFakeIdToken({'nonce': nonce, 'iss': 'https://evil.example', 'aud': 'capitalism'});
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&id_token=$token';
      });

      await expectLater(
        BiatecOidcService(authenticator: authenticator).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', contains('issuer validation'))),
      );
    });

    test('throws when the audience does not match', () async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        final state = authorizeUrl.queryParameters['state']!;
        final nonce = authorizeUrl.queryParameters['nonce']!;
        final token = buildFakeIdToken({
          'nonce': nonce,
          'iss': 'https://google.biatec.io',
          'aud': 'someone-else',
        });
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&id_token=$token';
      });

      await expectLater(
        BiatecOidcService(authenticator: authenticator).signIn(),
        throwsA(isA<BiatecOidcException>().having((e) => e.message, 'message', contains('audience validation'))),
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
            final nonce = authorizeUrl.queryParameters['nonce']!;
            final token = buildFakeIdToken({
              'nonce': nonce,
              'iss': 'https://google.biatec.io',
              'aud': 'capitalism',
            });
            return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&id_token=$token';
          });
          await BiatecOidcService(authenticator: authenticator).signIn();
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
