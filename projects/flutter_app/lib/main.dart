import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app.dart';
import 'core/auth/auth_state.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final authState = AuthState();
  await authState.restoreSession();

  runApp(
    ChangeNotifierProvider<AuthState>.value(
      value: authState,
      child: CapitalismApp(),
    ),
  );
}
