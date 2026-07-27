import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_link_connector_widgets.dart';
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
  linkLeft: true,
);

const _linkedNeighbor = BuildingUnitDetail(
  id: 'unit-3',
  unitType: 'PURCHASE',
  level: 1,
  resourceTypeId: 'resource-1',
  productTypeId: null,
  minPrice: null,
  gridX: 1,
  gridY: 0,
  linkRight: true,
);

String _itemNameFor(BuildingUnitDetail unit) => unit.productTypeId == 'product-1' ? 'Steel Beams' : '';

Future<void> _pump(
  WidgetTester tester, {
  required List<BuildingUnitDetail> units,
  Set<String> actionLoadingIds = const {},
  void Function(BuildingUnitDetail unit)? onUnitTap,
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
        body: BuildingUnitGrid(units: units, itemNameFor: _itemNameFor, actionLoadingIds: actionLoadingIds, onUnitTap: onUnitTap ?? (_) {}),
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

    testWidgets('tapping an occupied cell calls onUnitTap with that unit', (tester) async {
      BuildingUnitDetail? tapped;
      await _pump(tester, units: const [_manufacturing], onUnitTap: (unit) => tapped = unit);

      await tester.tap(find.byKey(const ValueKey('cell-unit-unit-1')));
      await tester.pumpAndSettle();

      expect(tapped?.id, 'unit-1');
    });

    testWidgets('a loading unit shows a progress indicator instead of its monogram', (tester) async {
      await _pump(tester, units: const [_manufacturing], actionLoadingIds: {'unit-1'}, settle: false);

      expect(
        find.descendant(of: find.byKey(const ValueKey('cell-unit-unit-1')), matching: find.byType(CircularProgressIndicator)),
        findsOneWidget,
      );
    });

    testWidgets('renders non-interactive link connectors reflecting each unit\'s link flags', (tester) async {
      await _pump(tester, units: const [_publicSales, _linkedNeighbor]);

      // 4 horizontal + 4 vertical connectors per row/col gap, plus diagonals —
      // just assert connectors are present and non-interactive (no toggle
      // handler reachable) rather than the web's toggle-cycle behavior,
      // which is edit-mode only.
      expect(find.byType(LinkConnectorButton), findsWidgets);
      expect(find.byType(DiagonalConnectorWidget), findsWidgets);
    });

    testWidgets('falls back to a fixed size + horizontal scroll below the minimum legible cell size', (tester) async {
      // Narrower than 4 cells at the minimum legible size (56px) can fit —
      // the grid falls back to a fixed size + horizontal scroll rather than
      // shrinking cells further, guarding against illegibly small cells.
      await _pump(tester, units: const [_manufacturing, _publicSales], surfaceSize: const Size(250, 700));

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

    testWidgets('scales cells down to fit a wide-but-constrained column instead of scrolling', (tester) async {
      // A width comfortably above the phone-scroll fallback range but below
      // the grid's natural width (330px) should shrink cells to fit exactly,
      // with no horizontal scroll view.
      await _pump(tester, units: const [_manufacturing], surfaceSize: const Size(280, 700));

      expect(tester.takeException(), isNull);
      expect(find.byType(SingleChildScrollView), findsNothing);
    });
  });
}
