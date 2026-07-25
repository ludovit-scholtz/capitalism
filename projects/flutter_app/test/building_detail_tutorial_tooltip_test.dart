// First-time-user tooltip sequencing on Building Detail (ROADMAP 138b):
// the building-detail tooltip shows first (once, per incomplete milestone),
// dismissing it marks `FIRST_BUILDING_DETAIL_VISIT` complete, and only then
// can the grid-editor tooltip appear (and only once edit mode is entered).

import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/context/recent_building_state.dart';
import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_detail_screen.dart';
import 'package:capitalism_app/features/tutorial/tutorial_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_building_analytics_service.dart';
import 'support/fake_building_detail_service.dart';
import 'support/fake_building_sales_service.dart';
import 'support/fake_building_sourcing_service.dart';
import 'support/fake_tutorial_service.dart';
import 'support/in_memory_selected_building_storage.dart';
import 'support/in_memory_token_storage.dart';

const _emptyFactory = BuildingDetail(
  id: 'building-1',
  companyId: 'company-1',
  name: 'Empty Factory',
  type: 'FACTORY',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: 0,
  isForSale: false,
  units: [],
  pendingConfiguration: null,
);

Future<void> _pumpBuildingDetail(WidgetTester tester, {required FakeBuildingDetailService service, required FakeTutorialService tutorialService}) async {
  await tester.binding.setSurfaceSize(const Size(900, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/',
        builder: (context, state) => Scaffold(
          body: BuildingDetailScreen(
            buildingId: 'building-1',
            buildingDetailService: service,
            tutorialService: tutorialService,
            buildingSalesService: FakeBuildingSalesService(),
            buildingSourcingService: FakeBuildingSourcingService(),
            buildingAnalyticsService: FakeBuildingAnalyticsService(),
          ),
        ),
      ),
      GoRoute(path: '/building/:id/sell', builder: (context, state) => Scaffold(body: Text('Sell ${state.pathParameters['id']}'))),
    ],
  );
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<RecentBuildingState>.value(value: RecentBuildingState(storage: InMemorySelectedBuildingStorage())),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
  // `pumpAndSettle` only advances the fake clock while a frame keeps being
  // scheduled (e.g. an active animation) — with nothing animating here, it
  // settles almost immediately and never reaches the tooltip-readiness
  // `Timer`'s 800ms mark on its own, so advance it explicitly.
  await tester.pump(const Duration(milliseconds: 850));
  await tester.pumpAndSettle();
}

void main() {
  group('BuildingDetailScreen tutorial tooltips', () {
    testWidgets('shows the building-detail tooltip and marks it complete on dismiss', (tester) async {
      final service = FakeBuildingDetailService(building: _emptyFactory);
      final tutorialService = FakeTutorialService();
      await _pumpBuildingDetail(tester, service: service, tutorialService: tutorialService);

      expect(find.text('Building Detail View'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Got it'));
      await tester.pumpAndSettle();

      expect(find.text('Building Detail View'), findsNothing);
      expect(tutorialService.markedComplete, contains('FIRST_BUILDING_DETAIL_VISIT'));
    });

    testWidgets('does not show the building-detail tooltip once the milestone is already completed', (tester) async {
      final service = FakeBuildingDetailService(building: _emptyFactory);
      final tutorialService = FakeTutorialService(
        statuses: const [
          TutorialMilestoneStatus(milestone: 'FIRST_BUILDING_DETAIL_VISIT', isCompleted: true, bountyAwarded: true, bountyPoints: 30),
        ],
      );
      await _pumpBuildingDetail(tester, service: service, tutorialService: tutorialService);

      expect(find.text('Building Detail View'), findsNothing);
    });

    testWidgets('shows the grid-editor tooltip only after entering edit mode, sequenced after the building-detail one', (tester) async {
      final service = FakeBuildingDetailService(building: _emptyFactory);
      final tutorialService = FakeTutorialService(
        statuses: const [
          TutorialMilestoneStatus(milestone: 'FIRST_BUILDING_DETAIL_VISIT', isCompleted: true, bountyAwarded: true, bountyPoints: 30),
        ],
      );
      await _pumpBuildingDetail(tester, service: service, tutorialService: tutorialService);

      // Building-detail tooltip already done — grid-editor tooltip must not
      // show yet either, since edit mode hasn't been entered.
      expect(find.text('Unit Grid Editor'), findsNothing);

      await tester.tap(find.widgetWithText(TextButton, 'Edit Building'));
      await tester.pumpAndSettle();

      expect(find.text('Unit Grid Editor'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Got it'));
      await tester.pumpAndSettle();

      expect(find.text('Unit Grid Editor'), findsNothing);
      expect(tutorialService.markedComplete, contains('FIRST_GRID_EDITOR_OPEN'));
    });

    testWidgets('does not show the grid-editor tooltip while the building-detail tooltip is still showing', (tester) async {
      final service = FakeBuildingDetailService(building: _emptyFactory);
      final tutorialService = FakeTutorialService();
      await _pumpBuildingDetail(tester, service: service, tutorialService: tutorialService);

      expect(find.text('Building Detail View'), findsOneWidget);

      await tester.tap(find.widgetWithText(TextButton, 'Edit Building'));
      await tester.pumpAndSettle();

      expect(find.text('Unit Grid Editor'), findsNothing);
    });
  });
}
