import 'package:capitalism_app/features/buildings/building_analytics_models.dart';
import 'package:capitalism_app/features/buildings/building_unit_history_panel.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _history = [
  UnitResourceHistoryPoint(buildingUnitId: 'unit-1', tick: 100, inflowQuantity: 10, outflowQuantity: 4, consumedQuantity: 8, producedQuantity: 0),
  UnitResourceHistoryPoint(buildingUnitId: 'unit-1', tick: 101, inflowQuantity: 12, outflowQuantity: 6, consumedQuantity: 9, producedQuantity: 0),
];

const _productAnalytics = UnitProductAnalytics(
  buildingUnitId: 'unit-2',
  productName: 'Steel Beams',
  dataFromTick: 100,
  dataToTick: 160,
  totalCost: 4000,
  totalQuantityProduced: 200,
  estimatedRevenue: 6000,
  estimatedProfit: 2000,
  cityCurrencyCode: 'USD',
  snapshots: [UnitProductTickSnapshot(tick: 150, totalCost: 60, quantityProduced: 3, estimatedRevenue: 90, estimatedProfit: 30)],
);

Future<void> _pumpHistory(WidgetTester tester, {List<UnitResourceHistoryPoint> history = const []}) async {
  await tester.pumpWidget(MaterialApp(home: Scaffold(body: SingleChildScrollView(child: UnitResourceHistoryPanel(history: history)))));
  await tester.pumpAndSettle();
}

Future<void> _pumpProductAnalytics(WidgetTester tester, {UnitProductAnalytics? analytics, bool loading = false, bool settle = true}) async {
  await tester.pumpWidget(
    MaterialApp(home: Scaffold(body: SingleChildScrollView(child: UnitProductAnalyticsPanel(analytics: analytics, loading: loading)))),
  );
  if (settle) {
    await tester.pumpAndSettle();
  } else {
    await tester.pump();
  }
}

void main() {
  group('UnitResourceHistoryPanel', () {
    testWidgets('shows an empty state with no tracked history', (tester) async {
      await _pumpHistory(tester);
      expect(find.text('No tracked history yet.'), findsOneWidget);
    });

    testWidgets('shows the tick range and inflow/outflow/consumed/produced rows', (tester) async {
      await _pumpHistory(tester, history: _history);
      expect(find.text('History · T100–T101'), findsOneWidget);
      expect(find.text('Inflow'), findsOneWidget);
      expect(find.text('Outflow'), findsOneWidget);
      expect(find.text('Consumed'), findsOneWidget);
      expect(find.text('Produced'), findsOneWidget);
    });
  });

  group('UnitProductAnalyticsPanel', () {
    testWidgets('shows a loading indicator while loading', (tester) async {
      await _pumpProductAnalytics(tester, loading: true, settle: false);
      expect(find.byType(LinearProgressIndicator), findsOneWidget);
    });

    testWidgets('shows an empty state with no production data', (tester) async {
      await _pumpProductAnalytics(tester);
      expect(find.text('No production data yet.'), findsOneWidget);
    });

    testWidgets('shows produced/cost/revenue/profit metrics', (tester) async {
      await _pumpProductAnalytics(tester, analytics: _productAnalytics);
      expect(find.text('200'), findsOneWidget);
      expect(find.text('4000'), findsOneWidget);
      expect(find.text('6000'), findsOneWidget);
      expect(find.text('2000'), findsOneWidget);
    });
  });
}
