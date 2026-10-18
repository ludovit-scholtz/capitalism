import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/context/account_context_models.dart';
import 'package:capitalism_app/core/context/account_context_state.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/features/chat/chat_models.dart';
import 'package:capitalism_app/features/chat/chat_state.dart';
import 'package:capitalism_app/features/chat/chat_view.dart';
import 'package:capitalism_app/l10n/generated/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:provider/provider.dart';

import 'support/fake_chat_service.dart';
import 'support/in_memory_selected_city_storage.dart';
import 'support/in_memory_selected_locale_storage.dart';
import 'support/in_memory_token_storage.dart';

class _Harness {
  _Harness(this.chat, this.auth, this.context);

  final ChatState chat;
  final AuthState auth;
  final AccountContextState context;
}

Future<_Harness> _pumpChat(
  WidgetTester tester,
  FakeChatService service, {
  bool signedIn = true,
  String? selectedCityId,
  List<ContextCityOption> cities = const [],
}) async {
  await initializeDateFormatting();
  final auth = AuthState(storage: InMemoryTokenStorage());
  if (signedIn) await auth.setToken('test-token');
  final context = AccountContextState(storage: InMemorySelectedCityStorage())
    ..cities = cities
    ..selectedCityId = selectedCityId;
  final chat = ChatState(service: service, storage: InMemoryChatReadStorage(), pollingEnabled: false)..bindAuth(auth);
  addTearDown(chat.dispose);

  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<AccountContextState>.value(value: context),
        ChangeNotifierProvider<LocaleState>.value(value: LocaleState(storage: InMemorySelectedLocaleStorage())),
        ChangeNotifierProvider<ChatState>.value(value: chat),
      ],
      child: const MaterialApp(
        localizationsDelegates: [
          AppLocalizations.delegate,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        supportedLocales: AppLocalizations.supportedLocales,
        home: Scaffold(body: Padding(padding: EdgeInsets.all(8), child: ChatView())),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return _Harness(chat, auth, context);
}

void main() {
  testWidgets('renders history with author and marks the panel as read', (tester) async {
    final service = FakeChatService(messages: [chatMessage('1', author: 'Alice', content: 'Hello world'), chatMessage('2', author: 'Bob', content: 'Hi Alice')]);

    final h = await _pumpChat(tester, service);

    expect(find.text('Hello world'), findsOneWidget);
    expect(find.text('Hi Alice'), findsOneWidget);
    expect(find.textContaining('Alice'), findsWidgets);
    expect(h.chat.unreadCount, 0);
    expect(h.chat.panelOpen, isTrue);
  });

  testWidgets('shows the empty state when there are no messages', (tester) async {
    await _pumpChat(tester, FakeChatService());

    expect(find.text('No messages yet. Say hello!'), findsOneWidget);
  });

  testWidgets('shows a retry action when the history cannot be loaded and recovers on retry', (tester) async {
    final service = FakeChatService()..fetchError = Exception('offline');
    await _pumpChat(tester, service);
    expect(find.text('Could not load the chat.'), findsOneWidget);

    service.fetchError = null;
    service.messages = [chatMessage('1', content: 'Back online')];
    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();

    expect(find.text('Back online'), findsOneWidget);
    expect(find.text('Could not load the chat.'), findsNothing);
  });

  testWidgets('sending a message shows it, clears the composer and calls the server once', (tester) async {
    final service = FakeChatService();
    await _pumpChat(tester, service);

    await tester.enterText(find.byType(TextField), '  Good morning  ');
    await tester.pump();
    await tester.tap(find.byTooltip('Send'));
    await tester.pumpAndSettle();

    expect(service.sent.map((m) => m.content), ['Good morning']);
    expect(find.text('Good morning'), findsOneWidget);
    expect(tester.widget<TextField>(find.byType(TextField)).controller!.text, isEmpty);
  });

  testWidgets('the send button is disabled for empty or too long text', (tester) async {
    await _pumpChat(tester, FakeChatService());
    IconButton button() => tester.widget<IconButton>(find.byType(IconButton));

    expect(button().onPressed, isNull);

    await tester.enterText(find.byType(TextField), 'x' * (maxChatLength + 1));
    await tester.pump();
    expect(button().onPressed, isNull);
    expect(find.text('Message is too long (maximum 500 characters).'), findsWidgets);

    await tester.enterText(find.byType(TextField), 'ok');
    await tester.pump();
    expect(button().onPressed, isNotNull);
  });

  testWidgets('shows the character counter near the limit', (tester) async {
    await _pumpChat(tester, FakeChatService());

    await tester.enterText(find.byType(TextField), 'y' * chatCounterThreshold);
    await tester.pump();

    expect(find.text('$chatCounterThreshold/$maxChatLength'), findsOneWidget);
  });

  testWidgets('a rate limited send keeps the text and explains why', (tester) async {
    final service = FakeChatService()..sendErrorCode = 'RATE_LIMITED';
    await _pumpChat(tester, service);

    await tester.enterText(find.byType(TextField), 'spam');
    await tester.pump();
    await tester.tap(find.byTooltip('Send'));
    await tester.pumpAndSettle();

    expect(find.text('You are sending messages too quickly. Please wait a moment.'), findsOneWidget);
    expect(tester.widget<TextField>(find.byType(TextField)).controller!.text, 'spam');
  });

  testWidgets('a failed send shows the generic error', (tester) async {
    final service = FakeChatService()..sendErrorCode = 'BOOM';
    await _pumpChat(tester, service);

    await tester.enterText(find.byType(TextField), 'hello');
    await tester.pump();
    await tester.tap(find.byTooltip('Send'));
    await tester.pumpAndSettle();

    expect(find.text('The message could not be sent.'), findsOneWidget);
  });

  testWidgets('admin-hidden authors: others see a removed placeholder, the author sees a note', (tester) async {
    final service = FakeChatService(
      messages: [
        chatMessage('1', author: 'Hidden', content: 'secret text', removed: true),
        chatMessage('2', author: 'Me', content: 'my words', own: true, visible: false),
      ],
    );
    await _pumpChat(tester, service);

    expect(find.text('Message removed'), findsOneWidget);
    expect(find.text('secret text'), findsNothing); // the server never sends it, and the UI never shows it
    expect(find.text('my words'), findsOneWidget);
    expect(find.text('Only you can see this message'), findsOneWidget);
  });

  testWidgets('with a selected city the channel switch loads and sends into the city channel', (tester) async {
    final service = FakeChatService(messages: [chatMessage('g1', content: 'global hello')]);
    service.byCity['city-1'] = [chatMessage('c1', cityId: 'city-1', content: 'city hello')];
    await _pumpChat(
      tester,
      service,
      selectedCityId: 'city-1',
      cities: const [ContextCityOption(id: 'city-1', name: 'Bratislava', countryCode: 'SK')],
    );
    expect(find.text('global hello'), findsOneWidget);

    await tester.tap(find.text('Bratislava'));
    await tester.pumpAndSettle();
    expect(find.text('city hello'), findsOneWidget);
    expect(find.text('global hello'), findsNothing);

    await tester.enterText(find.byType(TextField), 'for the city');
    await tester.pump();
    await tester.tap(find.byTooltip('Send'));
    await tester.pumpAndSettle();
    expect(service.sent.single.cityId, 'city-1');

    await tester.tap(find.text('Global'));
    await tester.pumpAndSettle();
    expect(find.text('global hello'), findsOneWidget);
  });

  testWidgets('no channel switch is offered without a selected city', (tester) async {
    await _pumpChat(tester, FakeChatService(messages: [chatMessage('1')]));

    expect(find.byType(SegmentedButton<bool>), findsNothing);
  });

  testWidgets('a signed-out player is asked to sign in and nothing is fetched', (tester) async {
    final service = FakeChatService(messages: [chatMessage('1')]);

    await _pumpChat(tester, service, signedIn: false);

    expect(find.text('Sign in to chat.'), findsOneWidget);
    expect(service.fetchCount, 0);
  });
}
