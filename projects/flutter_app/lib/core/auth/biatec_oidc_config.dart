/// Configuration for the Biatec OIDC sign-in flow, mirroring the
/// `VITE_BIATEC_OIDC_*` env vars read by `projects/frontend/src/stores/auth.ts`.
/// Override at build time with `--dart-define=BIATEC_OIDC_...=...`.
class BiatecOidcConfig {
  BiatecOidcConfig._();

  static const String authorizeUrl = String.fromEnvironment(
    'BIATEC_OIDC_AUTHORIZE_URL',
    defaultValue: 'https://google.biatec.io/authorize',
  );

  static const String tokenUrl = String.fromEnvironment(
    'BIATEC_OIDC_TOKEN_URL',
    defaultValue: 'https://google.biatec.io/token',
  );

  static const String clientId = String.fromEnvironment('BIATEC_OIDC_CLIENT_ID', defaultValue: 'capitalism-pkce');

  static const String scope = String.fromEnvironment('BIATEC_OIDC_SCOPE', defaultValue: 'openid profile email');

  static const String audience = String.fromEnvironment('BIATEC_OIDC_AUDIENCE', defaultValue: 'capitalism-pkce');

  static const String _allowedIssuersRaw = String.fromEnvironment(
    'BIATEC_OIDC_ALLOWED_ISSUERS',
    defaultValue: 'https://google.biatec.io',
  );

  static List<String> get allowedIssuers =>
      _allowedIssuersRaw.split(',').map((entry) => entry.trim()).where((entry) => entry.isNotEmpty).toList();

  /// Custom URL scheme registered in `AndroidManifest.xml`
  /// (`com.linusu.flutter_web_auth_2.CallbackActivity`'s intent filter) for
  /// mobile platforms. iOS needs no manifest entry — `ASWebAuthenticationSession`
  /// intercepts the redirect at the OS level regardless of scheme.
  static const String mobileCallbackScheme = String.fromEnvironment(
    'BIATEC_OIDC_CALLBACK_SCHEME',
    defaultValue: 'io.biatec.capitalism',
  );

  /// Loopback port for the desktop (Windows/Linux) flow, where
  /// flutter_web_auth_2 opens the system browser and listens on
  /// `http://localhost:<port>` instead of using a URL scheme.
  static const int desktopCallbackPort = int.fromEnvironment('BIATEC_OIDC_DESKTOP_PORT', defaultValue: 42815);
}
