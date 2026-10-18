import 'package:capitalism_app/features/chat/chat_panel.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/app_harness.dart';
import 'support/fake_chat_service.dart';
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
    testWidgets('opens from the drawer with the real chat and can be dismissed', (tester) async {
      final chat = FakeChatService(messages: [chatMessage('1', content: 'Welcome to the game')]);
      await pumpCapitalismApp(tester, authenticated: true, chatService: chat);
      await _openDrawer(tester);

      await tester.tap(find.widgetWithText(ListTile, 'Chat'));
      await tester.pumpAndSettle();

      expect(find.byType(ChatPanel), findsOneWidget);
      expect(find.text('Welcome to the game'), findsOneWidget);
      expect(find.text('Not implemented yet. Mirrors the chat side panel in AppHeader.vue.'), findsNothing);

      await tester.tap(find.byTooltip('Close'));
      await tester.pumpAndSettle();

      expect(find.byType(ChatPanel), findsNothing);
    });

    testWidgets('the drawer item shows the unread count and the badge clears after opening the chat', (tester) async {
      final chat = FakeChatService(messages: [chatMessage('1'), chatMessage('2'), chatMessage('3')]);
      await pumpCapitalismApp(tester, authenticated: true, chatService: chat);
      await _openDrawer(tester);

      expect(find.descendant(of: find.widgetWithText(ListTile, 'Chat'), matching: find.text('3')), findsOneWidget);

      await tester.tap(find.widgetWithText(ListTile, 'Chat'));
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('Close'));
      await tester.pumpAndSettle();
      await _openDrawer(tester);

      expect(find.descendant(of: find.widgetWithText(ListTile, 'Chat'), matching: find.text('3')), findsNothing);
    });
  });
}
