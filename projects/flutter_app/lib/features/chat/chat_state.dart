import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../core/auth/auth_state.dart';
import 'chat_models.dart';
import 'chat_read_storage.dart';
import 'chat_service.dart';

/// App-wide chat state: the loaded history of the active channel, the unread count (for the drawer badge) and polling.
/// Mirrors `useChat.ts` + `stores/chat.ts` on the web: the server has no push channel for the web client either, so
/// new messages arrive by polling (fast while the chat is open, slow in the background to keep the badge fresh).
class ChatState extends ChangeNotifier {
  ChatState({
    required ChatService service,
    ChatReadStorage? storage,
    this.pollingEnabled = true,
    this.openInterval = const Duration(seconds: 3),
    this.backgroundInterval = const Duration(seconds: 20),
  }) : _service = service,
       _storage = storage ?? SharedPreferencesChatReadStorage();

  final ChatService _service;
  final ChatReadStorage _storage;

  /// Tests switch polling off so no timers stay pending; production keeps it on.
  final bool pollingEnabled;
  final Duration openInterval;
  final Duration backgroundInterval;

  List<ChatMessage> messages = const [];
  bool loading = false;
  bool sending = false;
  String? error;
  int unreadCount = 0;
  bool panelOpen = false;

  /// `null` is the global channel; a city id selects that city's channel.
  String? channelCityId;

  String? _lastSeenMessageId;
  AuthState? _auth;
  Timer? _timer;
  bool _disposed = false;
  int _loadGeneration = 0;

  bool get hasUnread => unreadCount > 0;

  /// Starts and stops background polling with the session: signed out means no calls and no badge.
  void bindAuth(AuthState auth) {
    _auth?.removeListener(_onAuthChanged);
    _auth = auth;
    auth.addListener(_onAuthChanged);
    _onAuthChanged();
  }

  void _onAuthChanged() {
    final authenticated = _auth?.isAuthenticated ?? false;
    if (authenticated) {
      unawaited(_activate());
    } else {
      _timer?.cancel();
      _timer = null;
      messages = const [];
      unreadCount = 0;
      error = null;
      _notify();
    }
  }

  Future<void> _activate() async {
    _lastSeenMessageId ??= await _storage.read();
    await refresh(silent: true);
    _restartTimer();
  }

  void _restartTimer() {
    _timer?.cancel();
    _timer = null;
    if (!pollingEnabled || !(_auth?.isAuthenticated ?? false)) return;
    _timer = Timer.periodic(panelOpen ? openInterval : backgroundInterval, (_) => unawaited(refresh(silent: true)));
  }

  /// The chat panel (or the dashboard's Chat tab) became visible: everything loaded counts as read.
  Future<void> openPanel() async {
    panelOpen = true;
    _restartTimer();
    await refresh(silent: messages.isNotEmpty);
    await _markAllRead();
  }

  Future<void> closePanel() async {
    panelOpen = false;
    _restartTimer();
    _notify();
  }

  Future<void> selectChannel(String? cityId) async {
    if (channelCityId == cityId) return;
    channelCityId = cityId;
    messages = const [];
    await refresh();
    if (panelOpen) await _markAllRead();
  }

  /// Loads the newest messages. [silent] keeps the current list on screen (background polling); a failed silent refresh never replaces it with an error.
  Future<void> refresh({bool silent = false}) async {
    if (!(_auth?.isAuthenticated ?? true)) return;
    final generation = ++_loadGeneration;
    if (!silent) {
      loading = true;
      error = null;
      _notify();
    }
    try {
      final fetched = await _service.fetchMessages(cityId: channelCityId);
      if (_disposed || generation != _loadGeneration) return; // a newer load (channel switch) won
      if (!_sameMessages(messages, fetched)) messages = fetched;
      error = null;
      if (panelOpen) {
        await _markAllRead();
      } else {
        _updateUnread();
      }
    } catch (e) {
      if (_disposed || generation != _loadGeneration) return;
      if (!silent) error = e.toString().replaceFirst('Exception: ', '');
    } finally {
      if (!_disposed && generation == _loadGeneration) {
        loading = false;
        _notify();
      }
    }
  }

  /// Sends [text] to the active channel and reloads the history on success.
  Future<ChatSendOutcome> send(String text) async {
    final content = text.trim();
    if (content.isEmpty) return ChatSendOutcome.empty;
    if (content.length > maxChatLength) return ChatSendOutcome.tooLong;
    sending = true;
    _notify();
    try {
      await _service.sendMessage(cityId: channelCityId, content: content);
    } on ChatSendException catch (e) {
      return switch (e.code) {
        'RATE_LIMITED' => ChatSendOutcome.rateLimited,
        'MESSAGE_TOO_LONG' => ChatSendOutcome.tooLong,
        _ => ChatSendOutcome.failed,
      };
    } catch (_) {
      return ChatSendOutcome.failed;
    } finally {
      sending = false;
      _notify();
    }
    await refresh(silent: true);
    return ChatSendOutcome.sent;
  }

  Future<void> _markAllRead() async {
    if (messages.isNotEmpty && _lastSeenMessageId != messages.last.id) {
      _lastSeenMessageId = messages.last.id;
      await _storage.write(_lastSeenMessageId!);
    }
    unreadCount = 0;
    _notify();
  }

  void _updateUnread() {
    unreadCount = computeUnreadCount(
      messageIds: messages.map((m) => m.id).toList(),
      lastSeenMessageId: _lastSeenMessageId,
      panelOpen: panelOpen,
    );
  }

  static bool _sameMessages(List<ChatMessage> a, List<ChatMessage> b) {
    if (a.length != b.length) return false;
    for (var i = 0; i < a.length; i++) {
      if (a[i].id != b[i].id || a[i].content != b[i].content || a[i].isRemovedForViewer != b[i].isRemovedForViewer) {
        return false;
      }
    }
    return true;
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _timer?.cancel();
    _auth?.removeListener(_onAuthChanged);
    super.dispose();
  }
}
