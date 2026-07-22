import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/exchange/global_exchange_models.dart';
import 'package:capitalism_app/features/exchange/global_exchange_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_global_exchange_service.dart';
import 'support/in_memory_token_storage.dart';

const _offer = GlobalExchangeOffer(
  cityId: 'city-2',
  cityName: 'Steeltown',
  resourceTypeId: 'resource-1',
  resourceName: 'Iron Ore',
  unitSymbol: 't',
  exchangePricePerUnit: 5,
  deliveredPricePerUnit: 6.5,
  estimatedQuality: 0.8,
);

const _productListing = GlobalExchangeProductListing(
  orderId: 'order-1',
  productName: 'Steel Beams',
  unitSymbol: null,
  pricePerUnit: 20,
  remainingQuantity: 50,
  sellerCityName: 'Steeltown',
  sellerCompanyName: 'Acme Corp',
);

Future<void> _pumpGlobalExchange(WidgetTester tester, {required FakeGlobalExchangeService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(
      value: auth,
      child: MaterialApp(home: Scaffold(body: GlobalExchangeScreen(globalExchangeService: service))),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('GlobalExchangeScreen', () {
    testWidgets('loads offers for the first city by default', (tester) async {
      final service = FakeGlobalExchangeService(
        cities: const [
          {'id': 'city-1', 'name': 'Metropolis'},
        ],
        offersByCity: {
          'city-1': [_offer],
        },
      );

      await _pumpGlobalExchange(tester, service: service);

      expect(find.text('Iron Ore from Steeltown'), findsOneWidget);
    });

    testWidgets('switching to Products tab loads listings', (tester) async {
      final service = FakeGlobalExchangeService(
        cities: const [
          {'id': 'city-1', 'name': 'Metropolis'},
        ],
        products: [_productListing],
      );

      await _pumpGlobalExchange(tester, service: service);
      await tester.tap(find.widgetWithText(ChoiceChip, 'Products'));
      await tester.pumpAndSettle();

      expect(find.text('Steel Beams'), findsOneWidget);
    });

    testWidgets('buying an offer opens the dialog and submits', (tester) async {
      final service = FakeGlobalExchangeService(
        cities: const [
          {'id': 'city-1', 'name': 'Metropolis'},
        ],
        offersByCity: {
          'city-1': [_offer],
        },
        bankAccounts: const [
          {'id': 'account-1', 'currencyCode': 'EUR'},
        ],
        targetUnits: const [ExchangeTargetUnit(id: 'unit-1', buildingName: 'Main Factory', unitType: 'STORAGE')],
      );

      await _pumpGlobalExchange(tester, service: service);
      await tester.tap(find.widgetWithText(FilledButton, 'Buy'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), '50');
      await tester.tap(find.widgetWithText(FilledButton, 'Buy').last);
      await tester.pumpAndSettle();

      expect(service.lastBuyArgs?['resourceTypeId'], 'resource-1');
      expect(service.lastBuyArgs?['targetBuildingUnitId'], 'unit-1');
      expect(service.lastBuyArgs?['bankAccountId'], 'account-1');
    });
  });
}
