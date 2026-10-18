import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../core/auth/auth_state.dart';
import 'chat_models.dart';
import 'chat_read_storage.dart';
import 'chat_service.dart';

/// App-wide chat state: the loaded history of the active channel, the unread count (for the drawer badge) and polling.
/// Mirrors `useChat.ts` + `stores/chat.ts` on the web: the server has no push channel for the web client either, so
/// new messages arrive by polling (fast while a chat view is visible, slow in the background to keep the badge fresh).
///
/// The unread count and the read marker always describe the global channel: the city channel is only browsed while a
/// chat view is visible, and the channel resets to global when the last view closes.
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

  /// `true` when the last visible (non-background) load failed and there is nothing to show yet.
  bool loadFailed = false;
  int unreadCount = 0;

  /// `null` is the global channel; a city id selects that city's channel.
  String? channelCityId;

  String? _lastSeenMessageId;
  AuthState? _auth;
  bool _wasAuthenticated = false;
  Timer? _timer;
  bool _disposed = false;
  int _loadGeneration = 0;

  /// Number of chat views currently visible (the bottom sheet and the dashboard tab can both exist).
  int _visibleViews = 0;

  bool get panelOpen => _visibleViews > 0;
  bool get hasUnread => unreadCount > 0;

  /// Starts and stops background polling with the session: signed out means no calls and no badge.
  void bindAuth(AuthState auth) {
    _auth?.removeListener(_onAuthChanged);
    _auth = auth;
    _wasAuthenticated = false;
    auth.addListener(_onAuthChanged);
    _onAuthChanged();
  }

  void _onAuthChanged() {
    final authenticated = _auth?.isAuthenticated ?? false;
    if (authenticated == _wasAuthenticated) return; // other auth notifications (token refresh, profile) are not a session change
    _wasAuthenticated = authenticated;
    if (authenticated) {
      unawaited(_activate());
      return;
    }
    // Signed out: drop everything, ignore any request still in flight and forget the read marker (the next account on this device starts fresh).
    _loadGeneration++;
    _timer?.cancel();
    _timer = null;
    messages = const [];
    unreadCount = 0;
    loading = false;
    loadFailed = false;
    channelCityId = null;
    _lastSeenMessageId = null;
    unawaited(_storage.clear());
    _notify();
  }

  Future<void> _activate() async {
    _lastSeenMessageId ??= await _storage.read();
    if (_disposed || !_wasAuthenticated) return;
    await refresh(silent: true);
    _restartTimer();
  }

  void _restartTimer() {
    _timer?.cancel();
    _timer = null;
    if (!pollingEnabled || !_wasAuthenticatedOrUnbound) return;
    _timer = Timer.periodic(panelOpen ? openInterval : backgroundInterval, (_) => unawaited(refresh(silent: true)));
  }

  bool get _wasAuthenticatedOrUnbound => _auth == null || _wasAuthenticated;

  /// A chat view became visible: everything loaded counts as read and polling speeds up.
  Future<void> openPanel() async {
    _visibleViews++;
    _restartTimer();
    await refresh(silent: messages.isNotEmpty);
    await _markAllRead();
  }

  /// A chat view was hidden. When the last one closes the channel resets to global so the badge keeps tracking it.
  Future<void> closePanel() async {
    if (_visibleViews > 0) _visibleViews--;
    if (!panelOpen) {
      if (channelCityId != null) {
        channelCityId = null;
        messages = const [];
        unawaited(refresh(silent: true));
      }
    }
    _restartTimer();
    _notify();
  }

  Future<void> selectChannel(String? cityId) async {
    if (channelCityId == cityId) return;
    channelCityId = cityId;
    messages = const [];
    await refresh();
    await _markAllRead();
  }

  /// Loads the newest messages. [silent] keeps the current list on screen (background polling); a failed silent refresh never replaces it with an error.
  Future<void> refresh({bool silent = false}) async {
    if (!_wasAuthenticatedOrUnbound) return;
    final generation = ++_loadGeneration;
    if (!silent) {
      loading = true;
      loadFailed = false;
      _notify();
    }
    var fetchedOk = false;
    try {
      final fetched = await _service.fetchMessages(cityId: channelCityId);
      if (_disposed || generation != _loadGeneration) return; // a newer load (channel switch, sign-out) won
      if (!_sameMessages(messages, fetched)) messages = fetched;
      loadFailed = false;
      fetchedOk = true;
    } catch (_) {
      if (_disposed || generation != _loadGeneration) return;
      if (!silent) loadFailed = true;
    } finally {
      if (!_disposed && generation == _loadGeneration) {
        loading = false;
        _notify();
      }
    }
    // Bookkeeping stays outside the fetch's try block: a storage failure must not look like a failed load.
    if (fetchedOk && generation == _loadGeneration && !_disposed) {
      if (panelOpen) {
        await _markAllRead();
      } else {
        _updateUnread();
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

  /// Marks the loaded global history as read. City channel messages never move the global read marker.
  Future<void> _markAllRead() async {
    if (channelCityId == null && messages.isNotEmpty && _lastSeenMessageId != messages.last.id) {
      _lastSeenMessageId = messages.last.id;
      try {
        await _storage.write(_lastSeenMessageId!);
      } catch (_) {
        // The marker is a convenience; failing to persist it must not break the chat.
      }
    }
    unreadCount = 0;
    _notify();
  }

  void _updateUnread() {
    if (channelCityId != null) return;
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
