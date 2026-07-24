import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/context/account_context_models.dart';
import 'package:capitalism_app/core/context/account_context_state.dart';
import 'package:capitalism_app/core/theme/app_icons.dart';
import 'package:capitalism_app/features/buildings/building_market_models.dart';
import 'package:capitalism_app/features/buildings/building_market_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_building_market_service.dart';
import 'support/in_memory_selected_city_storage.dart';
import 'support/in_memory_token_storage.dart';

const _city = MarketBuildingCity(id: 'city-1', name: 'Metropolis', currencyCode: 'USD');
const _company = MarketBuildingCompany(id: 'company-2', name: 'Acme Corp', ownerDisplayName: 'Bob');

const _forSaleBuilding = MarketBuilding(
  id: 'building-1',
  name: 'Downtown Factory',
  type: 'FACTORY',
  isForSale: true,
  askingPrice: 100000,
  level: 2,
  isCollateralized: false,
  city: _city,
  company: _company,
);

const _pendingOffer = BuildingOffer(
  id: 'offer-1',
  offerVersion: 1,
  offeredPrice: 95000,
  status: 'PENDING',
  buyerCompanyName: 'Buyer Co',
  buyerDisplayName: 'Carol',
);

const _myListing = MyBuildingListing(building: _forSaleBuilding, offers: [_pendingOffer]);

Future<void> _pumpMarket(
  WidgetTester tester, {
  required FakeBuildingMarketService service,
  bool authenticated = true,
  String? activeCompanyId,
}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  if (authenticated) await auth.setToken('test-token');
  final accountContextState = AccountContextState(storage: InMemorySelectedCityStorage());
  if (activeCompanyId != null) {
    accountContextState.activeAccount = ActiveAccountInfo(
      playerId: 'player-1',
      displayName: 'Player',
      availableCash: 0,
      activeAccountType: 'COMPANY',
      activeCompanyId: activeCompanyId,
    );
  }
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<AccountContextState>.value(value: accountContextState),
      ],
      child: MaterialApp(home: Scaffold(body: BuildingMarketScreen(buildingMarketService: service))),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('BuildingMarketScreen', () {
    testWidgets('shows market listings by default', (tester) async {
      final service = FakeBuildingMarketService(market: [_forSaleBuilding]);

      await _pumpMarket(tester, service: service);

      expect(find.text('Downtown Factory'), findsOneWidget);
      expect(service.calls, contains('fetchMarket'));
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeBuildingMarketService(marketError: Exception('down'));

      await _pumpMarket(tester, service: service);

      expect(find.text('Could not load the building market. Please try again.'), findsOneWidget);
    });

    testWidgets('tapping a listing opens the Make Offer dialog and submits', (tester) async {
      final service = FakeBuildingMarketService(
        market: [_forSaleBuilding],
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
        ],
      );

      await _pumpMarket(tester, service: service);
      await tester.tap(find.text('Downtown Factory'));
      await tester.pumpAndSettle();

      expect(find.text('Make an offer on Downtown Factory'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Send offer'));
      await tester.pumpAndSettle();

      expect(service.lastOfferArgs?['buildingId'], 'building-1');
      expect(service.lastOfferArgs?['buyerCompanyId'], 'company-1');
    });

    testWidgets('Make Offer dialog defaults the buyer company to the header\'s active company', (tester) async {
      final service = FakeBuildingMarketService(
        market: [_forSaleBuilding],
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
          {'id': 'company-3', 'name': 'Third Company'},
        ],
      );

      await _pumpMarket(tester, service: service, activeCompanyId: 'company-3');
      await tester.tap(find.text('Downtown Factory'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Send offer'));
      await tester.pumpAndSettle();

      expect(service.lastOfferArgs?['buyerCompanyId'], 'company-3');
    });

    testWidgets('My Listings tab is disabled when unauthenticated', (tester) async {
      final service = FakeBuildingMarketService();

      await _pumpMarket(tester, service: service, authenticated: false);
      await tester.tap(find.widgetWithText(ChoiceChip, 'My Listings'));
      await tester.pumpAndSettle();

      expect(service.calls.contains('fetchMyListings'), isFalse);
    });

    testWidgets('My Listings tab shows offers with accept/reject actions', (tester) async {
      final service = FakeBuildingMarketService(myListings: [_myListing]);

      await _pumpMarket(tester, service: service);
      await tester.tap(find.widgetWithText(ChoiceChip, 'My Listings'));
      await tester.pumpAndSettle();

      expect(find.textContaining('Buyer Co'), findsOneWidget);

      await tester.tap(find.byIcon(AppIcons.check.data));
      await tester.pumpAndSettle();

      expect(service.acceptedOfferIds, ['offer-1']);
    });
  });
}
