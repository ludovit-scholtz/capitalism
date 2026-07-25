import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app.dart';
import 'core/auth/auth_state.dart';
import 'core/config/app_environment_state.dart';
import 'core/config/game_server_state.dart';
import 'core/context/account_context_state.dart';
import 'core/context/recent_building_state.dart';
import 'core/game_state/game_state_state.dart';
import 'core/graphql/graphql_service.dart';
import 'core/services/app_logger.dart';
import 'features/servers/game_server_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Route framework-level and otherwise-uncaught errors into AppLogger so
  // they show up on the Dev Info screen, not just the (often unavailable to
  // players) attached debugger console.
  final previousOnError = FlutterError.onError;
  FlutterError.onError = (details) {
    AppLogger.instance.error('Flutter framework error', details.exception, details.stack, 'Flutter');
    previousOnError?.call(details);
  };
  PlatformDispatcher.instance.onError = (error, stackTrace) {
    AppLogger.instance.error('Uncaught error', error, stackTrace, 'Platform');
    return false;
  };

  AppLogger.instance.info('App starting (${kReleaseMode ? 'release' : 'debug'} build)', tag: 'App');

  final authState = AuthState();
  final environmentState = AppEnvironmentState();
  final gameServerState = GameServerState();
  final accountContextState = AccountContextState();
  final recentBuildingState = RecentBuildingState();
  final gameStateState = GameStateState();
  await Future.wait([
    authState.restoreSession(),
    environmentState.restoreSelection(),
    gameServerState.restoreSelection(),
    accountContextState.restoreSelectedCity(),
    recentBuildingState.restoreLastBuilding(),
  ]);
  AppLogger.instance.info(
    'Session restored: authenticated=${authState.isAuthenticated}, environment=${environmentState.environment.label}, '
    'server=${gameServerState.selectedDisplayName ?? 'none'}',
    tag: 'App',
  );

  // The game server list is only known via the Master API — if nothing was
  // persisted from a previous run (fresh install, or an environment with no
  // fixed default game endpoint like stage/prod), fetch it now and connect
  // to the first available shard rather than leaving the app pointed at a
  // build-time default that may not even exist in this environment.
  await gameServerState.autoSelectFirstAvailable(GameServerService(GraphQlService(authState)));

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: authState),
        ChangeNotifierProvider<AppEnvironmentState>.value(value: environmentState),
        ChangeNotifierProvider<GameServerState>.value(value: gameServerState),
        ChangeNotifierProvider<AccountContextState>.value(value: accountContextState),
        ChangeNotifierProvider<RecentBuildingState>.value(value: recentBuildingState),
        ChangeNotifierProvider<GameStateState>.value(value: gameStateState),
      ],
      child: CapitalismApp(),
    ),
  );
}
