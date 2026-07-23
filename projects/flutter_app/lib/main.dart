import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app.dart';
import 'core/auth/auth_state.dart';
import 'core/config/game_server_state.dart';
import 'core/context/account_context_state.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final authState = AuthState();
  final gameServerState = GameServerState();
  final accountContextState = AccountContextState();
  await Future.wait([authState.restoreSession(), gameServerState.restoreSelection(), accountContextState.restoreSelectedCity()]);

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
