import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'core/i18n/locale_state.dart';
import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'l10n/generated/app_localizations.dart';

class CapitalismApp extends StatelessWidget {
  CapitalismApp({super.key, GoRouter? router}) : router = router ?? createAppRouter();

  /// Injectable so tests can supply a fresh router per test instead of
  /// sharing navigation state across `pumpWidget` calls.
  final GoRouter router;

  @override
  Widget build(BuildContext context) {
    final locale = context.watch<LocaleState>().locale;
    return MaterialApp.router(
      title: 'Capitalism',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: ThemeMode.dark,
      routerConfig: router,
      locale: locale,
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
    );
  }
}
