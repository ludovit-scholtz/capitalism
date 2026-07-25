import 'package:capitalism_app/features/buildings/building_panel_models.dart';
import 'package:capitalism_app/features/buildings/building_research_panel.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _productBrand = CompanyBrand(
  id: 'brand-1',
  name: 'Acme Steel',
  scope: 'PRODUCT',
  awareness: 0.4,
  quality: 0.6,
  marketingQuality: 0.2,
  marketingEfficiencyMultiplier: 1.3,
  productName: 'Steel Beams',
  accumulatedResearchBudget: 12000,
  baseResearchBudget: 20000,
);

Future<void> _pump(
  WidgetTester tester, {
  List<CompanyBrand> brands = const [],
  bool loading = false,
  bool hasConfiguredRdUnits = false,
  // A visible indeterminate LinearProgressIndicator animates forever, so
  // pumpAndSettle never settles while one is on screen.
  bool settle = true,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: BuildingResearchPanel(brands: brands, loading: loading, hasConfiguredRdUnits: hasConfiguredRdUnits),
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
  group('BuildingResearchPanel', () {
    testWidgets('shows an empty-state message when no research has been recorded', (tester) async {
      await _pump(tester, hasConfiguredRdUnits: false);
      expect(find.textContaining('No research recorded yet'), findsOneWidget);
    });

    testWidgets('shows a pending message when units are configured but no brand data yet', (tester) async {
      await _pump(tester, hasConfiguredRdUnits: true);
      expect(find.textContaining('pending activation'), findsOneWidget);
    });

    testWidgets('renders a brand card with quality/awareness progress and a Lv badge', (tester) async {
      await _pump(tester, brands: const [_productBrand]);

      expect(find.byKey(const ValueKey('brand-brand-1')), findsOneWidget);
      expect(find.text('Steel Beams'), findsOneWidget);
      expect(find.textContaining('Product Quality: 60%'), findsOneWidget);
      expect(find.textContaining('Brand Awareness: 40%'), findsOneWidget);
      expect(find.textContaining('Research budget invested'), findsOneWidget);
    });

    testWidgets('shows a loading indicator while loading', (tester) async {
      await _pump(tester, loading: true, settle: false);
      expect(find.byType(LinearProgressIndicator), findsOneWidget);
    });
  });
}
