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
}
