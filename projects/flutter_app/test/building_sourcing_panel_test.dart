import 'package:capitalism_app/features/buildings/building_sourcing_models.dart';
import 'package:capitalism_app/features/buildings/building_sourcing_panel.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _blockedPreview = ProcurementPreview(
  sourceType: 'GLOBAL_EXCHANGE',
  sourceCityName: 'Prague',
  sourceVendorName: null,
  exchangePricePerUnit: 10,
  transitCostPerUnit: 2,
  deliveredPricePerUnit: 12,
  estimatedQuality: 0.7,
  canExecute: false,
  blockReason: 'MAX_PRICE',
  blockMessage: 'Delivered price exceeds this unit\'s max price.',
);

const _executablePreview = ProcurementPreview(
  sourceType: 'LOCKED_VENDOR',
  sourceCityName: 'Prague',
  sourceVendorName: 'Acme Steelworks',
  exchangePricePerUnit: 9,
  transitCostPerUnit: 1,
  deliveredPricePerUnit: 10,
  estimatedQuality: 0.9,
  canExecute: true,
  blockReason: null,
  blockMessage: null,
);

const _recommendedCandidate = SourcingCandidate(
  sourceType: 'GLOBAL_EXCHANGE',
  sourceCityName: 'Prague',
  sourceVendorName: null,
  exchangePricePerUnit: 9,
  transitCostPerUnit: 1,
  deliveredPricePerUnit: 10,
  estimatedQuality: 0.9,
  distanceKm: 120,
  isEligible: true,
  blockReason: null,
  blockMessage: null,
  isRecommended: true,
  rank: 1,
);

const _blockedCandidate = SourcingCandidate(
  sourceType: 'GLOBAL_EXCHANGE',
  sourceCityName: 'Berlin',
  sourceVendorName: null,
  exchangePricePerUnit: 6,
  transitCostPerUnit: 8,
  deliveredPricePerUnit: 14,
  estimatedQuality: 0.5,
  distanceKm: 900,
  isEligible: false,
  blockReason: 'MIN_QUALITY',
  blockMessage: 'Quality below this unit\'s minimum.',
  isRecommended: false,
  rank: 2,
);

Future<void> _pump(
  WidgetTester tester, {
  ProcurementPreview? preview,
  List<SourcingCandidate> candidates = const [],
  bool loading = false,
  // A visible indeterminate LinearProgressIndicator animates forever, so
  // pumpAndSettle never settles while one is on screen.
  bool settle = true,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(body: SingleChildScrollView(child: SourcingComparisonPanel(preview: preview, candidates: candidates, loading: loading))),
    ),
  );
  if (settle) {
    await tester.pumpAndSettle();
  } else {
    await tester.pump();
  }
}

void main() {
  group('SourcingComparisonPanel', () {
    testWidgets('shows a loading indicator while loading', (tester) async {
      await _pump(tester, loading: true, settle: false);
      expect(find.byType(LinearProgressIndicator), findsOneWidget);
    });

    testWidgets('shows an empty state with nothing to compare', (tester) async {
      await _pump(tester);
      expect(find.text('No sourcing options available.'), findsOneWidget);
    });

    testWidgets('shows a blocked procurement preview with its block message', (tester) async {
      await _pump(tester, preview: _blockedPreview);
      expect(find.text('Blocked next tick'), findsOneWidget);
      expect(find.text('Delivered price exceeds this unit\'s max price.'), findsOneWidget);
    });

    testWidgets('shows an executable procurement preview with vendor/price detail', (tester) async {
      await _pump(tester, preview: _executablePreview);
      expect(find.text('Will execute next tick'), findsOneWidget);
      expect(find.text('Vendor: Acme Steelworks'), findsOneWidget);
      expect(find.text('Delivered price: 10.00'), findsOneWidget);
    });

    testWidgets('marks the recommended candidate with a star and no blocked chip', (tester) async {
      await _pump(tester, candidates: const [_recommendedCandidate]);
      expect(find.text('★'), findsOneWidget);
      expect(find.text('Blocked'), findsNothing);
    });

    testWidgets('shows a blocked chip and reason for an ineligible candidate', (tester) async {
      await _pump(tester, candidates: const [_recommendedCandidate, _blockedCandidate]);
      expect(find.text('Blocked'), findsOneWidget);
      expect(find.text('Quality below this unit\'s minimum.'), findsOneWidget);
    });
  });
}
