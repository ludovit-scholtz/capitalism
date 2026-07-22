import 'package:capitalism_app/features/chat/chat_panel.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/app_harness.dart';
import 'support/fake_url_opener.dart';

Future<void> _openDrawer(WidgetTester tester) async {
  await tester.tap(find.byIcon(Icons.menu));
  await tester.pumpAndSettle();
}

void main() {
  group('Discord nav item', () {
    testWidgets('opens the external link instead of navigating', (tester) async {
      final fakeOpener = FakeUrlOpener();
      await pumpCapitalismApp(tester, urlOpener: fakeOpener);
      await _openDrawer(tester);

      await tester.tap(find.widgetWithText(ListTile, 'Discord'));
      await tester.pumpAndSettle();

      expect(fakeOpener.openedUrls, ['https://discord.gg/PhHSxJvDn6']);
      expect(find.text('Get Started'), findsOneWidget); // still on Home, nothing navigated
      expect(find.byType(Drawer), findsNothing); // drawer still closed itself
    });
  });

  group('Chat panel', () {
    testWidgets('opens from the drawer and can be dismissed', (tester) async {
      await pumpCapitalismApp(tester, authenticated: true);
      await _openDrawer(tester);

      await tester.tap(find.widgetWithText(ListTile, 'Chat'));
      await tester.pumpAndSettle();

      expect(find.byType(ChatPanel), findsOneWidget);
      expect(find.text('Not implemented yet. Mirrors the chat side panel in AppHeader.vue.'), findsOneWidget);

      await tester.tap(find.byTooltip('Close'));
      await tester.pumpAndSettle();

      expect(find.byType(ChatPanel), findsNothing);
    });
  });
}
