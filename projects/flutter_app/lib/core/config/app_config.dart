import 'app_environment.dart';

/// Runtime configuration, mirroring how `projects/frontend/src/lib/runtimeGraphqlUrl.ts`
/// resolves the game API URL. Values are seeded at build time via
/// `--dart-define` (see README.md) and then layered with runtime overrides:
/// [AppEnvironmentState] (driven from the About screen) picks which
/// deployment ([AppEnvironment]) to talk to, and [GameServerState] (driven
/// from `GameServerSelectionScreen`, or the startup auto-select in
/// `main.dart`) picks which registered shard within that deployment becomes
/// the active game endpoint.
class AppConfig {
  AppConfig._();

  static const String _environmentOverride = String.fromEnvironment('APP_ENVIRONMENT');

  /// The active deployment. Defaults to [AppEnvironment.stage] for every
  /// build — debug and release alike — so a release build never silently
  /// falls back to a dev-only `localhost` address before the player (or
  /// `--dart-define=APP_ENVIRONMENT=...`) has chosen one explicitly.
  static AppEnvironment _environment = AppEnvironment.fromName(
    _environmentOverride.isEmpty ? null : _environmentOverride,
  );

  static AppEnvironment get environment => _environment;

  /// Set by [AppEnvironmentState] when the player switches environments on
  /// the About screen. Also resets the active game endpoint back to this
  /// environment's default (or unset, for stage/prod) — the previously
  /// selected shard belonged to the old environment's Master API registry
  /// and is no longer valid. Callers are expected to also clear
  /// [GameServerState]'s persisted selection in the same action.
  static void setEnvironment(AppEnvironment value) {
    _environment = value;
    _graphqlUrl = value.defaultGameGraphqlUrl;
  }

  static const String _graphqlUrlOverride = String.fromEnvironment('GRAPHQL_URL');

  static String? _graphqlUrl = _graphqlUrlOverride.isEmpty
      ? AppEnvironment.fromName(_environmentOverride.isEmpty ? null : _environmentOverride).defaultGameGraphqlUrl
      : _graphqlUrlOverride;

  /// The active game-server GraphQL endpoint, or `''` if none is known yet
  /// (stage/prod before a shard has been selected — [GraphQlService] treats
  /// an empty endpoint as "no server selected" rather than attempting a
  /// network call). Overridden at runtime once the player picks a game
  /// server (persisted by [GameServerState] so the choice survives
  /// restarts), or automatically on startup — see `main.dart`.
  static String get graphqlUrl => _graphqlUrlOverride.isNotEmpty ? _graphqlUrlOverride : (_graphqlUrl ?? '');

  static void setGraphqlUrl(String url) => _graphqlUrl = url;

  static void resetGraphqlUrl() => _graphqlUrl = _environment.defaultGameGraphqlUrl;

  static const String _masterGraphqlUrlOverride = String.fromEnvironment('MASTER_GRAPHQL_URL');

  /// Master API endpoint — the source of truth for the registered
  /// game-server list. Derived from [environment] unless explicitly
  /// overridden via `--dart-define=MASTER_GRAPHQL_URL=...`.
  static String get masterGraphqlUrl =>
      _masterGraphqlUrlOverride.isNotEmpty ? _masterGraphqlUrlOverride : _environment.masterGraphqlUrl;

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
