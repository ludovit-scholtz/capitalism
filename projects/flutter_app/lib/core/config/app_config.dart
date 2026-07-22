/// Runtime configuration, mirroring how `projects/frontend/src/lib/runtimeGraphqlUrl.ts`
/// resolves the game API URL. Values are supplied at build time via
/// `--dart-define=GRAPHQL_URL=...` (see README.md); there is no window-based
/// runtime override on mobile, so a build-flavor-per-shard or in-app server
/// picker is the mobile equivalent of the web's per-container runtime config.
class AppConfig {
  AppConfig._();

  static const String graphqlUrl = String.fromEnvironment(
    'GRAPHQL_URL',
    defaultValue: 'http://localhost:44356/graphql',
  );

  static const String masterGraphqlUrl = String.fromEnvironment(
    'MASTER_GRAPHQL_URL',
    defaultValue: 'https://localhost:44364/graphql',
  );

  /// Master API origin with the trailing `/graphql` stripped, for the REST
  /// `/auth/forgot-password` and `/auth/reset-password` endpoints (there is
  /// no GraphQL mutation for either — see `MasterApi/Program.cs`).
  static String get masterApiBaseUrl => masterGraphqlUrl.replaceFirst(RegExp(r'/graphql/?$'), '');

  /// Mirrors `VITE_AUTH_PASSWORD_ENABLED` in the web frontend, which also
  /// defaults to **disabled** — production shows Biatec-OIDC-only sign-in by
  /// default, and email/password is opt-in via env var for dev/testing.
  /// Override with `--dart-define=AUTH_PASSWORD_ENABLED=true`.
  static const bool authPasswordEnabled = bool.fromEnvironment('AUTH_PASSWORD_ENABLED');
}
