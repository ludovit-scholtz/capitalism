/// Which backend deployment this app talks to. Switchable at runtime from
/// the About screen (see `AppEnvironmentState`), and seedable at build time
/// via `--dart-define=APP_ENVIRONMENT=local|stage|prod`. The build default
/// (see `AppConfig`) is `stage` for every build, debug and release alike —
/// a release build must never silently fall back to a dev-only `localhost`
/// address just because nobody picked anything yet.
enum AppEnvironment {
  local,
  stage,
  prod;

  String get label => switch (this) {
    AppEnvironment.local => 'Local',
    AppEnvironment.stage => 'Stage',
    AppEnvironment.prod => 'Production',
  };

  /// Master API GraphQL endpoint for this environment — the source of
  /// truth for the registered game-server (shard) list
  /// ([GameServerService.fetchGameServers]).
  String get masterGraphqlUrl => switch (this) {
    AppEnvironment.local => 'https://localhost:44364/graphql',
    AppEnvironment.stage => 'https://api.stage.capitalism5.com/graphql',
    AppEnvironment.prod => 'https://api.capitalism5.com/graphql',
  };

  /// Game API endpoint to fall back on before a shard has been selected
  /// (either by the player on `GameServerSelectionScreen`, or
  /// automatically at startup — see `main.dart`). Only `local` has one
  /// fixed, known game API instance; stage/prod are multi-shard, so there
  /// is no single correct default there — `null` means "none known yet".
  String? get defaultGameGraphqlUrl => this == AppEnvironment.local ? 'http://localhost:44356/graphql' : null;

  static AppEnvironment fromName(String? name) =>
      AppEnvironment.values.firstWhere((e) => e.name == name, orElse: () => AppEnvironment.stage);
}
