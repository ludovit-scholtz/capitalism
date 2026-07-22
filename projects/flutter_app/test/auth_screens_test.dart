import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/auth/web_authenticator.dart';
import 'package:capitalism_app/core/router/app_router.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;

import 'support/app_harness.dart';
import 'support/fake_auth_graphql_client.dart';
import 'support/fake_id_token.dart';
import 'support/fake_password_reset_client.dart';
import 'support/fake_web_authenticator.dart';

Future<(AuthState auth, GoRouter router)> _pumpAt(
  WidgetTester tester,
  String path, {
  bool passwordAuthEnabled = true,
  WebAuthenticator? webAuthenticator,
  http.Client? httpClient,
  http.Client? passwordResetHttpClient,
}) async {
  final router = createAppRouter(
    httpClient: httpClient ?? fakeAuthGraphQlClient(),
    passwordResetHttpClient: passwordResetHttpClient,
    webAuthenticator: webAuthenticator,
    passwordAuthEnabled: passwordAuthEnabled,
  );
  final auth = await pumpCapitalismApp(tester, router: router);
  router.go(path);
  await tester.pumpAndSettle();
  return (auth, router);
}

/// Builds a fake id_token whose `nonce`/`iss`/`aud`/`exp` claims pass
/// [BiatecOidcService]'s validation, echoing back whatever `state` the
/// authorize URL was built with — for tests exercising a *successful*
/// Biatec round trip.
String _successfulCallbackFor(Uri authorizeUrl) {
  final state = authorizeUrl.queryParameters['state']!;
  final nonce = authorizeUrl.queryParameters['nonce']!;
  final token = buildFakeIdToken({
    'nonce': nonce,
    'iss': 'https://google.biatec.io',
    'aud': 'capitalism',
    'exp': DateTime.now().add(const Duration(hours: 1)).millisecondsSinceEpoch ~/ 1000,
  });
  return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&id_token=$token';
}

void main() {
  group('LoginScreen — password auth disabled by default (matches VITE_AUTH_PASSWORD_ENABLED)', () {
    testWidgets('shows the OIDC-only banner and auto-completes Biatec sign-in after a short delay', (
      tester,
    ) async {
      final authenticator = FakeWebAuthenticator(_successfulCallbackFor);
      final router = createAppRouter(
        httpClient: fakeAuthGraphQlClient(),
        webAuthenticator: authenticator,
        passwordAuthEnabled: false,
      );
      final auth = await pumpCapitalismApp(tester, router: router);
      router.go('/login');
      // A couple of plain frames, not pumpAndSettle: go_router's route
      // transition needs more than one frame to actually swap in the new
      // screen, but pumpAndSettle would run long enough for the real 500ms
      // auto-redirect timer to already fire and navigate away before this
      // assertion ever gets to see the pre-redirect banner.
      await tester.pump();
      await tester.pump();

      expect(find.text('This server uses Biatec sign-in only.'), findsOneWidget);
      expect(find.byKey(const Key('login-email')), findsNothing);
      expect(auth.isAuthenticated, isFalse);

      await tester.pump(const Duration(milliseconds: 600));
      await tester.pumpAndSettle();

      expect(auth.isAuthenticated, isTrue);
      expect(find.text('Completing sign-in…'), findsNothing);
      expect(find.text('Go to Dashboard'), findsOneWidget); // landed back on Home, authenticated
    });
  });

  group('LoginScreen — password form (passwordAuthEnabled: true)', () {
    testWidgets('logs in with valid credentials and lands on Home', (tester) async {
      final client = fakeAuthGraphQlClient(
        onLogin: (body) {
          final input = (body['variables'] as Map)['input'] as Map;
          expect(input['email'], 'player@example.com');
          expect(input['password'], 'correct-password');
          return graphQlLoginSuccess(token: 'issued-token');
        },
      );

      final (auth, _) = await _pumpAt(tester, '/login', httpClient: client);

      await tester.enterText(find.byKey(const Key('login-email')), 'player@example.com');
      await tester.enterText(find.byKey(const Key('login-password')), 'correct-password');
      await tester.tap(find.widgetWithText(FilledButton, 'Sign In'));
      await tester.pumpAndSettle();

      expect(auth.isAuthenticated, isTrue);
      expect(auth.token, 'issued-token');
      expect(find.text('Go to Dashboard'), findsOneWidget);
    });

    testWidgets('shows a client-side validation error and never calls the API for an invalid email', (
      tester,
    ) async {
      var loginCalled = false;
      final client = fakeAuthGraphQlClient(
        onLogin: (_) {
          loginCalled = true;
          return graphQlLoginSuccess();
        },
      );

      await _pumpAt(tester, '/login', httpClient: client);

      await tester.enterText(find.byKey(const Key('login-email')), 'not-an-email');
      await tester.enterText(find.byKey(const Key('login-password')), 'correct-password');
      await tester.tap(find.widgetWithText(FilledButton, 'Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Please enter a valid email address.'), findsOneWidget);
      expect(loginCalled, isFalse);
    });

    testWidgets('maps INVALID_CREDENTIALS to a generic message', (tester) async {
      final client = fakeAuthGraphQlClient(onLogin: (_) => graphQlError('nope', 'INVALID_CREDENTIALS'));
      final (auth, _) = await _pumpAt(tester, '/login', httpClient: client);

      await tester.enterText(find.byKey(const Key('login-email')), 'player@example.com');
      await tester.enterText(find.byKey(const Key('login-password')), 'wrong-password');
      await tester.tap(find.widgetWithText(FilledButton, 'Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Incorrect email or password.'), findsOneWidget);
      expect(auth.isAuthenticated, isFalse);
    });

    testWidgets('shows a distinct banner for LOGIN_THROTTLED', (tester) async {
      final client = fakeAuthGraphQlClient(onLogin: (_) => graphQlError('slow down', 'LOGIN_THROTTLED'));
      await _pumpAt(tester, '/login', httpClient: client);

      await tester.enterText(find.byKey(const Key('login-email')), 'player@example.com');
      await tester.enterText(find.byKey(const Key('login-password')), 'whatever1');
      await tester.tap(find.widgetWithText(FilledButton, 'Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Too many sign-in attempts. Please wait a moment before trying again.'), findsOneWidget);
    });

    testWidgets('toggling to register shows the display name field and calls the register mutation', (
      tester,
    ) async {
      final client = fakeAuthGraphQlClient(
        onRegister: (body) {
          final input = (body['variables'] as Map)['input'] as Map;
          expect(input['email'], 'new@example.com');
          expect(input['displayName'], 'New Player');
          expect(input['password'], 'brand-new-pw');
          return graphQlRegisterSuccess(token: 'register-token');
        },
      );

      final (auth, _) = await _pumpAt(tester, '/login', httpClient: client);

      await tester.tap(find.text("Don't have an account? Create one"));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('login-display-name')), findsOneWidget);

      await tester.enterText(find.byKey(const Key('login-display-name')), 'New Player');
      await tester.enterText(find.byKey(const Key('login-email')), 'new@example.com');
      await tester.enterText(find.byKey(const Key('login-password')), 'brand-new-pw');
      await tester.tap(find.widgetWithText(FilledButton, 'Create Account'));
      await tester.pumpAndSettle();

      expect(auth.isAuthenticated, isTrue);
      expect(auth.token, 'register-token');
    });

    testWidgets('REGISTRATION_FAILED shows the deliberately generic message (anti-enumeration)', (tester) async {
      final client = fakeAuthGraphQlClient(onRegister: (_) => graphQlError('email exists', 'REGISTRATION_FAILED'));
      await _pumpAt(tester, '/login', httpClient: client);

      await tester.tap(find.text("Don't have an account? Create one"));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('login-display-name')), 'Someone');
      await tester.enterText(find.byKey(const Key('login-email')), 'taken@example.com');
      await tester.enterText(find.byKey(const Key('login-password')), 'password1');
      await tester.tap(find.widgetWithText(FilledButton, 'Create Account'));
      await tester.pumpAndSettle();

      expect(find.text('Registration could not be completed. Please try a different email.'), findsOneWidget);
    });

    testWidgets('the forgot-password link navigates to the Forgot Password screen', (tester) async {
      await _pumpAt(tester, '/login');

      await tester.tap(find.text('Forgot password?'));
      await tester.pumpAndSettle();

      expect(find.text('Forgot Password'), findsOneWidget);
    });

    testWidgets('the Biatec button navigates to the callback screen rather than signing in directly', (
      tester,
    ) async {
      // LoginScreen itself no longer holds a BiatecOidcService/authenticator
      // reference at all (see auth_screens.dart) — the only way this
      // authenticator can be invoked is via AuthCallbackScreen, so a
      // successful sign-in after tapping this button is proof the button
      // navigated there rather than signing in directly.
      var authenticatorInvoked = false;
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        authenticatorInvoked = true;
        return _successfulCallbackFor(authorizeUrl);
      });
      final (auth, _) = await _pumpAt(tester, '/login', webAuthenticator: authenticator);

      await tester.tap(find.text('Sign in with Biatec'));
      await tester.pumpAndSettle();

      expect(authenticatorInvoked, isTrue);
      expect(auth.isAuthenticated, isTrue);
    });
  });

  group('AuthCallbackScreen', () {
    testWidgets('shows a provider error from the route query params without calling the authenticator', (
      tester,
    ) async {
      var authenticatorCalled = false;
      final authenticator = FakeWebAuthenticator((_) {
        authenticatorCalled = true;
        return '';
      });

      await _pumpAt(
        tester,
        '/auth/callback?error=access_denied&error_description=User%20declined',
        webAuthenticator: authenticator,
      );

      expect(find.text('User declined'), findsOneWidget);
      expect(authenticatorCalled, isFalse);

      await tester.tap(find.widgetWithText(FilledButton, 'Sign In'));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('login-email')), findsOneWidget); // back on the login form
    });

    testWidgets('surfaces a BiatecOidcException message and lets the user retry from Sign In', (tester) async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        return '${authorizeUrl.queryParameters['redirect_uri']}?error=access_denied&error_description=User+cancelled';
      });

      final (auth, _) = await _pumpAt(tester, '/auth/callback', webAuthenticator: authenticator);

      expect(find.text('User cancelled'), findsOneWidget);
      expect(auth.isAuthenticated, isFalse);
    });

    testWidgets('honors the redirect query param on success', (tester) async {
      final authenticator = FakeWebAuthenticator(_successfulCallbackFor);

      final (auth, _) = await _pumpAt(
        tester,
        '/auth/callback?redirect=%2Fleaderboard',
        webAuthenticator: authenticator,
      );

      expect(auth.isAuthenticated, isTrue);
      // LeaderboardScreen is real and GraphQL-backed now; this test's fake
      // GraphQL client (`fakeAuthGraphQlClient`) returns an empty `{}` for
      // any query it doesn't specifically recognize, so the screen lands on
      // its own empty state rather than a placeholder — still proves the
      // redirect itself landed on /leaderboard.
      expect(find.text('No players on the leaderboard yet.'), findsOneWidget);
    });
  });

  group('ForgotPasswordScreen', () {
    testWidgets('submits the trimmed email and shows the success message without redirecting', (tester) async {
      String? sentEmail;
      final client = fakePasswordResetClient(
        onForgotPassword: (body) {
          sentEmail = body['email'] as String;
          return restSuccess('If an account exists, a reset link has been sent.');
        },
      );

      await _pumpAt(tester, '/forgot-password', passwordResetHttpClient: client);

      await tester.enterText(find.byKey(const Key('forgot-password-email')), '  player@example.com  ');
      await tester.tap(find.widgetWithText(FilledButton, 'Send Reset Link'));
      await tester.pumpAndSettle();

      expect(sentEmail, 'player@example.com');
      expect(find.text('If an account exists, a reset link has been sent.'), findsOneWidget);
      // Still on the Forgot Password screen — no redirect on success.
      expect(find.text('Forgot Password'), findsOneWidget);
    });

    testWidgets('maps METHOD_NOT_ALLOWED to the OIDC-only message', (tester) async {
      final client = fakePasswordResetClient(
        onForgotPassword: (_) => restError('disabled', 'METHOD_NOT_ALLOWED', statusCode: 405),
      );

      await _pumpAt(tester, '/forgot-password', passwordResetHttpClient: client);

      await tester.enterText(find.byKey(const Key('forgot-password-email')), 'player@example.com');
      await tester.tap(find.widgetWithText(FilledButton, 'Send Reset Link'));
      await tester.pumpAndSettle();

      expect(
        find.text('Password sign-in is disabled on this server. Use Biatec sign-in instead.'),
        findsOneWidget,
      );
    });
  });

  group('ResetPasswordScreen', () {
    testWidgets('reads the token from the route query param and submits it with the new password', (tester) async {
      String? sentToken;
      String? sentPassword;
      final client = fakePasswordResetClient(
        onResetPassword: (body) {
          sentToken = body['token'] as String;
          sentPassword = body['newPassword'] as String;
          return restSuccess('Password has been reset successfully.');
        },
      );

      await _pumpAt(tester, '/reset-password?token=abc123', passwordResetHttpClient: client);

      await tester.enterText(find.byKey(const Key('reset-new-password')), 'new-password-1');
      await tester.enterText(find.byKey(const Key('reset-confirm-password')), 'new-password-1');
      await tester.tap(find.widgetWithText(FilledButton, 'Reset Password'));
      await tester.pumpAndSettle();

      expect(sentToken, 'abc123');
      expect(sentPassword, 'new-password-1');
      expect(find.text('Password has been reset successfully.'), findsOneWidget);

      // Auto-redirects to /login two seconds after success.
      await tester.pump(const Duration(seconds: 2));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('login-email')), findsOneWidget);
    });

    testWidgets('shows a client-side error and makes no request when the token is missing', (tester) async {
      var requestMade = false;
      final client = fakePasswordResetClient(
        onResetPassword: (_) {
          requestMade = true;
          return restSuccess('should not happen');
        },
      );

      await _pumpAt(tester, '/reset-password', passwordResetHttpClient: client); // no ?token=

      await tester.enterText(find.byKey(const Key('reset-new-password')), 'new-password-1');
      await tester.enterText(find.byKey(const Key('reset-confirm-password')), 'new-password-1');
      await tester.tap(find.widgetWithText(FilledButton, 'Reset Password'));
      await tester.pumpAndSettle();

      expect(find.text('This reset link is missing its token. Please request a new one.'), findsOneWidget);
      expect(requestMade, isFalse);
    });

    testWidgets('shows a client-side error and makes no request when the passwords do not match', (tester) async {
      var requestMade = false;
      final client = fakePasswordResetClient(
        onResetPassword: (_) {
          requestMade = true;
          return restSuccess('should not happen');
        },
      );

      await _pumpAt(tester, '/reset-password?token=abc123', passwordResetHttpClient: client);

      await tester.enterText(find.byKey(const Key('reset-new-password')), 'password-one');
      await tester.enterText(find.byKey(const Key('reset-confirm-password')), 'password-two');
      await tester.tap(find.widgetWithText(FilledButton, 'Reset Password'));
      await tester.pumpAndSettle();

      expect(find.text('Passwords do not match.'), findsOneWidget);
      expect(requestMade, isFalse);
    });

    testWidgets('maps RESET_TOKEN_INVALID_OR_EXPIRED to the server message', (tester) async {
      final client = fakePasswordResetClient(
        onResetPassword: (_) => restError(
          'This reset link is invalid or expired. Please request a new one.',
          'RESET_TOKEN_INVALID_OR_EXPIRED',
        ),
      );

      await _pumpAt(tester, '/reset-password?token=stale-token', passwordResetHttpClient: client);

      await tester.enterText(find.byKey(const Key('reset-new-password')), 'new-password-1');
      await tester.enterText(find.byKey(const Key('reset-confirm-password')), 'new-password-1');
      await tester.tap(find.widgetWithText(FilledButton, 'Reset Password'));
      await tester.pumpAndSettle();

      expect(find.text('This reset link is invalid or expired. Please request a new one.'), findsOneWidget);
    });

    testWidgets('shows a live password-strength label', (tester) async {
      await _pumpAt(tester, '/reset-password?token=abc123');

      await tester.enterText(find.byKey(const Key('reset-new-password')), 'short1');
      await tester.pump();
      expect(find.text('Password strength: Weak'), findsOneWidget);

      await tester.enterText(find.byKey(const Key('reset-new-password')), 'medium12');
      await tester.pump();
      expect(find.text('Password strength: Medium'), findsOneWidget);

      await tester.enterText(find.byKey(const Key('reset-new-password')), 'averyverylongpassword1');
      await tester.pump();
      expect(find.text('Password strength: Strong'), findsOneWidget);
    });
  });
}
