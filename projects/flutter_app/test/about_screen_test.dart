import 'dart:convert';

import 'package:capitalism_app/core/services/app_logger.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'support/app_harness.dart';

/// Serves the Home screen's status query normally but fails the News
/// screen's `GameNewsFeed` query with a GraphQL error, so we can exercise
/// the real [GraphQlService] failure path (not a fake service) and verify
/// it lands in [AppLogger] for the Dev Info screen to show.
http.Client _newsFailingClient() {
  return MockClient((request) async {
    final body = jsonDecode(request.body) as Map<String, dynamic>;
    final query = body['query'] as String;
    if (query.contains('GameNewsFeed')) {
      return http.Response(
        jsonEncode({
          'errors': [
            {'message': 'Simulated server failure'},
          ],
        }),
        200,
      );
    }
    return http.Response(
      jsonEncode({
        'data': {
          'gameState': {'currentTick': 1234, 'taxRate': 15.0},
          'rankings': <Map<String, dynamic>>[],
        },
      }),
      200,
    );
  });
}

void main() {
  setUp(() => AppLogger.instance.clear());

  testWidgets('About screen shows app info and links to Dev info', (tester) async {
    await pumpCapitalismApp(tester, httpClient: _newsFailingClient());

    await tester.tap(find.byIcon(Icons.menu));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(ListTile, 'About'));
    await tester.pumpAndSettle();

    expect(find.text('Capitalism 5'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Dev info'), findsOneWidget);
  });

  testWidgets('Dev info screen surfaces a logged GraphQL failure', (tester) async {
    await pumpCapitalismApp(tester, httpClient: _newsFailingClient());

    // Trigger a real network failure through the News screen so
    // GraphQlService logs it.
    await tester.tap(find.text('News'));
    await tester.pumpAndSettle();
    expect(find.text('Could not load the news feed. Please try again.'), findsOneWidget);

    await tester.tap(find.byIcon(Icons.menu));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ListTile, 'About'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(OutlinedButton, 'Dev info'));
    await tester.pumpAndSettle();

    expect(find.textContaining('GameNewsFeed'), findsWidgets);
    expect(find.textContaining('Simulated server failure'), findsWidgets);
  });

  testWidgets('Dev info screen clear button empties the log', (tester) async {
    AppLogger.instance.info('pre-existing entry', tag: 'Test');
    await pumpCapitalismApp(tester, httpClient: _newsFailingClient());

    await tester.tap(find.byIcon(Icons.menu));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ListTile, 'About'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(OutlinedButton, 'Dev info'));
    await tester.pumpAndSettle();

    expect(find.textContaining('pre-existing entry'), findsWidgets);

    await tester.tap(find.byTooltip('Clear log'));
    await tester.pumpAndSettle();

    expect(find.textContaining('pre-existing entry'), findsNothing);
    expect(find.textContaining('No log entries yet'), findsOneWidget);
  });
}
