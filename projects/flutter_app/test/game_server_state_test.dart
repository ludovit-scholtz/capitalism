import 'dart:convert';

import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/config/app_config.dart';
import 'package:capitalism_app/core/config/game_server_state.dart';
import 'package:capitalism_app/core/config/selected_game_server_storage.dart';
import 'package:capitalism_app/core/graphql/graphql_service.dart';
import 'package:capitalism_app/features/servers/game_server_models.dart';
import 'package:capitalism_app/features/servers/game_server_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'support/in_memory_selected_game_server_storage.dart';
import 'support/in_memory_token_storage.dart';

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

    test('autoSelectFirstAvailable connects to the first online server when none is selected', () async {
      final state = GameServerState(storage: InMemorySelectedGameServerStorage());
      final service = _serverListService([
        _serverJson(key: 'offline-1', online: false),
        _serverJson(key: 'online-1', online: true),
      ]);

      await state.autoSelectFirstAvailable(service);

      expect(state.selectedServerKey, 'online-1');
      expect(AppConfig.graphqlUrl, 'https://online-1.example.com/graphql');
    });

    test('autoSelectFirstAvailable falls back to the first server when none report online', () async {
      final state = GameServerState(storage: InMemorySelectedGameServerStorage());
      final service = _serverListService([_serverJson(key: 'offline-1', online: false)]);

      await state.autoSelectFirstAvailable(service);

      expect(state.selectedServerKey, 'offline-1');
    });

    test('autoSelectFirstAvailable is a no-op when a server is already selected', () async {
      final state = GameServerState(storage: InMemorySelectedGameServerStorage());
      await state.selectServer(_server);
      final service = _serverListService([_serverJson(key: 'online-1', online: true)]);

      await state.autoSelectFirstAvailable(service);

      expect(state.selectedServerKey, 'stage');
    });

    test('autoSelectFirstAvailable with force reconnects even when already selected', () async {
      final state = GameServerState(storage: InMemorySelectedGameServerStorage());
      await state.selectServer(_server);
      final service = _serverListService([_serverJson(key: 'online-1', online: true)]);

      await state.autoSelectFirstAvailable(service, force: true);

      expect(state.selectedServerKey, 'online-1');
    });

    test('autoSelectFirstAvailable leaves no selection when the server list is empty', () async {
      final state = GameServerState(storage: InMemorySelectedGameServerStorage());
      final service = _serverListService(const []);

      await state.autoSelectFirstAvailable(service);

      expect(state.hasSelection, isFalse);
    });

    test('autoSelectFirstAvailable leaves no selection when the request fails', () async {
      final state = GameServerState(storage: InMemorySelectedGameServerStorage());
      final graphQlService = GraphQlService(
        AuthState(storage: InMemoryTokenStorage()),
        client: MockClient((request) async => http.Response('not json', 500)),
      );

      await state.autoSelectFirstAvailable(GameServerService(graphQlService));

      expect(state.hasSelection, isFalse);
    });
  });
}

Map<String, dynamic> _serverJson({required String key, required bool online}) => {
  'id': key,
  'serverKey': key,
  'displayName': key,
  'description': '',
  'region': 'eu',
  'environment': 'stage',
  'backendUrl': 'https://$key.example.com',
  'graphqlUrl': 'https://$key.example.com/graphql',
  'frontendUrl': 'https://$key.example.com',
  'version': '1.0.0',
  'playerCount': 0,
  'companyCount': 0,
  'currentTick': 0,
  'registeredAtUtc': '2026-01-01T00:00:00Z',
  'lastHeartbeatAtUtc': '2026-01-01T00:00:00Z',
  'isOnline': online,
};

GameServerService _serverListService(List<Map<String, dynamic>> servers) {
  final graphQlService = GraphQlService(
    AuthState(storage: InMemoryTokenStorage()),
    client: MockClient((request) async {
      return http.Response(
        jsonEncode({
          'data': {'gameServers': servers},
        }),
        200,
      );
    }),
  );
  return GameServerService(graphQlService);
}
