import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/bounties/bounty_history_screen.dart';
import 'package:capitalism_app/features/bounties/bounty_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_bounty_service.dart';
import 'support/in_memory_token_storage.dart';

const _firstSale = CompletedBounty(
  id: 'bounty-1',
  bountyCode: 'FIRST_SALE',
  bountyDisplayName: 'First Sale',
  pointsAwarded: 50,
  status: 'AWARDED',
  serverKey: 'prod',
  eventDateUtc: '2026-01-01T00:00:00Z',
  awardedAtUtc: '2026-01-01T00:05:00Z',
);

const _tenBuildings = CompletedBounty(
  id: 'bounty-2',
  bountyCode: 'TEN_BUILDINGS',
  bountyDisplayName: 'Ten Buildings',
  pointsAwarded: 120,
  status: 'AWARDED',
  serverKey: 'prod',
  eventDateUtc: '2026-02-01T00:00:00Z',
  awardedAtUtc: '2026-02-01T00:05:00Z',
);

Future<void> _pump(WidgetTester tester, {required FakeBountyService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));

  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');

  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(
      value: auth,
      child: MaterialApp(home: Scaffold(body: BountyHistoryScreen(bountyService: service))),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('BountyHistoryScreen', () {
    testWidgets('shows completed bounties with points and a running total', (tester) async {
      await _pump(tester, service: FakeBountyService(bounties: [_firstSale, _tenBuildings]));

      expect(find.text('First Sale'), findsOneWidget);
      expect(find.text('Ten Buildings'), findsOneWidget);
      expect(find.text('+50'), findsOneWidget);
      expect(find.text('+120'), findsOneWidget);
      expect(find.text('2 bounty(ies) awarded · 170 points total'), findsOneWidget);
    });

    testWidgets('shows empty state when no bounties are completed yet', (tester) async {
      await _pump(tester, service: FakeBountyService(bounties: const []));

      expect(find.text('No bounties completed yet.'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      await _pump(tester, service: FakeBountyService(error: Exception('down')));

      expect(find.text('Could not load your bounties. Please try again.'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Try again'), findsOneWidget);
    });
  });
}
