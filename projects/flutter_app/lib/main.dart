import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app.dart';
import 'core/auth/auth_state.dart';
import 'core/config/game_server_state.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final authState = AuthState();
  final gameServerState = GameServerState();
  await Future.wait([authState.restoreSession(), gameServerState.restoreSelection()]);

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: authState),
        ChangeNotifierProvider<GameServerState>.value(value: gameServerState),
      ],
      child: CapitalismApp(),
    ),
  );
}
