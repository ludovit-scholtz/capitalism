import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/config/app_config.dart';
import 'package:capitalism_app/core/config/game_server_state.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/features/servers/game_server_models.dart';
import 'package:capitalism_app/features/servers/game_server_selection_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_game_server_service.dart';
import 'support/in_memory_selected_game_server_storage.dart';
import 'support/in_memory_selected_locale_storage.dart';
import 'support/in_memory_token_storage.dart';

const _stage = GameServerSummary(
  id: 'server-1',
  serverKey: 'stage',
  displayName: 'Stage',
  description: 'Staging shard',
  region: 'eu',
  environment: 'stage',
  backendUrl: 'https://stage.example.com',
  graphqlUrl: 'https://stage.example.com/graphql',
  frontendUrl: 'https://stage.example.com',
  version: '1.2.3',
  playerCount: 12,
  companyCount: 4,
  currentTick: 500,
  registeredAtUtc: '2026-01-01T00:00:00Z',
  lastHeartbeatAtUtc: '2026-01-01T00:00:05Z',
  isOnline: true,
);

const _prod = GameServerSummary(
  id: 'server-2',
  serverKey: 'prod',
  displayName: 'Production',
  description: 'Live shard',
  region: 'eu',
  environment: 'production',
  backendUrl: 'https://prod.example.com',
  graphqlUrl: 'https://prod.example.com/graphql',
  frontendUrl: 'https://prod.example.com',
  version: '1.2.3',
  playerCount: 500,
  companyCount: 120,
  currentTick: 9000,
  registeredAtUtc: '2026-01-01T00:00:00Z',
  lastHeartbeatAtUtc: null,
  isOnline: false,
);

Future<GameServerState> _pump(WidgetTester tester, {required FakeGameServerService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  addTearDown(AppConfig.resetGraphqlUrl);

  final auth = AuthState(storage: InMemoryTokenStorage());
  final gameServerState = GameServerState(storage: InMemorySelectedGameServerStorage());

  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<GameServerState>.value(value: gameServerState),
        ChangeNotifierProvider<LocaleState>.value(value: LocaleState(storage: InMemorySelectedLocaleStorage())),
      ],
      child: MaterialApp(home: Scaffold(body: GameServerSelectionScreen(gameServerService: service))),
    ),
  );
  await tester.pumpAndSettle();
  return gameServerState;
}

void main() {
  group('GameServerSelectionScreen', () {
    testWidgets('shows the registered servers with status and stats', (tester) async {
      await _pump(tester, service: FakeGameServerService(servers: [_stage, _prod]));

      expect(find.text('Stage'), findsOneWidget);
      expect(find.text('Production'), findsOneWidget);
      expect(find.text('Online'), findsOneWidget);
      expect(find.text('Offline'), findsOneWidget);
      expect(find.text('12 players · 4 companies · Jan 21, 2000 20:00'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      await _pump(tester, service: FakeGameServerService(error: Exception('down')));

      expect(find.text('Could not load the server list. Please try again.'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Try again'), findsOneWidget);
    });

    testWidgets('connecting to a server persists the selection and updates AppConfig.graphqlUrl', (tester) async {
      final gameServerState = await _pump(tester, service: FakeGameServerService(servers: [_stage, _prod]));

      await tester.tap(find.widgetWithText(FilledButton, 'Connect').first);
      await tester.pumpAndSettle();

      expect(gameServerState.selectedServerKey, 'stage');
      expect(AppConfig.graphqlUrl, 'https://stage.example.com/graphql');
      expect(find.text('Connected to Stage.'), findsOneWidget);
      expect(find.text('Currently connected'), findsOneWidget);
    });
  });
}
