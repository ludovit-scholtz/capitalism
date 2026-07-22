import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/trade/trade_models.dart';
import 'package:capitalism_app/features/trade/trade_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_trade_service.dart';
import 'support/in_memory_token_storage.dart';

const _inTransit = TradeRoute(
  id: 'route-1',
  sourceBuildingName: 'Mine',
  sourceCityName: 'Steeltown',
  destinationBuildingName: 'Factory',
  destinationCityName: 'Metropolis',
  productTypeName: null,
  resourceTypeName: 'Iron Ore',
  quantity: 100,
  expectedArrivalTick: 500,
  status: 'IN_TRANSIT',
  failureReason: null,
);

const _completed = TradeRoute(
  id: 'route-2',
  sourceBuildingName: 'Factory',
  sourceCityName: 'Metropolis',
  destinationBuildingName: 'Shop',
  destinationCityName: 'Rivertown',
  productTypeName: 'Steel Beams',
  resourceTypeName: null,
  quantity: 50,
  expectedArrivalTick: 400,
  status: 'COMPLETED',
  failureReason: null,
);

Future<void> _pump(WidgetTester tester, {required FakeTradeService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(
      value: auth,
      child: MaterialApp(home: Scaffold(body: TradeRoutesScreen(tradeService: service))),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('TradeRoutesScreen', () {
    testWidgets('shows all routes and active count by default', (tester) async {
      final service = FakeTradeService(routes: const [_inTransit, _completed]);

      await _pump(tester, service: service);

      expect(find.text('1 active shipments'), findsOneWidget);
      expect(find.textContaining('Iron Ore'), findsOneWidget);
      expect(find.textContaining('Steel Beams'), findsOneWidget);
    });

    testWidgets('filtering to Completed hides active routes', (tester) async {
      final service = FakeTradeService(routes: const [_inTransit, _completed]);

      await _pump(tester, service: service);
      await tester.tap(find.widgetWithText(ChoiceChip, 'Completed'));
      await tester.pumpAndSettle();

      expect(find.textContaining('Iron Ore'), findsNothing);
      expect(find.textContaining('Steel Beams'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeTradeService(fetchError: Exception('down'));

      await _pump(tester, service: service);

      expect(find.text('Could not load trade routes. Please try again.'), findsOneWidget);
    });
  });
}
