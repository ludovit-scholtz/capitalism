import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_panel_models.dart';
import 'package:capitalism_app/features/buildings/building_property_panel.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/in_memory_selected_locale_storage.dart';

const _apartment = BuildingDetail(
  id: 'building-1',
  companyId: 'company-1',
  name: 'Riverside Apartments',
  type: 'APARTMENT',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: 72,
  isForSale: false,
  units: [],
  pendingConfiguration: null,
  totalAreaSqm: 2000,
  pricePerSqm: 8.5,
  cityReferenceRentPerSqm: 9,
  adjustedMarketRentPerSqm: 9.0,
  populationIndex: 1.1,
);

const _detail = ApartmentBuildingDetail(
  buildingId: 'building-1',
  occupancyPercent: 72,
  totalAreaSqm: 2000,
  pricePerSqm: 8.5,
  pendingPricePerSqm: null,
  pendingPriceActivationTick: null,
  cityAverageRentPerSqm: 9,
  adjustedMarketRentPerSqm: 9.5,
  populationIndex: 1.1,
  currencyCode: 'EUR',
  revenueHistory: [
    RentalTickSnapshot(tick: 1, revenue: 100, occupancyPercent: 70, rentPerSqm: 8.5),
    RentalTickSnapshot(tick: 2, revenue: 150, occupancyPercent: 72, rentPerSqm: 8.5),
  ],
);

Future<double?> _pump(WidgetTester tester, {BuildingDetail building = _apartment, ApartmentBuildingDetail? detail = _detail}) async {
  double? scheduled;
  await tester.pumpWidget(
    ChangeNotifierProvider<LocaleState>.value(
      value: LocaleState(storage: InMemorySelectedLocaleStorage()),
      child: MaterialApp(
        home: Scaffold(
          body: SingleChildScrollView(
            child: BuildingPropertyPanel(
              building: building,
              detail: detail,
              onScheduleRent: (rent) async => scheduled = rent,
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return scheduled;
}

void main() {
  group('BuildingPropertyPanel', () {
    testWidgets('shows key metrics from the building', (tester) async {
      await _pump(tester);
      expect(find.textContaining('Total area: 2000'), findsOneWidget);
      expect(find.textContaining('Occupancy: 72.0%'), findsOneWidget);
      expect(find.textContaining('Rent: 8.50'), findsOneWidget);
    });

    testWidgets('shows market rate guidance and price position when adjustedMarketRentPerSqm is set', (tester) async {
      await _pump(tester);
      expect(find.text('Market Rate Guidance'), findsOneWidget);
      expect(find.textContaining('At market rate'), findsOneWidget);
    });

    testWidgets('shows the pending rent notice when a change is scheduled', (tester) async {
      const withPending = BuildingDetail(
        id: 'building-1',
        companyId: 'company-1',
        name: 'Riverside Apartments',
        type: 'APARTMENT',
        level: 1,
        powerStatus: 'POWERED',
        occupancyPercent: 72,
        isForSale: false,
        units: [],
        pendingConfiguration: null,
        pricePerSqm: 8.5,
        pendingPricePerSqm: 9.0,
        pendingPriceActivationTick: 500,
      );
      await _pump(tester, building: withPending);
      expect(find.textContaining('Rent change scheduled'), findsOneWidget);
    });

    testWidgets('Set Rent opens a dialog and schedules the entered rent', (tester) async {
      double? scheduled;
      await tester.pumpWidget(
        ChangeNotifierProvider<LocaleState>.value(
          value: LocaleState(storage: InMemorySelectedLocaleStorage()),
          child: MaterialApp(
            home: Scaffold(
              body: BuildingPropertyPanel(building: _apartment, detail: _detail, onScheduleRent: (rent) async => scheduled = rent),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(FilledButton, 'Set Rent'));
      await tester.pumpAndSettle();
      expect(find.text('Schedule Rent Change'), findsOneWidget);

      await tester.enterText(find.byType(TextField), '11');
      await tester.tap(find.widgetWithText(FilledButton, 'Schedule Change'));
      await tester.pumpAndSettle();

      expect(scheduled, 11);
    });

    testWidgets('shows the revenue sparkline when history is present', (tester) async {
      await _pump(tester);
      expect(find.textContaining('Revenue History'), findsOneWidget);
    });
  });
}
