import 'package:flutter/foundation.dart' show kReleaseMode;

/// Runtime configuration, mirroring how `projects/frontend/src/lib/runtimeGraphqlUrl.ts`
/// resolves the game API URL. Values are supplied at build time via
/// `--dart-define=GRAPHQL_URL=...` (see README.md); there is no window-based
/// runtime override on mobile, so [setGraphqlUrl] (driven by
/// `GameServerState`/`GameServerSelectionScreen`) is the mobile equivalent of
/// the web's per-container runtime config — the player picks a registered
/// game server from the Master API and this becomes the active game endpoint
/// for the rest of the session.
class AppConfig {
  AppConfig._();

  static const String _defaultGraphqlUrl = String.fromEnvironment(
    'GRAPHQL_URL',
    defaultValue: 'http://localhost:44356/graphql',
  );

  static String _graphqlUrl = _defaultGraphqlUrl;

  /// The active game-server GraphQL endpoint. Starts at the `GRAPHQL_URL`
  /// build-time default and is overridden at runtime once the player selects
  /// a server (persisted by `GameServerState` so the choice survives restarts).
  static String get graphqlUrl => _graphqlUrl;

  static void setGraphqlUrl(String url) => _graphqlUrl = url;

  static void resetGraphqlUrl() => _graphqlUrl = _defaultGraphqlUrl;

  /// Stage and production are distinct, real Master API deployments (not
  /// placeholders) — default to stage outside release builds so local/CI
  /// runs don't accidentally hit production, matching how release builds
  /// should default to the real production Master API without requiring a
  /// `--dart-define` on every release build. Override either with
  /// `--dart-define=MASTER_GRAPHQL_URL=...`.
  static const String masterGraphqlUrl = String.fromEnvironment(
    'MASTER_GRAPHQL_URL',
    defaultValue: kReleaseMode ? 'https://api.capitalism5.com/graphql' : 'https://api.stage.capitalism5.com/graphql',
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
