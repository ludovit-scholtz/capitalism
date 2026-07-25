import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_unit_grid.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _manufacturing = BuildingUnitDetail(
  id: 'unit-1',
  unitType: 'MANUFACTURING',
  level: 2,
  resourceTypeId: null,
  productTypeId: 'product-1',
  minPrice: null,
  gridX: 1,
  gridY: 3,
);

const _publicSales = BuildingUnitDetail(
  id: 'unit-2',
  unitType: 'PUBLIC_SALES',
  level: 1,
  resourceTypeId: null,
  productTypeId: 'product-1',
  minPrice: 10,
  gridX: 0,
  gridY: 0,
);

String _itemNameFor(BuildingUnitDetail unit) => unit.productTypeId == 'product-1' ? 'Steel Beams' : '';

Future<void> _pump(
  WidgetTester tester, {
  required List<BuildingUnitDetail> units,
  Set<String> actionLoadingIds = const {},
  void Function(BuildingUnitDetail unit)? onUpgrade,
  void Function(BuildingUnitDetail unit)? onUpdatePrice,
  // A visible CircularProgressIndicator animates forever, so pumpAndSettle
  // never settles while one is on screen — callers exercising the loading
  // state must pass false and drive a single `pump()` instead.
  bool settle = true,
  Size surfaceSize = const Size(800, 2000),
  double textScale = 1,
}) async {
  await tester.binding.setSurfaceSize(surfaceSize);
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(
    MaterialApp(
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(context).copyWith(size: surfaceSize, textScaler: TextScaler.linear(textScale)),
        child: child!,
      ),
      home: Scaffold(
        body: BuildingUnitGrid(
          units: units,
          itemNameFor: _itemNameFor,
          actionLoadingIds: actionLoadingIds,
          onUpgrade: onUpgrade ?? (_) {},
          onUpdatePrice: onUpdatePrice ?? (_) {},
        ),
      ),
    ),
  );
  if (settle) {
    await tester.pumpAndSettle();
  } else {
    await tester.pump();
  }
}

void main() {
  group('BuildingUnitGrid', () {
    testWidgets('places units at their gridX/gridY cell and leaves the rest empty', (tester) async {
      await _pump(tester, units: const [_manufacturing]);

      expect(find.byKey(const ValueKey('cell-unit-unit-1')), findsOneWidget);
      expect(find.byKey(const ValueKey('cell-1-3')), findsOneWidget);
      // 16 total cells (4x4), 1 occupied + 15 empty placeholders.
      expect(find.byIcon(Icons.add), findsNWidgets(15));
      expect(find.text('Lvl 2'), findsOneWidget);
      expect(find.text('Steel Beams'), findsOneWidget);
    });

    testWidgets('empty cells are not tappable', (tester) async {
      await _pump(tester, units: const []);

      expect(find.byIcon(Icons.add), findsNWidgets(16));
      await tester.tap(find.byKey(const ValueKey('cell-0-0')));
      await tester.pumpAndSettle();
      expect(find.byType(BottomSheet), findsNothing);
    });

    testWidgets('tapping an occupied cell opens a sheet with an Upgrade action', (tester) async {
      BuildingUnitDetail? upgraded;
      await _pump(tester, units: const [_manufacturing], onUpgrade: (unit) => upgraded = unit);

      await tester.tap(find.byKey(const ValueKey('cell-unit-unit-1')));
      await tester.pumpAndSettle();

      expect(find.text('MANUFACTURING · Level 2'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Upgrade'));
      await tester.pumpAndSettle();

      expect(upgraded?.id, 'unit-1');
    });

    testWidgets('only PUBLIC_SALES cells offer Update price', (tester) async {
      BuildingUnitDetail? priceUpdated;
      await _pump(tester, units: const [_publicSales], onUpdatePrice: (unit) => priceUpdated = unit);

      await tester.tap(find.byKey(const ValueKey('cell-unit-unit-2')));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(OutlinedButton, 'Update price'), findsOneWidget);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Update price'));
      await tester.pumpAndSettle();

      expect(priceUpdated?.id, 'unit-2');
    });

    testWidgets('a loading unit shows a progress indicator instead of its monogram', (tester) async {
      await _pump(tester, units: const [_manufacturing], actionLoadingIds: {'unit-1'}, settle: false);

      expect(
        find.descendant(of: find.byKey(const ValueKey('cell-unit-unit-1')), matching: find.byType(CircularProgressIndicator)),
        findsOneWidget,
      );
    });

    testWidgets('fits and scrolls horizontally on a narrow phone width without overflowing', (tester) async {
      // Smallest realistic phone width (e.g. iPhone SE). Cells use a fixed
      // pixel size and the grid scrolls horizontally rather than shrinking
      // cells below what their content needs — this guards against the
      // RenderFlex overflow a flexible/Expanded cell would hit here.
      await _pump(tester, units: const [_manufacturing, _publicSales], surfaceSize: const Size(320, 700));

      expect(tester.takeException(), isNull);
      expect(find.byType(SingleChildScrollView), findsOneWidget);

      final scrollable = find.byType(SingleChildScrollView);
      await tester.drag(scrollable, const Offset(-200, 0));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
    });

    testWidgets('does not overflow at a large accessibility text scale', (tester) async {
      await _pump(tester, units: const [_manufacturing, _publicSales], surfaceSize: const Size(360, 800), textScale: 1.4);

      expect(tester.takeException(), isNull);
    });
  });
}
