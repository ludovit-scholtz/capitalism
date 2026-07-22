import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/news/notification_models.dart';
import 'package:capitalism_app/features/news/notifications_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_notifications_service.dart';
import 'support/in_memory_token_storage.dart';

const _unread = PlayerNotification(
  id: 'notif-1',
  type: 'SHIPMENT_ARRIVED',
  severity: 'INFO',
  title: 'Shipment arrived',
  message: 'Your goods have arrived.',
  isRead: false,
  createdAtUtc: '2026-07-20T10:00:00Z',
  companyId: null,
  buildingId: null,
  bankAccountId: null,
  loanId: null,
);

const _buildingAlert = PlayerNotification(
  id: 'notif-2',
  type: 'GENERIC',
  severity: 'CRITICAL',
  title: 'Building at risk',
  message: 'Power outage detected.',
  isRead: true,
  createdAtUtc: '2026-07-19T09:00:00Z',
  companyId: null,
  buildingId: 'building-42',
  bankAccountId: null,
  loanId: null,
);

Future<GoRouter> _pumpNotifications(WidgetTester tester, {required FakeNotificationsService service}) async {
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => NotificationsScreen(notificationsService: service)),
      GoRoute(path: '/building/:id', builder: (context, state) => Scaffold(body: Text('Building ${state.pathParameters['id']}'))),
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
  group('NotificationsScreen', () {
    testWidgets('shows notifications grouped and auto-marks unread as read', (tester) async {
      final service = FakeNotificationsService(inbox: const NotificationInbox(unreadCount: 1, items: [_unread, _buildingAlert]));

      await _pumpNotifications(tester, service: service);

      expect(find.text('Shipment arrived'), findsOneWidget);
      expect(find.text('Building at risk'), findsOneWidget);
      expect(service.markedReadIds, ['notif-1']);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeNotificationsService(fetchError: Exception('down'));

      await _pumpNotifications(tester, service: service);

      expect(find.text('Could not load notifications. Please try again.'), findsOneWidget);
    });

    testWidgets('tapping a notification with a buildingId navigates to /building/:id', (tester) async {
      final service = FakeNotificationsService(inbox: const NotificationInbox(unreadCount: 0, items: [_buildingAlert]));

      await _pumpNotifications(tester, service: service);
      await tester.tap(find.text('Building at risk'));
      await tester.pumpAndSettle();

      expect(find.text('Building building-42'), findsOneWidget);
    });

    testWidgets('mark all read button clears the inbox via markAllRead', (tester) async {
      final service = FakeNotificationsService(inbox: const NotificationInbox(unreadCount: 1, items: [_unread]));

      await _pumpNotifications(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Mark all read'));
      await tester.pumpAndSettle();

      expect(service.markAllReadCallCount, 1);
    });
  });
}
