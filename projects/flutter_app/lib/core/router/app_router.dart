import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import '../../features/about/about_screen.dart';
import '../../features/about/dev_info_screen.dart';
import '../../features/auth/auth_screens.dart';
import '../../features/banking/bank_loan_request_screen.dart';
import '../../features/banking/bank_management_screen.dart';
import '../../features/banking/bank_statement_screen.dart';
import '../../features/banking/loan_marketplace_screen.dart';
import '../../features/bounties/bounty_history_screen.dart';
import '../../features/buildings/building_detail_screen.dart';
import '../../features/buildings/building_market_screen.dart';
import '../../features/buildings/buy_building_screen.dart';
import '../../features/buildings/sell_building_screen.dart';
import '../../features/cities/cities_screens.dart';
import '../../features/city/city_tab_screens.dart';
import '../../features/company/company_screens.dart';
import '../../features/company/personal_ledger_screen.dart';
import '../../features/dashboard/dashboard_screen.dart';
import '../../features/economy/contracts_screen.dart';
import '../../features/encyclopedia/encyclopedia_screens.dart';
import '../../features/exchange/forex_exchange_screen.dart';
import '../../features/exchange/global_exchange_screen.dart';
import '../../features/exchange/stock_exchange_screen.dart';
import '../../features/exchange/stock_trading_screen.dart';
import '../../features/home/home_screen.dart';
import '../../features/leaderboard/leaderboard_screens.dart';
import '../../features/market/market_screens.dart';
import '../../features/news/news_screen.dart';
import '../../features/news/notifications_screen.dart';
import '../../features/onboarding/onboarding_screen.dart';
import '../../features/operations/operations_screens.dart';
import '../../features/servers/game_server_selection_screen.dart';
import '../../features/settings/settings_screen.dart';
import '../../features/trade/trade_screens.dart';
import '../../features/tutorial/tutorial_screen.dart';
import '../auth/auth_state.dart';
import '../auth/biatec_oidc_service.dart';
import '../auth/password_reset_service.dart';
import '../auth/web_authenticator.dart';
import '../context/account_context_service.dart';
import '../game_state/game_state_service.dart';
import '../graphql/graphql_service.dart';
import '../services/url_opener.dart';
import '../widgets/app_shell.dart';

/// Route table mirroring `projects/frontend/src/router/index.ts`.
///
/// Path *shapes* are adapted for go_router/mobile deep-linking conventions
/// rather than kept byte-identical to the web URLs (e.g. the web's nested
/// `/encyclopedia/:topicSlug(pattern)` route becomes two flat routes here).
/// Screen names and the views they mirror are kept 1:1 with the web app —
/// see CLAUDE.md for the full route/nav parity notes.
///
/// [createAppRouter] is a factory rather than a bare singleton so tests can
/// build a fresh [GoRouter] per test — a shared instance would leak
/// navigation state (current location) across tests via `pumpWidget`. It
/// also accepts an injectable [UrlOpener] (so tests can fake external-link
/// taps, e.g. Discord, without exercising the real url_launcher platform
/// channel), an injectable [httpClient] (so tests can fake HomeScreen's and
/// LoginScreen's GraphQL calls instead of hitting a real backend), and an
/// injectable [webAuthenticator] (so tests can fake the Biatec OIDC round
/// trip on AuthCallbackScreen instead of exercising the real platform
/// channel).
GoRouter createAppRouter({
  UrlOpener urlOpener = const ExternalUrlOpener(),
  http.Client? httpClient,
  http.Client? passwordResetHttpClient,
  WebAuthenticator? webAuthenticator,
  http.Client? oidcHttpClient,
  bool? passwordAuthEnabled,
  AccountContextService? accountContextService,
  GameStateService? gameStateService,
}) {
  BiatecOidcService buildOidcService() =>
      webAuthenticator != null || oidcHttpClient != null
          ? BiatecOidcService(
              authenticator:
                  webAuthenticator ?? const FlutterWebAuthenticator(),
              httpClient: oidcHttpClient,
            )
          : const BiatecOidcService();

  return GoRouter(
    initialLocation: '/',
    routes: [
      ShellRoute(
        builder: (context, state, child) => AppShell(
            urlOpener: urlOpener,
            accountContextService: accountContextService,
            gameStateService: gameStateService,
            child: child),
        routes: [
          GoRoute(
            path: '/',
            builder: (context, state) => HomeScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/login',
            builder: (context, state) => LoginScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
              redirectPath: state.uri.queryParameters['redirect'] ?? '/',
              passwordAuthEnabled: passwordAuthEnabled,
            ),
          ),
          GoRoute(
            path: '/forgot-password',
            builder: (context, state) => ForgotPasswordScreen(
              passwordResetService: passwordResetHttpClient != null
                  ? PasswordResetService(client: passwordResetHttpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/reset-password',
            builder: (context, state) => ResetPasswordScreen(
              token: state.uri.queryParameters['token'],
              passwordResetService: passwordResetHttpClient != null
                  ? PasswordResetService(client: passwordResetHttpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/auth/callback',
            builder: (context, state) => AuthCallbackScreen(
              oidcService: buildOidcService(),
              providerError: state.uri.queryParameters['error'],
              providerErrorDescription:
                  state.uri.queryParameters['error_description'],
              redirectPath: state.uri.queryParameters['redirect'] ?? '/',
            ),
          ),
          GoRoute(
            path: '/onboarding',
            builder: (context, state) => OnboardingScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
              oidcService: buildOidcService(),
            ),
          ),
          GoRoute(
            path: '/dashboard',
            builder: (context, state) => DashboardScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/news',
            builder: (context, state) => NewsScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/notifications',
            builder: (context, state) => NotificationsScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/contracts',
            builder: (context, state) => ContractsScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/leaderboard',
            builder: (context, state) => LeaderboardScreen(
              initialTab: state.uri.queryParameters['tab'],
              initialPage: int.tryParse(state.uri.queryParameters['page'] ?? ''),
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/player/:id',
            builder: (context, state) => PlayerProfileScreen(
              playerId: state.pathParameters['id']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/cities',
            builder: (context, state) => CitiesScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/map',
            builder: (context, state) => WorldMapScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/buildings/market',
            builder: (context, state) => BuildingMarketScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/encyclopedia',
            builder: (context, state) => EncyclopediaScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/encyclopedia/topic/:topicSlug',
            builder: (context, state) => EncyclopediaScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/encyclopedia/resource/:slug',
            builder: (context, state) => ResourceDetailScreen(
              slug: state.pathParameters['slug']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/exchange',
            builder: (context, state) => GlobalExchangeScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/stocks',
            builder: (context, state) => StockExchangeScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/stock/trade/:companyId',
            builder: (context, state) => StockTradingScreen(
              companyId: state.pathParameters['companyId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/forex',
            builder: (context, state) => ForexExchangeScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/buy-building/:companyId',
            builder: (context, state) => BuyBuildingScreen(
              companyId: state.pathParameters['companyId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/building/:id',
            builder: (context, state) => BuildingDetailScreen(
              buildingId: state.pathParameters['id']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/building/:id/sell',
            builder: (context, state) => SellBuildingScreen(
              buildingId: state.pathParameters['id']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
              path: '/city/:cityId',
              redirect: (context, state) => '${state.uri.path}/overview'),
          GoRoute(
            path: '/city/:cityId/overview',
            builder: (context, state) => CityOverviewScreen(
              cityId: state.pathParameters['cityId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/city/:cityId/economy',
            builder: (context, state) => CityEconomyScreen(
              cityId: state.pathParameters['cityId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/city/:cityId/buildings',
            builder: (context, state) => CityBuildingsScreen(
              cityId: state.pathParameters['cityId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/city/:cityId/market',
            builder: (context, state) => CityMarketScreen(
              cityId: state.pathParameters['cityId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/city/:cityId/contracts',
            builder: (context, state) => CityContractsScreen(
              cityId: state.pathParameters['cityId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/city/:cityId/competitors',
            builder: (context, state) => CityCompetitorsScreen(
              cityId: state.pathParameters['cityId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/ledger/:companyId',
            builder: (context, state) => LedgerScreen(
              companyId: state.pathParameters['companyId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/company/:companyId/contracts',
            builder: (context, state) => CompanyContractsScreen(
              companyId: state.pathParameters['companyId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/company/:companyId/settings',
            builder: (context, state) => CompanySettingsScreen(
              companyId: state.pathParameters['companyId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/company/:companyId/research',
            builder: (context, state) => CompanyResearchScreen(
              companyId: state.pathParameters['companyId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/banking',
            builder: (context, state) => LoanMarketplaceScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/bank/:buildingId',
            builder: (context, state) => BankManagementScreen(
              bankBuildingId: state.pathParameters['buildingId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/bank/:buildingId/request-loan',
            builder: (context, state) => BankLoanRequestScreen(
              bankBuildingId: state.pathParameters['buildingId']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/personal-ledger',
            builder: (context, state) => PersonalLedgerScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/market-intelligence',
            builder: (context, state) => MarketIntelligenceScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/market',
            builder: (context, state) => MarketDashboardScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/energy-market',
            builder: (context, state) => EnergyMarketScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/market/events',
            builder: (context, state) => GlobalEventsScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/marketing-analytics',
            builder: (context, state) => MarketingAnalyticsScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/bank-statement',
            builder: (context, state) => BankStatementScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/bank-statement/:companyId',
            builder: (context, state) => BankStatementScreen(
              companyId: state.pathParameters['companyId'],
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/trade-routes',
            builder: (context, state) => TradeRoutesScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/tutorial',
            builder: (context, state) => TutorialScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/operations/statistics',
            builder: (context, state) => OperationsOverviewScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/operations/statistics/money-flow',
            builder: (context, state) => OperationsMoneyFlowScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/operations/statistics/product-analytics',
            builder: (context, state) => OperationsProductAnalyticsScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/operations/statistics/news',
            builder: (context, state) => OperationsNewsScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/operations/statistics/players',
            builder: (context, state) => OperationsPlayersScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/operations/statistics/players/:id',
            builder: (context, state) => OperationsPlayerDetailScreen(
              playerId: state.pathParameters['id']!,
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/servers',
            builder: (context, state) => GameServerSelectionScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
            path: '/bounties',
            builder: (context, state) => BountyHistoryScreen(
              graphQlService: httpClient != null
                  ? GraphQlService(context.read<AuthState>(),
                      client: httpClient)
                  : null,
            ),
          ),
          GoRoute(
              path: '/settings', builder: (context, state) => const SettingsScreen()),
          GoRoute(
              path: '/about', builder: (context, state) => const AboutScreen()),
          GoRoute(
              path: '/about/dev-info',
              builder: (context, state) => const DevInfoScreen()),
        ],
      ),
    ],
  );
}
