import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/encyclopedia/encyclopedia_models.dart';
import 'package:capitalism_app/features/encyclopedia/encyclopedia_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_encyclopedia_service.dart';
import 'support/in_memory_token_storage.dart';

const _ironOre = EncyclopediaEntry(
  id: 'e1',
  kind: 'RESOURCE',
  name: 'Iron Ore',
  slug: 'iron-ore',
  category: 'MINERAL',
  industry: null,
  description: 'Raw iron ore.',
  imageUrl: null,
  isPerishable: false,
  isProOnly: false,
  isUnlockedForCurrentPlayer: true,
  basePrice: 5,
  weightPerUnit: 1,
  baseCraftTicks: null,
  outputQuantity: null,
  unitName: 'ton',
  unitSymbol: 't',
);

const _steelBeam = EncyclopediaEntry(
  id: 'e2',
  kind: 'PRODUCT',
  name: 'Steel Beam',
  slug: 'steel-beam',
  category: 'CONSTRUCTION',
  industry: 'HEAVY_INDUSTRY',
  description: 'A structural steel beam.',
  imageUrl: null,
  isPerishable: false,
  isProOnly: true,
  isUnlockedForCurrentPlayer: false,
  basePrice: 50,
  weightPerUnit: 10,
  baseCraftTicks: 5,
  outputQuantity: 1,
  unitName: 'unit',
  unitSymbol: null,
);

Future<GoRouter> _pumpEncyclopedia(WidgetTester tester, {required FakeEncyclopediaService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: EncyclopediaScreen(encyclopediaService: service))),
      GoRoute(
        path: '/encyclopedia/resource/:slug',
        builder: (context, state) => Scaffold(
          body: ResourceDetailScreen(slug: state.pathParameters['slug']!, encyclopediaService: service),
        ),
      ),
    ],
  );
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
  return router;
}

void main() {
  group('EncyclopediaScreen', () {
    testWidgets('shows entries with counts and Pro badge', (tester) async {
      final service = FakeEncyclopediaService(entries: [_ironOre, _steelBeam]);

      await _pumpEncyclopedia(tester, service: service);

      expect(find.text('Iron Ore'), findsOneWidget);
      expect(find.text('Steel Beam'), findsOneWidget);
      expect(find.text('1 resources · 1 products'), findsOneWidget);
      expect(find.text('⭐ Pro'), findsOneWidget);
    });

    testWidgets('filtering by search narrows the list', (tester) async {
      final service = FakeEncyclopediaService(entries: [_ironOre, _steelBeam]);

      await _pumpEncyclopedia(tester, service: service);
      await tester.enterText(find.byType(TextField), 'iron');
      await tester.pumpAndSettle();

      expect(find.text('Iron Ore'), findsOneWidget);
      expect(find.text('Steel Beam'), findsNothing);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeEncyclopediaService(entriesError: Exception('down'));

      await _pumpEncyclopedia(tester, service: service);

      expect(find.text('Could not load the encyclopedia. Please try again.'), findsOneWidget);
    });

    testWidgets('tapping an entry navigates to its resource detail', (tester) async {
      final service = FakeEncyclopediaService(
        entries: [_ironOre],
        detailBySlug: {
          'iron-ore': const EncyclopediaResourceDetail(entry: _ironOre, producedByRecipes: [], usedInRecipes: []),
        },
      );

      await _pumpEncyclopedia(tester, service: service);
      await tester.tap(find.text('Iron Ore'));
      await tester.pumpAndSettle();

      expect(find.text('Base price: 5.00'), findsOneWidget);
    });

    testWidgets('switching to a guide topic shows its static content instead of the resources catalog', (tester) async {
      final service = FakeEncyclopediaService(entries: [_ironOre]);

      await _pumpEncyclopedia(tester, service: service);
      expect(find.text('Iron Ore'), findsOneWidget);

      await tester.tap(find.byKey(const Key('encyclopedia-topic-onboarding-help')));
      await tester.pumpAndSettle();

      expect(find.text('Onboarding Help'), findsOneWidget);
      expect(find.text('Step 1 - Choose your city'), findsOneWidget);
      expect(find.text('Iron Ore'), findsNothing);
      expect(find.text('Search'), findsNothing);
    });

    testWidgets('each of the 5 guide topics shows its title, topics checklist, and cards', (tester) async {
      final service = FakeEncyclopediaService(entries: [_ironOre]);
      await _pumpEncyclopedia(tester, service: service);

      const expectations = {
        'encyclopedia-topic-onboarding-help': 'Onboarding Help',
        'encyclopedia-topic-factory-layout-help': 'Factory Layout Help',
        'encyclopedia-topic-sales-shop-help': 'Sales Shop Setup Walkthrough',
        'encyclopedia-topic-forex-trading-help': 'Forex Trading Walkthrough',
        'encyclopedia-topic-stock-exchange-help': 'Stock Exchange Walkthrough',
      };

      for (final entry in expectations.entries) {
        await tester.tap(find.byKey(Key(entry.key)));
        await tester.pumpAndSettle();
        expect(find.text(entry.value), findsOneWidget, reason: entry.key);
      }
    });

    testWidgets('switching back to Resources definition restores the catalog', (tester) async {
      final service = FakeEncyclopediaService(entries: [_ironOre]);

      await _pumpEncyclopedia(tester, service: service);
      await tester.tap(find.byKey(const Key('encyclopedia-topic-forex-trading-help')));
      await tester.pumpAndSettle();
      expect(find.text('Iron Ore'), findsNothing);

      await tester.tap(find.byKey(const Key('encyclopedia-topic-resources-definition')));
      await tester.pumpAndSettle();

      expect(find.text('Iron Ore'), findsOneWidget);
    });
  });

  group('ResourceDetailScreen', () {
    const recipe = EncyclopediaRecipe(
      id: 'r1',
      recipeName: 'Smelting',
      buildingType: 'FACTORY',
      output: _steelBeam,
      inputs: [RecipeInput(kind: 'RESOURCE', name: 'Iron Ore', slug: 'iron-ore', quantity: 2, unitSymbol: 't', isProOnly: false)],
    );

    testWidgets('shows entry details and recipe cards', (tester) async {
      final service = FakeEncyclopediaService(
        detailBySlug: {
          'steel-beam': const EncyclopediaResourceDetail(entry: _steelBeam, producedByRecipes: [recipe], usedInRecipes: []),
        },
      );

      final router = GoRouter(
        initialLocation: '/encyclopedia/resource/steel-beam',
        routes: [
          GoRoute(path: '/encyclopedia', builder: (context, state) => const Scaffold(body: Text('Encyclopedia Screen'))),
          GoRoute(
            path: '/encyclopedia/resource/:slug',
            builder: (context, state) => Scaffold(
              body: ResourceDetailScreen(slug: state.pathParameters['slug']!, encyclopediaService: service),
            ),
          ),
        ],
      );
      final auth = AuthState(storage: InMemoryTokenStorage());
      await tester.pumpWidget(
        ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
      );
      await tester.pumpAndSettle();

      expect(find.text('Steel Beam'), findsWidgets);
      expect(find.text('Smelting'), findsOneWidget);
      expect(find.textContaining('Iron Ore'), findsWidgets);
    });

    testWidgets('shows not-found state for a missing slug', (tester) async {
      final service = FakeEncyclopediaService();

      final router = GoRouter(
        initialLocation: '/encyclopedia/resource/unknown',
        routes: [
          GoRoute(path: '/encyclopedia', builder: (context, state) => const Scaffold(body: Text('Encyclopedia Screen'))),
          GoRoute(
            path: '/encyclopedia/resource/:slug',
            builder: (context, state) => Scaffold(
              body: ResourceDetailScreen(slug: state.pathParameters['slug']!, encyclopediaService: service),
            ),
          ),
        ],
      );
      final auth = AuthState(storage: InMemoryTokenStorage());
      await tester.pumpWidget(
        ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
      );
      await tester.pumpAndSettle();

      expect(find.text('Entry not found.'), findsOneWidget);
    });
  });
}
