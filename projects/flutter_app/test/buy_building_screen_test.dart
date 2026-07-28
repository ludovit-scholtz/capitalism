import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/features/banking/banking_models.dart';
import 'package:capitalism_app/features/buildings/buy_building_models.dart';
import 'package:capitalism_app/features/buildings/buy_building_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_banking_service.dart';
import 'support/fake_buy_building_service.dart';
import 'support/fake_tile_provider.dart';
import 'support/in_memory_selected_locale_storage.dart';
import 'support/in_memory_token_storage.dart';

const _availableLot = CityLot(
  id: 'lot-1',
  name: 'Riverside Plot',
  district: 'Riverside',
  price: 50000,
  suitableTypes: ['FACTORY'],
  ownerCompanyId: null,
  buildingId: null,
  latitude: 48.15,
  longitude: 17.11,
);

const _metropolis = BuyBuildingCity(id: 'city-1', name: 'Metropolis', currencyCode: 'EUR');

const _mediaLot = CityLot(
  id: 'lot-media',
  name: 'Media Plot',
  district: 'Downtown',
  price: 40000,
  suitableTypes: ['MEDIA_HOUSE'],
  ownerCompanyId: null,
  buildingId: null,
  latitude: 48.15,
  longitude: 17.11,
);

const _powerPlantLot = CityLot(
  id: 'lot-power',
  name: 'Power Plot',
  district: 'Industrial',
  price: 60000,
  suitableTypes: ['POWER_PLANT'],
  ownerCompanyId: null,
  buildingId: null,
  latitude: 48.15,
  longitude: 17.11,
);

const _bankLot = CityLot(
  id: 'lot-bank',
  name: 'Bank Plot',
  district: 'Downtown',
  price: 80000,
  suitableTypes: ['BANK'],
  ownerCompanyId: null,
  buildingId: null,
  latitude: 48.15,
  longitude: 17.11,
);

const _ownedLot = CityLot(
  id: 'lot-2',
  name: 'Taken Plot',
  district: 'Downtown',
  price: 30000,
  suitableTypes: ['FACTORY'],
  ownerCompanyId: 'someone-else',
  buildingId: 'building-99',
  latitude: 48.16,
  longitude: 17.12,
);

Future<GoRouter> _pumpBuyBuilding(
  WidgetTester tester, {
  required FakeBuyBuildingService service,
  FakeBankingService? bankingService,
}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/',
        builder: (context, state) => Scaffold(
          body: BuyBuildingScreen(
            companyId: 'company-1',
            buyBuildingService: service,
            bankingService: bankingService ?? FakeBankingService(),
            tileProvider: FakeTileProvider(),
          ),
        ),
      ),
      GoRoute(path: '/dashboard', builder: (context, state) => const Scaffold(body: Text('Dashboard Screen'))),
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
  group('BuyBuildingScreen', () {
    testWidgets('walks through city, type, lot, and confirm steps to purchase', (tester) async {
      final service = FakeBuyBuildingService(
        cities: const [_metropolis],
        lotsByCity: {
          'city-1': [_availableLot, _ownedLot],
        },
      );

      await _pumpBuyBuilding(tester, service: service);

      // Step 1: city
      await tester.tap(find.text('Metropolis'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      // Step 2: building type
      await tester.tap(find.text('FACTORY'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      // Step 3: lot (only the available, suitable lot should show)
      expect(find.text('Riverside Plot'), findsOneWidget);
      expect(find.text('Taken Plot'), findsNothing);
      await tester.tap(find.text('Riverside Plot'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      // Step 4: confirm
      await tester.enterText(find.byType(TextField), 'My Factory');
      await tester.tap(find.widgetWithText(FilledButton, 'Purchase'));
      await tester.pumpAndSettle();

      expect(service.lastPurchaseArgs?['companyId'], 'company-1');
      expect(service.lastPurchaseArgs?['lotId'], 'lot-1');
      expect(service.lastPurchaseArgs?['buildingType'], 'FACTORY');
      expect(service.lastPurchaseArgs?['buildingName'], 'My Factory');
      expect(find.text('Dashboard Screen'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on city load failure', (tester) async {
      final service = FakeBuyBuildingService(citiesError: Exception('down'));

      await _pumpBuyBuilding(tester, service: service);

      expect(find.text('Could not load cities. Please try again.'), findsOneWidget);
    });

    testWidgets('lot step renders map markers and the nearest-existing-buildings distance list', (tester) async {
      final service = FakeBuyBuildingService(
        cities: const [_metropolis],
        lotsByCity: {
          'city-1': [_availableLot, _ownedLot],
        },
        myBuildingLocations: const [
          OwnedBuildingLocation(
            id: 'building-1',
            name: 'My Warehouse',
            type: 'STORAGE',
            cityId: 'city-1',
            latitude: 48.1502,
            longitude: 17.1105,
          ),
        ],
      );

      await _pumpBuyBuilding(tester, service: service);
      await tester.tap(find.text('Metropolis'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('FACTORY'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      // Only the available, suitable lot gets a marker.
      expect(find.byKey(const Key('map-marker-lot-lot-1')), findsOneWidget);
      expect(find.byKey(const Key('map-marker-lot-lot-2')), findsNothing);
      expect(find.byKey(const Key('map-marker-building-building-1')), findsOneWidget);

      // No lot selected yet — no distance list.
      expect(find.text('Nearest existing buildings'), findsNothing);

      await tester.tap(find.byKey(const Key('map-marker-lot-lot-1')));
      await tester.pumpAndSettle();

      expect(find.text('Nearest existing buildings'), findsOneWidget);
      expect(find.textContaining('My Warehouse (STORAGE)'), findsOneWidget);
    });

    testWidgets('MEDIA_HOUSE requires a channel type before continuing, then passes it through to purchaseLot', (tester) async {
      final service = FakeBuyBuildingService(
        cities: const [_metropolis],
        lotsByCity: {
          'city-1': [_mediaLot],
        },
      );

      await _pumpBuyBuilding(tester, service: service);
      await tester.tap(find.text('Metropolis'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('MEDIA_HOUSE'));
      await tester.pumpAndSettle();

      // Next is disabled until a channel type is chosen.
      expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Next')).onPressed, isNull);

      await tester.tap(find.byKey(const Key('media-type-RADIO')));
      await tester.pumpAndSettle();
      expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Next')).onPressed, isNotNull);

      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Media Plot'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Purchase'));
      await tester.pumpAndSettle();

      expect(service.lastPurchaseArgs?['mediaType'], 'RADIO');
      expect(find.text('Dashboard Screen'), findsOneWidget);
    });

    testWidgets('POWER_PLANT requires a subtype before continuing, then passes it through to purchaseLot', (tester) async {
      final service = FakeBuyBuildingService(
        cities: const [_metropolis],
        lotsByCity: {
          'city-1': [_powerPlantLot],
        },
      );

      await _pumpBuyBuilding(tester, service: service);
      await tester.tap(find.text('Metropolis'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('POWER_PLANT'));
      await tester.pumpAndSettle();

      expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Next')).onPressed, isNull);

      await tester.tap(find.byKey(const Key('power-plant-type-WIND')));
      await tester.pumpAndSettle();
      expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Next')).onPressed, isNotNull);

      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Power Plot'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Purchase'));
      await tester.pumpAndSettle();

      expect(service.lastPurchaseArgs?['powerPlantType'], 'WIND');
      expect(find.text('Dashboard Screen'), findsOneWidget);
    });

    testWidgets('BANK blocks Purchase when the company lacks base capital in the city currency', (tester) async {
      final service = FakeBuyBuildingService(
        cities: const [_metropolis],
        lotsByCity: {
          'city-1': [_bankLot],
        },
      );
      final bankingService = FakeBankingService(
        myBankAccounts: const [
          PlayerBankAccount(
            id: 'account-1',
            accountNumber: '001',
            currencyCode: 'EUR',
            balance: 1000,
            companyId: 'company-1',
            companyName: 'My Company',
            ownerType: 'COMPANY',
            bankBuildingId: null,
            isDepositAccount: false,
          ),
        ],
      );

      await _pumpBuyBuilding(tester, service: service, bankingService: bankingService);
      await tester.tap(find.text('Metropolis'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('BANK'));
      await tester.pumpAndSettle();
      expect(find.textContaining('insufficient'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Bank Plot'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Purchase')).onPressed, isNull);
      expect(bankingService.calls, isNot(contains('initiateBaseDeposit')));
    });

    testWidgets('BANK with sufficient capital purchases then initiates the base deposit and sets the entered rates', (tester) async {
      final service = FakeBuyBuildingService(
        cities: const [_metropolis],
        lotsByCity: {
          'city-1': [_bankLot],
        },
        purchasedBuildingId: 'bank-building-1',
      );
      final bankingService = FakeBankingService(
        myBankAccounts: const [
          PlayerBankAccount(
            id: 'account-1',
            accountNumber: '001',
            currencyCode: 'EUR',
            balance: 20000000,
            companyId: 'company-1',
            companyName: 'My Company',
            ownerType: 'COMPANY',
            bankBuildingId: null,
            isDepositAccount: false,
          ),
        ],
      );

      await _pumpBuyBuilding(tester, service: service, bankingService: bankingService);
      await tester.tap(find.text('Metropolis'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('BANK'));
      await tester.pumpAndSettle();
      expect(find.textContaining('sufficient'), findsOneWidget);

      await tester.enterText(find.byKey(const Key('bank-deposit-rate')), '4');
      await tester.enterText(find.byKey(const Key('bank-lending-rate')), '9');

      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Bank Plot'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Purchase'));
      await tester.pumpAndSettle();

      expect(bankingService.calls, contains('initiateBaseDeposit'));
      expect(bankingService.baseDepositActivated, isTrue);
      expect(bankingService.lastSetRatesArgs, {'depositRate': 4.0, 'lendingRate': 9.0});
      expect(find.text('Dashboard Screen'), findsOneWidget);
    });
  });
}
