import 'package:capitalism_app/features/buildings/building_analytics_models.dart';
import 'package:capitalism_app/features/buildings/building_financial_timeline_panel.dart';
import 'package:capitalism_app/features/buildings/building_recent_activity_panel.dart';
import 'package:capitalism_app/features/buildings/building_supply_chain_diagram.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _timeline = BuildingFinancialTimeline(
  dataFromTick: 100,
  dataToTick: 160,
  totalSales: 8000,
  totalCosts: 5000,
  totalProfit: 3000,
  timeline: [
    BuildingFinancialTickSnapshot(tick: 150, sales: 400, costs: 250, profit: 150),
    BuildingFinancialTickSnapshot(tick: 160, sales: 420, costs: 260, profit: -20),
  ],
);

const _activity = [
  BuildingRecentActivityEvent(tick: 150, eventType: 'SOLD', description: 'Sold 10 Steel Beams for 160.', quantity: 10, amount: 160),
  BuildingRecentActivityEvent(tick: 149, eventType: 'BLOCKED', description: 'Purchase blocked — max price exceeded.', quantity: null, amount: null),
];

const _purchaseUnit = SupplyChainUnitSummary(
  buildingUnitId: 'unit-1',
  unitType: 'PURCHASE',
  gridX: 0,
  gridY: 0,
  status: 'ACTIVE',
  idleTicks: 0,
  fillPercent: 0.5,
  resourceOrProductName: 'Steel',
);

const _manufacturingUnit = SupplyChainUnitSummary(
  buildingUnitId: 'unit-2',
  unitType: 'MANUFACTURING',
  gridX: 1,
  gridY: 0,
  status: 'IDLE',
  idleTicks: 8,
  fillPercent: 0.1,
  resourceOrProductName: 'Steel Beams',
);

const _diagram = BuildingSupplyChainDiagram(
  units: [_purchaseUnit, _manufacturingUnit],
  links: [SupplyChainLink(fromUnitId: 'unit-1', toUnitId: 'unit-2', estimatedTransitCost: 12)],
  healthScore: 'YELLOW',
  healthReason: 'One unit has been idle for a while.',
);

Future<void> _pumpFinancialTimeline(WidgetTester tester, {BuildingFinancialTimeline? timeline}) async {
  await tester.pumpWidget(MaterialApp(home: Scaffold(body: SingleChildScrollView(child: BuildingFinancialTimelinePanel(timeline: timeline)))));
  await tester.pumpAndSettle();
}

Future<void> _pumpRecentActivity(WidgetTester tester, {List<BuildingRecentActivityEvent> events = const []}) async {
  await tester.pumpWidget(MaterialApp(home: Scaffold(body: SingleChildScrollView(child: BuildingRecentActivityPanel(events: events)))));
  await tester.pumpAndSettle();
}

Future<void> _pumpSupplyChain(WidgetTester tester, {BuildingSupplyChainDiagram? diagram}) async {
  await tester.pumpWidget(MaterialApp(home: Scaffold(body: SingleChildScrollView(child: BuildingSupplyChainDiagramView(diagram: diagram)))));
  await tester.pumpAndSettle();
}

void main() {
  group('BuildingFinancialTimelinePanel', () {
    testWidgets('shows an empty state with no financial data', (tester) async {
      await _pumpFinancialTimeline(tester);
      expect(find.text('No financial data yet.'), findsOneWidget);
    });

    testWidgets('shows sales/costs/profit totals and the tick range', (tester) async {
      await _pumpFinancialTimeline(tester, timeline: _timeline);
      expect(find.text('8000'), findsOneWidget);
      expect(find.text('5000'), findsOneWidget);
      expect(find.text('3000'), findsOneWidget);
      expect(find.text('Profit trend · T100–T160'), findsOneWidget);
    });
  });

  group('BuildingRecentActivityPanel', () {
    testWidgets('shows an empty state with no activity', (tester) async {
      await _pumpRecentActivity(tester);
      expect(find.text('No recent activity.'), findsOneWidget);
    });

    testWidgets('shows each event with its tick and description', (tester) async {
      await _pumpRecentActivity(tester, events: _activity);
      expect(find.text('T150 · Sold 10 Steel Beams for 160.'), findsOneWidget);
      expect(find.text('T149 · Purchase blocked — max price exceeded.'), findsOneWidget);
    });
  });

  group('BuildingSupplyChainDiagramView', () {
    testWidgets('shows an empty state with no supply chain data', (tester) async {
      await _pumpSupplyChain(tester);
      expect(find.text('No supply chain data yet.'), findsOneWidget);
    });

    testWidgets('shows the health reason and positioned unit cells', (tester) async {
      await _pumpSupplyChain(tester, diagram: _diagram);
      expect(find.text('One unit has been idle for a while.'), findsOneWidget);
      expect(find.byKey(const ValueKey('supply-chain-cell-unit-1')), findsOneWidget);
      expect(find.byKey(const ValueKey('supply-chain-cell-unit-2')), findsOneWidget);
      expect(find.text('⚠'), findsOneWidget);
    });

    testWidgets('lists links between units with their transit cost', (tester) async {
      await _pumpSupplyChain(tester, diagram: _diagram);
      expect(find.textContaining('transit 12'), findsOneWidget);
    });
  });
}
