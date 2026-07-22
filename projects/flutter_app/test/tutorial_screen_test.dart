import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/tutorial/tutorial_models.dart';
import 'package:capitalism_app/features/tutorial/tutorial_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_tutorial_service.dart';
import 'support/in_memory_token_storage.dart';

Future<GoRouter> _pumpTutorial(WidgetTester tester, {required FakeTutorialService service, bool authenticated = true}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  if (authenticated) await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: TutorialScreen(tutorialService: service))),
      GoRoute(path: '/login', builder: (context, state) => const Scaffold(body: Text('Login Screen'))),
      GoRoute(path: '/dashboard', builder: (context, state) => const Scaffold(body: Text('Dashboard Screen'))),
    ],
  );
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
  return router;
}

void main() {
  group('TutorialScreen', () {
    testWidgets('shows the milestone list with progress for authenticated players', (tester) async {
      final service = FakeTutorialService(
        statuses: const [TutorialMilestoneStatus(milestone: 'FIRST_RESOURCE_SOLD', isCompleted: true, bountyAwarded: true, bountyPoints: 50)],
      );

      await _pumpTutorial(tester, service: service);

      expect(find.text('First Sale'), findsOneWidget);
      expect(find.text('1 / 7 complete'), findsOneWidget);
      expect(find.text('Bounty earned'), findsOneWidget);
    });

    testWidgets('does not fetch progress when unauthenticated and shows a sign-in prompt', (tester) async {
      final service = FakeTutorialService();

      await _pumpTutorial(tester, service: service, authenticated: false);

      expect(service.calls, isEmpty);
      expect(find.text('Sign in to track your tutorial progress and earn bounty points.'), findsOneWidget);
    });

    testWidgets('Resume button navigates to the milestone route', (tester) async {
      final service = FakeTutorialService(statuses: const []);

      await _pumpTutorial(tester, service: service);
      await tester.tap(find.widgetWithText(FilledButton, 'Resume').first);
      await tester.pumpAndSettle();

      expect(find.text('Dashboard Screen'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeTutorialService(fetchError: Exception('down'));

      await _pumpTutorial(tester, service: service);

      expect(find.text('Could not load your tutorial progress. Please try again.'), findsOneWidget);
    });
  });
}
