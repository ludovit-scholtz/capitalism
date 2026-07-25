import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_media_house_panel.dart';
import 'package:capitalism_app/features/buildings/building_panel_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _mediaHouse = BuildingDetail(
  id: 'building-1',
  companyId: 'company-1',
  name: 'Downtown Newspaper',
  type: 'MEDIA_HOUSE',
  level: 2,
  powerStatus: 'POWERED',
  occupancyPercent: null,
  isForSale: false,
  units: [],
  pendingConfiguration: null,
  mediaType: 'NEWSPAPER',
  contentValue: 500,
  contentBudgetPerTick: 200,
  isGovernmentOwned: false,
);

Future<Map<String, dynamic>> _pump(
  WidgetTester tester, {
  BuildingDetail building = _mediaHouse,
  List<MediaHouseUnitConfig> units = const [],
  List<CityMediaHouse> cityMediaHouses = const [],
  Map<String, String> ownedCompanyNames = const {'company-1': 'Acme Media Co'},
}) async {
  final calls = <String, dynamic>{};
  await tester.binding.setSurfaceSize(const Size(800, 2600));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: BuildingMediaHousePanel(
            building: building,
            units: units,
            cityMediaHouses: cityMediaHouses,
            ownedCompanyNames: ownedCompanyNames,
            onSaveBudget: (v) async => calls['budget'] = v,
            onUpgrade: () async => calls['upgraded'] = true,
            onSaveUnitConfig:
                ({required unitId, required targetCompanyId, required mediaType, required campaignBudgetPerTick, required isActive}) async {
                  calls['unitConfig'] = {
                    'unitId': unitId,
                    'targetCompanyId': targetCompanyId,
                    'mediaType': mediaType,
                    'campaignBudgetPerTick': campaignBudgetPerTick,
                    'isActive': isActive,
                  };
                },
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return calls;
}

void main() {
  group('BuildingMediaHousePanel', () {
    testWidgets('shows header metrics from the building', (tester) async {
      await _pump(tester);
      expect(find.textContaining('Channel: NEWSPAPER'), findsOneWidget);
      expect(find.textContaining('Content: 500'), findsOneWidget);
      expect(find.textContaining('Budget: 200/tick'), findsOneWidget);
    });

    testWidgets('Save Budget calls onSaveBudget with the entered value', (tester) async {
      final calls = await _pump(tester);
      final budgetField = find.widgetWithText(TextField, 'Content spend per tick');
      await tester.enterText(budgetField, '350');
      await tester.tap(find.widgetWithText(FilledButton, 'Save Budget'));
      await tester.pumpAndSettle();
      expect(calls['budget'], 350);
    });

    testWidgets('Stop Investment sets budget to zero', (tester) async {
      final calls = await _pump(tester);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Stop Investment'));
      await tester.pumpAndSettle();
      expect(calls['budget'], 0);
    });

    testWidgets('Upgrade Now calls onUpgrade unless at max level', (tester) async {
      final calls = await _pump(tester);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Upgrade Now'));
      await tester.pumpAndSettle();
      expect(calls['upgraded'], isTrue);
    });

    testWidgets('shows max-level message and no Upgrade button at level 5', (tester) async {
      const maxLevel = BuildingDetail(
        id: 'building-1',
        companyId: 'company-1',
        name: 'Downtown Newspaper',
        type: 'MEDIA_HOUSE',
        level: 5,
        powerStatus: 'POWERED',
        occupancyPercent: null,
        isForSale: false,
        units: [],
        pendingConfiguration: null,
        mediaType: 'NEWSPAPER',
      );
      await _pump(tester, building: maxLevel);
      expect(find.textContaining('Maximum level reached'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Upgrade Now'), findsNothing);
    });

    testWidgets('Save Campaign Unit calls onSaveUnitConfig with the form values', (tester) async {
      final calls = await _pump(tester);
      await tester.enterText(find.widgetWithText(TextField, 'Campaign Budget per Tick'), '500');
      await tester.tap(find.widgetWithText(FilledButton, 'Save Campaign Unit'));
      await tester.pumpAndSettle();

      expect(calls['unitConfig'], isNotNull);
      expect(calls['unitConfig']['targetCompanyId'], 'company-1');
      expect(calls['unitConfig']['campaignBudgetPerTick'], 500);
    });

    testWidgets('shows city rankings filtered to the same media channel', (tester) async {
      await _pump(
        tester,
        cityMediaHouses: const [
          CityMediaHouse(id: 'building-1', name: 'Downtown Newspaper', mediaType: 'NEWSPAPER', ownerCompanyName: 'Acme', contentRanking: 80, isGovernmentOwned: false),
          CityMediaHouse(id: 'other', name: 'City Radio', mediaType: 'RADIO', ownerCompanyName: 'Other Co', contentRanking: 50, isGovernmentOwned: false),
        ],
      );
      expect(find.text('Downtown Newspaper'), findsOneWidget);
      expect(find.text('City Radio'), findsNothing); // different channel, filtered out
      expect(find.text('YOU'), findsOneWidget);
    });
  });
}
