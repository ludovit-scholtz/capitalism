import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app.dart';
import 'core/auth/auth_state.dart';
import 'core/config/game_server_state.dart';
import 'core/context/account_context_state.dart';
import 'core/services/app_logger.dart';

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
  final gameServerState = GameServerState();
  final accountContextState = AccountContextState();
  await Future.wait([authState.restoreSession(), gameServerState.restoreSelection(), accountContextState.restoreSelectedCity()]);
  AppLogger.instance.info(
    'Session restored: authenticated=${authState.isAuthenticated}, server=${gameServerState.selectedDisplayName ?? 'default'}',
    tag: 'App',
  );

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: authState),
        ChangeNotifierProvider<GameServerState>.value(value: gameServerState),
        ChangeNotifierProvider<AccountContextState>.value(value: accountContextState),
      ],
      child: CapitalismApp(),
    ),
  );
}
