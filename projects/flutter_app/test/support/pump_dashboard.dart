import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/context/account_context_state.dart';
import 'package:capitalism_app/core/game_state/game_state_state.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/core/services/url_opener.dart';
import 'package:capitalism_app/features/chat/chat_state.dart';
import 'package:capitalism_app/features/dashboard/dashboard_screen.dart';
import 'package:capitalism_app/l10n/generated/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'fake_chat_service.dart';
import 'fake_dashboard_service.dart';
import 'fake_url_opener.dart';
import 'in_memory_selected_city_storage.dart';
import 'in_memory_selected_locale_storage.dart';

/// Pumps [DashboardScreen] inside a minimal router and the providers it needs (auth, game state, locale, account
/// context, chat with a fake service and polling off), with localization delegates so the embedded chat can read its strings.
Future<GoRouter> pumpDashboard(
  WidgetTester tester, {
  required AuthState auth,
  required FakeDashboardService service,
  GameStateState? gameStateState,
  UrlOpener? urlOpener,
  bool settle = true,
}) async {
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/',
        builder: (context, state) =>
            Scaffold(body: DashboardScreen(dashboardService: service, urlOpener: urlOpener ?? FakeUrlOpener())),
      ),
      GoRoute(path: '/login', builder: (context, state) => const Scaffold(body: Center(child: Text('Login Screen')))),
      GoRoute(
        path: '/onboarding',
        builder: (context, state) => const Scaffold(body: Center(child: Text('Onboarding Screen'))),
      ),
      GoRoute(
        path: '/building/:id',
        builder: (context, state) =>
            Scaffold(body: Center(child: Text('Building Detail ${state.pathParameters['id']}'))),
      ),
      GoRoute(
        path: '/bank/:id',
        builder: (context, state) => Scaffold(body: Center(child: Text('Bank Detail ${state.pathParameters['id']}'))),
      ),
      GoRoute(
        path: '/buy-building/:companyId',
        builder: (context, state) =>
            Scaffold(body: Center(child: Text('Buy Building ${state.pathParameters['companyId']}'))),
      ),
    ],
  );

  final chatState = ChatState(service: FakeChatService(), storage: InMemoryChatReadStorage(), pollingEnabled: false)..bindAuth(auth);
  addTearDown(chatState.dispose);
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<GameStateState>.value(value: gameStateState ?? GameStateState()),
        ChangeNotifierProvider<LocaleState>.value(value: LocaleState(storage: InMemorySelectedLocaleStorage())),
        ChangeNotifierProvider<AccountContextState>.value(value: AccountContextState(storage: InMemorySelectedCityStorage())),
        ChangeNotifierProvider<ChatState>.value(value: chatState),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        localizationsDelegates: const [
          AppLocalizations.delegate,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        supportedLocales: AppLocalizations.supportedLocales,
      ),
    ),
  );
  if (settle) await tester.pumpAndSettle();
  return router;
}
