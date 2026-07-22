import 'package:capitalism_app/core/config/app_config.dart';
import 'package:capitalism_app/core/config/game_server_state.dart';
import 'package:capitalism_app/core/config/selected_game_server_storage.dart';
import 'package:capitalism_app/features/servers/game_server_models.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/in_memory_selected_game_server_storage.dart';

const _server = GameServerSummary(
  id: 'server-1',
  serverKey: 'stage',
  displayName: 'Stage',
  description: '',
  region: 'eu',
  environment: 'stage',
  backendUrl: 'https://stage.example.com',
  graphqlUrl: 'https://stage.example.com/graphql',
  frontendUrl: 'https://stage.example.com',
  version: '1.0.0',
  playerCount: 1,
  companyCount: 1,
  currentTick: 1,
  registeredAtUtc: '2026-01-01T00:00:00Z',
  lastHeartbeatAtUtc: '2026-01-01T00:00:00Z',
  isOnline: true,
);

void main() {
  setUp(AppConfig.resetGraphqlUrl);
  tearDown(AppConfig.resetGraphqlUrl);

  group('GameServerState', () {
    test('has no selection and leaves AppConfig.graphqlUrl untouched by default', () async {
      final state = GameServerState(storage: InMemorySelectedGameServerStorage());
      final defaultUrl = AppConfig.graphqlUrl;

      await state.restoreSelection();

      expect(state.hasSelection, isFalse);
      expect(AppConfig.graphqlUrl, defaultUrl);
    });

    test('selectServer persists the choice and overrides AppConfig.graphqlUrl', () async {
      final storage = InMemorySelectedGameServerStorage();
      final state = GameServerState(storage: storage);

      await state.selectServer(_server);

      expect(state.selectedServerKey, 'stage');
      expect(state.selectedDisplayName, 'Stage');
      expect(AppConfig.graphqlUrl, 'https://stage.example.com/graphql');
      expect(await storage.read(), isA<SelectedGameServer>());
    });

    test('restoreSelection reapplies a previously persisted choice', () async {
      final storage = InMemorySelectedGameServerStorage();
      await storage.write(
        const SelectedGameServer(
          serverKey: 'stage',
          displayName: 'Stage',
          graphqlUrl: 'https://stage.example.com/graphql',
        ),
      );

      final state = GameServerState(storage: storage);
      await state.restoreSelection();

      expect(state.selectedServerKey, 'stage');
      expect(AppConfig.graphqlUrl, 'https://stage.example.com/graphql');
    });

    test('clearSelection resets AppConfig.graphqlUrl to the build-time default', () async {
      final storage = InMemorySelectedGameServerStorage();
      final state = GameServerState(storage: storage);
      final defaultUrl = AppConfig.graphqlUrl;
      await state.selectServer(_server);

      await state.clearSelection();

      expect(state.hasSelection, isFalse);
      expect(AppConfig.graphqlUrl, defaultUrl);
      expect(await storage.read(), isNull);
    });
  });
}
