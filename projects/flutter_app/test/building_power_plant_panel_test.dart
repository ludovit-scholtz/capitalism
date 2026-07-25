import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_panel_models.dart';
import 'package:capitalism_app/features/buildings/building_power_plant_panel.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _plant = BuildingDetail(
  id: 'building-1',
  companyId: 'company-1',
  name: 'Gas Plant',
  type: 'POWER_PLANT',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: null,
  isForSale: false,
  units: [],
  pendingConfiguration: null,
  powerPlantType: 'GAS',
  powerOutput: 50,
  dispatchTargetPercent: 100,
  powerPriority: 5,
);

const _analytics = PowerPlantAnalytics(
  currentOutputMw: 45,
  dispatchTargetPercent: 100,
  fuelReserveMwh: 200,
  maxFuelReserveMwh: 500,
  fuelReservePercent: 40,
  fuelPurchaseCapacityMwhPerTick: 10,
  energyProducingCapacityMw: 50,
  fuelConstrainedOutputMw: 0,
  fuelTypeLabel: 'GAS',
  totalSurplusIncome: 1000,
  totalGridFines: 0,
  totalOperatingCosts: 500,
  totalFuelCosts: 300,
  totalSpotMarketRevenue: 0,
  totalNetProfit: 200,
);

const _balance = CityPowerBalance(totalSupplyMw: 100, totalDemandMw: 80, reserveMw: 20, reservePercent: 20, status: 'BALANCED');

Future<Map<String, dynamic>> _pump(
  WidgetTester tester, {
  BuildingDetail building = _plant,
  PowerPlantAnalytics? analytics = _analytics,
  CityPowerBalance? balance = _balance,
}) async {
  final calls = <String, dynamic>{};
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: BuildingPowerPlantPanel(
            building: building,
            analytics: analytics,
            cityPowerBalance: balance,
            onSetDispatch: (v) async => calls['dispatch'] = v,
            onSetPriority: (v) async => calls['priority'] = v,
            onListEnergy: (price, capacity) async => calls['listing'] = (price, capacity),
            onCancelListing: (id) async => calls['cancel'] = id,
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return calls;
}

void main() {
  group('BuildingPowerPlantPanel', () {
    testWidgets('shows city power status and P&L totals', (tester) async {
      await _pump(tester);
      expect(find.textContaining('Fully powered'), findsOneWidget);
      expect(find.textContaining('Net profit: 200'), findsOneWidget);
    });

    testWidgets('shows fuel reserve section for thermal plants', (tester) async {
      await _pump(tester);
      expect(find.textContaining('Fuel Reserve'), findsOneWidget);
      expect(find.textContaining('200 / 500 MWh'), findsOneWidget);
    });

    testWidgets('Apply on the dispatch slider calls onSetDispatch with the new value', (tester) async {
      final calls = await _pump(tester);
      // Slider defaults to the building's current dispatch, so the Apply
      // button starts disabled; drag it first.
      await tester.drag(find.byType(Slider).first, const Offset(-80, 0));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(OutlinedButton, 'Apply'));
      await tester.pumpAndSettle();
      expect(calls['dispatch'], isNotNull);
    });

    testWidgets('shows the create-listing form when there is no active listing', (tester) async {
      await _pump(tester, analytics: const PowerPlantAnalytics(
        currentOutputMw: 45,
        dispatchTargetPercent: 100,
        fuelReserveMwh: 200,
        maxFuelReserveMwh: 500,
        fuelReservePercent: 40,
        fuelPurchaseCapacityMwhPerTick: 10,
        energyProducingCapacityMw: 50,
        fuelConstrainedOutputMw: 0,
        fuelTypeLabel: 'GAS',
        totalSurplusIncome: 0,
        totalGridFines: 0,
        totalOperatingCosts: 0,
        totalFuelCosts: 0,
        totalSpotMarketRevenue: 0,
        totalNetProfit: 0,
      ));
      expect(find.text('List surplus energy for sale'), findsOneWidget);
    });

    testWidgets('shows the active listing card with a Cancel button when a listing exists', (tester) async {
      final calls = await _pump(
        tester,
        analytics: const PowerPlantAnalytics(
          currentOutputMw: 45,
          dispatchTargetPercent: 100,
          fuelReserveMwh: 200,
          maxFuelReserveMwh: 500,
          fuelReservePercent: 40,
          fuelPurchaseCapacityMwhPerTick: 10,
          energyProducingCapacityMw: 50,
          fuelConstrainedOutputMw: 0,
          fuelTypeLabel: 'GAS',
          totalSurplusIncome: 0,
          totalGridFines: 0,
          totalOperatingCosts: 0,
          totalFuelCosts: 0,
          totalSpotMarketRevenue: 0,
          totalNetProfit: 0,
          activeListing: EnergyListing(listingId: 'listing-1', pricePerKwhLocal: 0.1, capacityKw: 100, availableKw: 80),
        ),
      );

      expect(find.textContaining('0.1000/kWh'), findsOneWidget);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Cancel listing'));
      await tester.pumpAndSettle();
      expect(calls['cancel'], 'listing-1');
    });
  });
}
