import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/news/news_models.dart';
import 'package:capitalism_app/features/news/news_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_news_service.dart';
import 'support/in_memory_token_storage.dart';

const _entry = GameNewsEntry(
  id: 'entry-1',
  entryType: 'NEWS',
  publishedAtUtc: '2026-07-01T00:00:00Z',
  updatedAtUtc: null,
  isRead: false,
  localizations: [
    GameNewsLocalization(locale: 'en', title: 'New Feature Launched', summary: null, htmlContent: '<p>Details here.</p>'),
  ],
);

const _reportEntry = GameNewsEntry(
  id: 'entry-2',
  entryType: 'MARKET_REPORT',
  publishedAtUtc: '2026-07-02T00:00:00Z',
  updatedAtUtc: null,
  isRead: true,
  localizations: [GameNewsLocalization(locale: 'en', title: 'Weekly Market Report', summary: null, htmlContent: 'Prices rose.')],
);

Future<AuthState> _pumpNews(WidgetTester tester, {required FakeNewsService service, bool authenticated = true}) async {
  final auth = AuthState(storage: InMemoryTokenStorage());
  if (authenticated) {
    await auth.setToken('test-token');
  }
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(
      value: auth,
      child: MaterialApp(home: Scaffold(body: NewsScreen(newsService: service))),
    ),
  );
  await tester.pumpAndSettle();
  return auth;
}

void main() {
  group('NewsScreen', () {
    testWidgets('shows entries and auto-marks-all-read when authenticated with unread items', (tester) async {
      final service = FakeNewsService(feed: const GameNewsFeed(unreadCount: 1, items: [_entry]));

      await _pumpNews(tester, service: service);

      expect(find.text('New Feature Launched'), findsOneWidget);
      expect(find.textContaining('Details here.'), findsOneWidget);
      expect(service.markAllReadCallCount, 1);
    });

    testWidgets('does not auto-mark-read when unauthenticated', (tester) async {
      final service = FakeNewsService(feed: const GameNewsFeed(unreadCount: 1, items: [_entry]));

      await _pumpNews(tester, service: service, authenticated: false);

      expect(service.markAllReadCallCount, 0);
    });

    testWidgets('filtering by MARKET_REPORT shows only market reports', (tester) async {
      final service = FakeNewsService(feed: const GameNewsFeed(unreadCount: 0, items: [_entry, _reportEntry]));

      await _pumpNews(tester, service: service);
      expect(find.text('New Feature Launched'), findsOneWidget);
      expect(find.text('Weekly Market Report'), findsOneWidget);

      await tester.tap(find.widgetWithText(ChoiceChip, 'MARKET_REPORT'));
      await tester.pumpAndSettle();

      expect(find.text('New Feature Launched'), findsNothing);
      expect(find.text('Weekly Market Report'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeNewsService(fetchError: Exception('network down'));

      await _pumpNews(tester, service: service);

      expect(find.text('Could not load the news feed. Please try again.'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Try again'), findsOneWidget);
    });

    testWidgets('mark all read button shows confirm dialog and re-fetches', (tester) async {
      final service = FakeNewsService(feed: const GameNewsFeed(unreadCount: 1, items: [_reportEntry]));
      // reportEntry.isRead is true so unreadCount>0 drives the button, but no auto-mark call fires (already read items).
      await _pumpNews(tester, service: service);
      service.calls.clear();

      await tester.tap(find.widgetWithText(OutlinedButton, 'Mark all read'));
      await tester.pumpAndSettle();
      expect(find.text('Mark all as read?'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Mark all read'));
      await tester.pumpAndSettle();

      expect(service.calls, ['markAllRead', 'fetchFeed']);
    });
  });
}
