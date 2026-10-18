import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/chat/chat_models.dart';
import 'package:capitalism_app/features/chat/chat_state.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_chat_service.dart';
import 'support/in_memory_token_storage.dart';

Future<AuthState> _signedIn() async {
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  return auth;
}

Future<ChatState> _chatState(FakeChatService service, {String? lastSeen, bool signedIn = true}) async {
  final auth = signedIn ? await _signedIn() : AuthState(storage: InMemoryTokenStorage());
  final state = ChatState(service: service, storage: InMemoryChatReadStorage(lastSeen), pollingEnabled: false)..bindAuth(auth);
  addTearDown(state.dispose);
  await Future<void>.delayed(Duration.zero);
  return state;
}

void main() {
  group('computeUnreadCount', () {
    test('is zero while the panel is open', () {
      expect(computeUnreadCount(messageIds: ['a', 'b'], lastSeenMessageId: null, panelOpen: true), 0);
    });

    test('counts every message when nothing was seen yet', () {
      expect(computeUnreadCount(messageIds: ['a', 'b', 'c'], lastSeenMessageId: null, panelOpen: false), 3);
    });

    test('counts only messages after the last seen one', () {
      expect(computeUnreadCount(messageIds: ['a', 'b', 'c', 'd'], lastSeenMessageId: 'b', panelOpen: false), 2);
      expect(computeUnreadCount(messageIds: ['a', 'b'], lastSeenMessageId: 'b', panelOpen: false), 0);
    });

    test('counts everything when the last seen id is no longer in the window', () {
      expect(computeUnreadCount(messageIds: ['x', 'y'], lastSeenMessageId: 'gone', panelOpen: false), 2);
    });
  });

  group('ChatMessage.fromJson', () {
    test('parses every field and defaults the flags', () {
      final full = ChatMessage.fromJson({
        'id': '1', 'authorPlayerId': 'p', 'authorDisplayName': 'Bob', 'cityId': 'c', 'content': 'Hi',
        'createdAtUtc': '2026-10-17T10:00:00Z', 'isVisible': false, 'isRemovedForViewer': true, 'isOwnMessage': true,
      });
      expect((full.id, full.authorDisplayName, full.cityId, full.content), ('1', 'Bob', 'c', 'Hi'));
      expect((full.isVisible, full.isRemovedForViewer, full.isOwnMessage), (false, true, true));
      expect(full.createdAt, isNotNull);

      final minimal = ChatMessage.fromJson({'id': '2'});
      expect((minimal.isVisible, minimal.isRemovedForViewer, minimal.isOwnMessage, minimal.cityId), (true, false, false, null));
      expect(minimal.createdAt, isNull);
    });
  });

  group('ChatState', () {
    test('background refresh counts unread messages and does not mark them read', () async {
      final service = FakeChatService(messages: [chatMessage('1'), chatMessage('2'), chatMessage('3')]);
      final state = await _chatState(service, lastSeen: '1');

      expect(state.unreadCount, 2);
      expect(state.hasUnread, isTrue);
    });

    test('opening the panel clears the badge and persists the newest message id', () async {
      final storage = InMemoryChatReadStorage();
      final service = FakeChatService(messages: [chatMessage('1'), chatMessage('2')]);
      final auth = await _signedIn();
      final state = ChatState(service: service, storage: storage, pollingEnabled: false)..bindAuth(auth);
      addTearDown(state.dispose);
      await Future<void>.delayed(Duration.zero);
      expect(state.unreadCount, 2);

      await state.openPanel();

      expect(state.unreadCount, 0);
      expect(storage.value, '2');

      service.messages = [...service.messages, chatMessage('3')];
      await state.refresh(silent: true);
      expect(state.unreadCount, 0); // still open: everything is read
      expect(storage.value, '3');

      await state.closePanel();
      service.messages = [...service.messages, chatMessage('4'), chatMessage('5')];
      await state.refresh(silent: true);
      expect(state.unreadCount, 2);
    });

    test('a failed silent refresh keeps the current messages and shows no error', () async {
      final service = FakeChatService(messages: [chatMessage('1')]);
      final state = await _chatState(service);
      service.fetchError = Exception('offline');

      await state.refresh(silent: true);

      expect(state.messages, hasLength(1));
      expect(state.loadFailed, isFalse);
    });

    test('a failed visible refresh reports the error, a later success clears it', () async {
      final service = FakeChatService()..fetchError = Exception('offline');
      final state = await _chatState(service);

      await state.refresh();
      expect(state.loadFailed, isTrue);
      expect(state.loading, isFalse);

      service.fetchError = null;
      service.messages = [chatMessage('1')];
      await state.refresh();
      expect(state.loadFailed, isFalse);
      expect(state.messages, hasLength(1));
    });

    test('send validates, maps server codes and reloads on success', () async {
      final service = FakeChatService(messages: [chatMessage('1')]);
      final state = await _chatState(service);

      expect(await state.send('   '), ChatSendOutcome.empty);
      expect(await state.send('x' * (maxChatLength + 1)), ChatSendOutcome.tooLong);
      expect(service.sent, isEmpty); // nothing reached the server

      service.sendErrorCode = 'RATE_LIMITED';
      expect(await state.send('hi'), ChatSendOutcome.rateLimited);
      service.sendErrorCode = 'MESSAGE_TOO_LONG';
      expect(await state.send('hi'), ChatSendOutcome.tooLong);
      service.sendErrorCode = 'SOMETHING_ELSE';
      expect(await state.send('hi'), ChatSendOutcome.failed);
      service.sendErrorCode = null;
      service.sendThrowsUnexpected = true;
      expect(await state.send('hi'), ChatSendOutcome.failed);
      service.sendThrowsUnexpected = false;
      expect(state.sending, isFalse);

      expect(await state.send('  hello  '), ChatSendOutcome.sent);
      expect(service.sent.single.content, 'hello'); // trimmed
      expect(state.messages.last.content, 'hello'); // history reloaded
    });

    test('selecting a city channel loads that channel and sends into it', () async {
      final service = FakeChatService(messages: [chatMessage('g1')]);
      service.byCity['city-1'] = [chatMessage('c1', cityId: 'city-1', content: 'city hello')];
      final state = await _chatState(service);

      await state.selectChannel('city-1');
      expect(state.messages.single.content, 'city hello');
      expect(service.fetchedCityIds.last, 'city-1');

      await state.send('to the city');
      expect(service.sent.single.cityId, 'city-1');

      await state.selectChannel(null);
      expect(state.messages.single.id, 'g1');
    });

    test('signing out clears messages and the badge and stops fetching', () async {
      final service = FakeChatService(messages: [chatMessage('1')]);
      final auth = await _signedIn();
      final state = ChatState(service: service, storage: InMemoryChatReadStorage(), pollingEnabled: false)..bindAuth(auth);
      addTearDown(state.dispose);
      await Future<void>.delayed(Duration.zero);
      expect(state.unreadCount, 1);
      final fetchesBefore = service.fetchCount;

      await auth.logout();
      await Future<void>.delayed(Duration.zero);

      expect(state.messages, isEmpty);
      expect(state.unreadCount, 0);
      await state.refresh(silent: true);
      expect(service.fetchCount, fetchesBefore);
    });

    test('a signed-out player triggers no chat calls', () async {
      final service = FakeChatService(messages: [chatMessage('1')]);
      final state = await _chatState(service, signedIn: false);

      expect(service.fetchCount, 0);
      expect(state.unreadCount, 0);
    });

    test('polling refreshes in the background at the configured interval', () async {
      final service = FakeChatService(messages: [chatMessage('1')]);
      final auth = await _signedIn();
      final state = ChatState(
        service: service,
        storage: InMemoryChatReadStorage(),
        backgroundInterval: const Duration(milliseconds: 30),
      )..bindAuth(auth);
      addTearDown(state.dispose);

      await Future<void>.delayed(const Duration(milliseconds: 150));

      expect(service.fetchCount, greaterThan(2));
    });

    test('a request still in flight when the player signs out is ignored', () async {
      final service = FakeChatService(messages: [chatMessage('1')]);
      final auth = await _signedIn();
      final state = ChatState(service: service, storage: InMemoryChatReadStorage(), pollingEnabled: false)..bindAuth(auth);
      addTearDown(state.dispose);
      await Future<void>.delayed(Duration.zero);
      service.messages = [chatMessage('1'), chatMessage('2')];

      final pending = state.refresh(silent: true); // started while signed in...
      await auth.logout(); // ...and the player signs out before it completes
      await pending;

      expect(state.messages, isEmpty);
      expect(state.unreadCount, 0);
    });

    test('signing out forgets the read marker so the next account starts fresh', () async {
      final storage = InMemoryChatReadStorage('old-marker');
      final auth = await _signedIn();
      final state = ChatState(service: FakeChatService(messages: [chatMessage('1')]), storage: storage, pollingEnabled: false)
        ..bindAuth(auth);
      addTearDown(state.dispose);
      await Future<void>.delayed(Duration.zero);

      await auth.logout();
      await Future<void>.delayed(Duration.zero);

      expect(storage.value, isNull);
    });

    test('auth notifications that are not a session change do not trigger chat calls', () async {
      final service = FakeChatService(messages: [chatMessage('1')]);
      final auth = await _signedIn();
      final state = ChatState(service: service, storage: InMemoryChatReadStorage(), pollingEnabled: false)..bindAuth(auth);
      addTearDown(state.dispose);
      await Future<void>.delayed(Duration.zero);
      final before = service.fetchCount;

      auth.setIsAdmin(true); // notifies listeners but the player is still the same signed-in session
      await Future<void>.delayed(Duration.zero);

      expect(service.fetchCount, before);
    });

    test('the city channel never moves the global read marker or the badge', () async {
      final storage = InMemoryChatReadStorage('g1');
      final service = FakeChatService(messages: [chatMessage('g1'), chatMessage('g2'), chatMessage('g3')]);
      service.byCity['c'] = [chatMessage('c1', cityId: 'c'), chatMessage('c2', cityId: 'c')];
      final auth = await _signedIn();
      final state = ChatState(service: service, storage: storage, pollingEnabled: false)..bindAuth(auth);
      addTearDown(state.dispose);
      await Future<void>.delayed(Duration.zero);
      expect(state.unreadCount, 2);

      await state.openPanel();
      await state.selectChannel('c');
      expect(storage.value, 'g3'); // the city's ids did not overwrite the global marker
      await state.closePanel(); // last view closed: back to the global channel
      await Future<void>.delayed(Duration.zero);

      expect(state.channelCityId, isNull);
      service.messages = [...service.messages, chatMessage('g4')];
      await state.refresh(silent: true);
      expect(state.unreadCount, 1); // the badge tracks the global channel again
    });

    test('closing one of two visible chat views keeps the chat open', () async {
      final service = FakeChatService(messages: [chatMessage('1')]);
      final state = await _chatState(service);

      await state.openPanel(); // e.g. the dashboard Chat tab
      await state.openPanel(); // and the bottom sheet on top of it
      await state.closePanel();
      expect(state.panelOpen, isTrue);

      service.messages = [...service.messages, chatMessage('2')];
      await state.refresh(silent: true);
      expect(state.unreadCount, 0); // still reading: no badge

      await state.closePanel();
      expect(state.panelOpen, isFalse);
    });
  });
}
