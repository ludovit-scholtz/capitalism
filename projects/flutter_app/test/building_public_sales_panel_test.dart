import 'package:capitalism_app/features/buildings/building_public_sales_panel.dart';
import 'package:capitalism_app/features/buildings/building_sales_models.dart';
import 'package:capitalism_app/features/exchange/forex_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _analytics = PublicSalesAnalytics(
  buildingUnitId: 'unit-1',
  productName: 'Steel Beams',
  totalRevenue: 5000,
  totalProfit: 1200,
  totalQuantitySold: 300,
  averagePricePerUnit: 16.67,
  currentSalesCapacity: 400,
  dataFromTick: 100,
  dataToTick: 160,
  demandSignal: 'STRONG',
  actionHint: 'Consider raising your price.',
  recentUtilization: 0.82,
  elasticityIndex: 1.1,
  trendDirection: 'UP',
  cityCurrencyCode: 'USD',
  cityMarketClearingPrice: 15,
  revenueHistory: [SalesTickPoint(tick: 150, revenue: 400, quantitySold: 24), SalesTickPoint(tick: 160, revenue: 480, quantitySold: 28)],
  priceHistory: [PriceTickPoint(tick: 150, pricePerUnit: 16), PriceTickPoint(tick: 160, pricePerUnit: 17)],
  profitHistory: [ProfitTickPoint(tick: 150, profit: 90), ProfitTickPoint(tick: 160, profit: 110)],
  marketShare: [MarketShareEntry(label: 'You', share: 0.4, isUnmet: false), MarketShareEntry(label: 'Player A', share: 0.2, isUnmet: true)],
);

const _marketEvent = MarketEvent(
  id: 'event-1',
  title: 'Commodity Shock',
  description: 'Steel prices are spiking city-wide.',
  magnitudeMultiplier: 1.3,
  ticksRemaining: 12,
  affectedResourceName: 'Steel',
);

Future<void> _pump(
  WidgetTester tester, {
  PublicSalesAnalytics? analytics,
  bool analyticsLoading = false,
  List<MarketEvent> marketEvents = const [],
  double? currentThreshold,
  Future<void> Function(double? threshold)? onSaveThreshold,
  Future<void> Function()? onFlushStorage,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: PublicSalesToolsPanel(
            analytics: analytics,
            analyticsLoading: analyticsLoading,
            marketEvents: marketEvents,
            currentThreshold: currentThreshold,
            onSaveThreshold: onSaveThreshold ?? (_) async {},
            onFlushStorage: onFlushStorage ?? () async {},
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('PublicSalesToolsPanel', () {
    testWidgets('shows an empty state when there is no analytics yet', (tester) async {
      await _pump(tester);
      expect(find.text('No sales analytics yet.'), findsOneWidget);
    });

    testWidgets('renders analytics metrics and history when loaded', (tester) async {
      await _pump(tester, analytics: _analytics);

      expect(find.text('5000'), findsOneWidget);
      expect(find.text('1200'), findsOneWidget);
      expect(find.textContaining('Demand: STRONG'), findsOneWidget);
      expect(find.text('Consider raising your price.'), findsOneWidget);
      expect(find.textContaining('You: 40%'), findsOneWidget);
      expect(find.textContaining('Player A: 20% (unmet demand)'), findsOneWidget);
    });

    testWidgets('shows only the first active market event as a banner', (tester) async {
      const secondEvent = MarketEvent(
        id: 'event-2',
        title: 'Second Event',
        description: 'Should not show.',
        magnitudeMultiplier: 1,
        ticksRemaining: 5,
        affectedResourceName: null,
      );
      await _pump(tester, marketEvents: const [_marketEvent, secondEvent]);

      expect(find.text('Commodity Shock'), findsOneWidget);
      expect(find.text('Steel prices are spiking city-wide.'), findsOneWidget);
      expect(find.text('Second Event'), findsNothing);
    });

    testWidgets('pre-fills the threshold field and saves a parsed value', (tester) async {
      double? saved;
      var called = false;
      await _pump(
        tester,
        currentThreshold: 50,
        onSaveThreshold: (value) async {
          called = true;
          saved = value;
        },
      );

      expect(find.text('50'), findsOneWidget);

      await tester.enterText(find.byKey(const ValueKey('public-sales-threshold-input')), '75');
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pumpAndSettle();

      expect(called, isTrue);
      expect(saved, 75);
      expect(find.text('Alert threshold saved.'), findsOneWidget);
    });

    testWidgets('an empty threshold saves null to disable the alert', (tester) async {
      double? saved = 999;
      var sawNull = false;
      await _pump(
        tester,
        currentThreshold: 50,
        onSaveThreshold: (value) async {
          saved = value;
          sawNull = value == null;
        },
      );

      await tester.enterText(find.byKey(const ValueKey('public-sales-threshold-input')), '');
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pumpAndSettle();

      expect(sawNull, isTrue);
      expect(saved, isNull);
    });

    testWidgets('rejects a negative threshold without calling the save callback', (tester) async {
      var called = false;
      await _pump(tester, onSaveThreshold: (_) async => called = true);

      await tester.enterText(find.byKey(const ValueKey('public-sales-threshold-input')), '-5');
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pumpAndSettle();

      expect(called, isFalse);
      expect(find.textContaining('Enter a positive number'), findsOneWidget);
    });

    testWidgets('flushing storage requires confirmation before calling the callback', (tester) async {
      var called = false;
      await _pump(tester, onFlushStorage: () async => called = true);

      await tester.tap(find.widgetWithText(OutlinedButton, 'Discard All Inventory'));
      await tester.pumpAndSettle();

      expect(find.text('Discard all inventory?'), findsOneWidget);
      expect(called, isFalse);

      await tester.tap(find.widgetWithText(FilledButton, 'Yes, Discard All'));
      await tester.pumpAndSettle();

      expect(called, isTrue);
    });

    testWidgets('cancelling the flush confirm dialog does not call the callback', (tester) async {
      var called = false;
      await _pump(tester, onFlushStorage: () async => called = true);

      await tester.tap(find.widgetWithText(OutlinedButton, 'Discard All Inventory'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(called, isFalse);
    });
  });
}
