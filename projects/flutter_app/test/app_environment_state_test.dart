import 'package:capitalism_app/core/config/app_config.dart';
import 'package:capitalism_app/core/config/app_environment.dart';
import 'package:capitalism_app/core/config/app_environment_state.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/in_memory_selected_environment_storage.dart';

void main() {
  setUp(() => AppConfig.setEnvironment(AppEnvironment.stage));
  tearDown(() => AppConfig.setEnvironment(AppEnvironment.stage));

  group('AppEnvironment', () {
    test('has distinct Master API URLs per environment', () {
      expect(AppEnvironment.local.masterGraphqlUrl, 'https://localhost:44364/graphql');
      expect(AppEnvironment.stage.masterGraphqlUrl, 'https://api.stage.capitalism5.com/graphql');
      expect(AppEnvironment.prod.masterGraphqlUrl, 'https://api.capitalism5.com/graphql');
    });

    test('only local has a fixed default game endpoint', () {
      expect(AppEnvironment.local.defaultGameGraphqlUrl, 'http://localhost:44356/graphql');
      expect(AppEnvironment.stage.defaultGameGraphqlUrl, isNull);
      expect(AppEnvironment.prod.defaultGameGraphqlUrl, isNull);
    });

    test('fromName falls back to stage for unknown or missing names', () {
      expect(AppEnvironment.fromName('local'), AppEnvironment.local);
      expect(AppEnvironment.fromName('bogus'), AppEnvironment.stage);
      expect(AppEnvironment.fromName(null), AppEnvironment.stage);
    });
  });

  group('AppEnvironmentState', () {
    test('setEnvironment updates AppConfig and persists the choice', () async {
      final storage = InMemorySelectedEnvironmentStorage();
      final state = AppEnvironmentState(storage: storage);

      await state.setEnvironment(AppEnvironment.prod);

      expect(state.environment, AppEnvironment.prod);
      expect(AppConfig.environment, AppEnvironment.prod);
      expect(AppConfig.masterGraphqlUrl, 'https://api.capitalism5.com/graphql');
      expect(await storage.read(), AppEnvironment.prod);
    });

    test('setEnvironment resets the game endpoint to the new environment default', () async {
      final state = AppEnvironmentState(storage: InMemorySelectedEnvironmentStorage());
      AppConfig.setGraphqlUrl('https://stale-shard.example.com/graphql');

      await state.setEnvironment(AppEnvironment.local);

      expect(AppConfig.graphqlUrl, 'http://localhost:44356/graphql');
    });

    test('restoreSelection reapplies a previously persisted environment', () async {
      final storage = InMemorySelectedEnvironmentStorage();
      await storage.write(AppEnvironment.local);
      final state = AppEnvironmentState(storage: storage);

      await state.restoreSelection();

      expect(state.environment, AppEnvironment.local);
      expect(AppConfig.environment, AppEnvironment.local);
    });

    test('restoreSelection is a no-op when nothing was persisted', () async {
      final state = AppEnvironmentState(storage: InMemorySelectedEnvironmentStorage());

      await state.restoreSelection();

      expect(state.environment, AppEnvironment.stage);
    });
  });
}
