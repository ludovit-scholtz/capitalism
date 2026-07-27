import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/features/leaderboard/leaderboard_models.dart';
import 'package:capitalism_app/features/leaderboard/leaderboard_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_leaderboard_service.dart';
import 'support/in_memory_selected_locale_storage.dart';
import 'support/in_memory_token_storage.dart';

const _alice = PlayerRanking(
  playerId: 'player-1',
  displayName: 'Alice',
  personalAccountName: 'AliceInWonderland',
  totalWealthUsd: 5000000,
  personalCash: 1000000,
  sharesValue: 4000000,
  companyCount: 2,
  badgeTypes: ['FIRST_MILLION'],
);

const _bob = PlayerRanking(
  playerId: 'player-2',
  displayName: 'Bob',
  personalAccountName: '',
  totalWealthUsd: 2000000,
  personalCash: 500000,
  sharesValue: 1500000,
  companyCount: 1,
  badgeTypes: [],
);

const _acmeCo = CompanyRanking(
  companyId: 'company-1',
  companyName: 'Acme Corp',
  playerId: 'player-1',
  ownerDisplayName: 'Alice',
  ownerPersonalAccountName: 'AliceInWonderland',
  totalWealthUsd: 3000000,
  currencyCode: 'EUR',
  cash: 500000,
  buildingValue: 2000000,
  inventoryValue: 500000,
  buildingCount: 4,
);

Future<GoRouter> _pumpLeaderboard(WidgetTester tester, {required FakeLeaderboardService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));

  final auth = AuthState(storage: InMemoryTokenStorage());
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: LeaderboardScreen(leaderboardService: service))),
      GoRoute(
        path: '/player/:id',
        builder: (context, state) => Scaffold(body: Text('Profile ${state.pathParameters['id']}')),
      ),
    ],
  );
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<LocaleState>.value(value: LocaleState(storage: InMemorySelectedLocaleStorage())),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
  return router;
}

void main() {
  group('LeaderboardScreen', () {
    testWidgets('shows player rankings by default with alias and wealth', (tester) async {
      final service = FakeLeaderboardService(players: [_alice, _bob]);

      await _pumpLeaderboard(tester, service: service);

      expect(find.text('AliceInWonderland'), findsOneWidget);
      expect(find.text('Bob'), findsOneWidget);
      expect(service.companyRankingsCallCount, 0);
    });

    testWidgets('switching to Companies tab lazily loads company rankings', (tester) async {
      final service = FakeLeaderboardService(players: [_alice], companies: [_acmeCo]);

      await _pumpLeaderboard(tester, service: service);
      await tester.tap(find.widgetWithText(ChoiceChip, 'Companies'));
      await tester.pumpAndSettle();

      expect(find.text('Acme Corp'), findsOneWidget);
      expect(service.companyRankingsCallCount, 1);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeLeaderboardService(playersError: Exception('down'));

      await _pumpLeaderboard(tester, service: service);

      expect(find.text('Could not load the leaderboard. Please try again.'), findsOneWidget);
    });

    testWidgets('tapping a player row navigates to /player/:id', (tester) async {
      final service = FakeLeaderboardService(players: [_alice]);

      await _pumpLeaderboard(tester, service: service);
      await tester.tap(find.text('AliceInWonderland'));
      await tester.pumpAndSettle();

      expect(find.text('Profile player-1'), findsOneWidget);
    });
  });

  group('PlayerProfileScreen', () {
    Future<void> pumpProfile(WidgetTester tester, {required FakeLeaderboardService service, String playerId = 'player-1'}) async {
      await tester.binding.setSurfaceSize(const Size(800, 2000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final auth = AuthState(storage: InMemoryTokenStorage());
      await tester.pumpWidget(
        MultiProvider(
          providers: [
            ChangeNotifierProvider<AuthState>.value(value: auth),
            ChangeNotifierProvider<LocaleState>.value(value: LocaleState(storage: InMemorySelectedLocaleStorage())),
          ],
          child: MaterialApp(home: Scaffold(body: PlayerProfileScreen(playerId: playerId, leaderboardService: service))),
        ),
      );
      await tester.pumpAndSettle();
    }

    const profile = PlayerProfile(
      playerId: 'player-1',
      displayName: 'Alice',
      bio: 'I love factories.',
      createdAtUtc: '2026-01-01T00:00:00Z',
      joinGameYear: 2024,
      hasProSubscription: true,
      totalWealthUsd: 5000000,
      totalCompanyEquityUsd: 2000000,
      companyCount: 2,
      leaderboardRank: 1,
      activeBuildingTypes: ['FACTORY', 'SHOP'],
      citiesWithBuildings: 3,
      totalProductsSold: 1200,
      hallOfFame: PlayerHallOfFame(
        highestSingleTickRevenue: 5000,
        highestSingleTickRevenueTick: 100,
        largestBuildingAcquisitionPrice: 200000,
        largestBuildingAcquisitionName: 'Big Factory',
        highestBrandQuality: 0.85,
        highestBrandQualityName: 'SuperBrand',
        accountAgeTicks: 500,
      ),
    );

    testWidgets('shows profile header, stats, and overview tab by default', (tester) async {
      final service = FakeLeaderboardService(profiles: {'player-1': profile});

      await pumpProfile(tester, service: service);

      expect(find.text('Alice'), findsOneWidget);
      expect(find.text('⭐ Pro'), findsOneWidget);
      expect(find.text('"I love factories."'), findsOneWidget);
      expect(find.text('FACTORY'), findsOneWidget);
      expect(find.text('SHOP'), findsOneWidget);
    });

    testWidgets('shows not-found state for a missing profile', (tester) async {
      final service = FakeLeaderboardService();

      await pumpProfile(tester, service: service);

      expect(find.text('Player not found.'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeLeaderboardService(profileError: Exception('down'));

      await pumpProfile(tester, service: service);

      expect(find.text('Could not load this profile. Please try again.'), findsOneWidget);
    });

    testWidgets('switching to Achievements tab lazily loads badges', (tester) async {
      final service = FakeLeaderboardService(
        profiles: {'player-1': profile},
        badgesByPlayer: {
          'player-1': const [PlayerBadge(id: 'b1', badgeType: 'FIRST_MILLION', rarity: 'RARE', unlockCondition: r'Earn $1M', unlockedAtUtc: '2026-01-02T00:00:00Z')],
        },
      );

      await pumpProfile(tester, service: service);
      await tester.tap(find.widgetWithText(ChoiceChip, '🏅 Achievements'));
      await tester.pumpAndSettle();

      expect(find.text('FIRST_MILLION'), findsOneWidget);
      expect(service.calls.contains('fetchPlayerBadges'), isTrue);
    });
  });
}
