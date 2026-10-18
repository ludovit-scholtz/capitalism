// Ported from `projects/frontend/src/composables/useChat.ts`, `stores/chat.ts` and the `InGameChatMessage` type.

/// Maximum character length of one chat message (the server enforces the same limit).
const int maxChatLength = 500;

/// Character count from which the composer shows a counter.
const int chatCounterThreshold = 450;

/// Messages requested per history load, matching the web's `lastN`.
const int chatHistorySize = 100;

class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.authorPlayerId,
    required this.authorDisplayName,
    required this.cityId,
    required this.content,
    required this.createdAtUtc,
    required this.isVisible,
    required this.isRemovedForViewer,
    required this.isOwnMessage,
  });

  factory ChatMessage.fromJson(Map<String, dynamic> json) => ChatMessage(
    id: json['id'] as String,
    authorPlayerId: json['authorPlayerId'] as String? ?? '',
    authorDisplayName: json['authorDisplayName'] as String? ?? '',
    cityId: json['cityId'] as String?,
    content: json['content'] as String? ?? '',
    createdAtUtc: json['createdAtUtc'] as String? ?? '',
    isVisible: json['isVisible'] as bool? ?? true,
    isRemovedForViewer: json['isRemovedForViewer'] as bool? ?? false,
    isOwnMessage: json['isOwnMessage'] as bool? ?? false,
  );

  final String id;
  final String authorPlayerId;
  final String authorDisplayName;

  /// `null` for the global channel, the city id for a city channel.
  final String? cityId;
  final String content;
  final String createdAtUtc;

  /// `false` when an administrator hid the author from chat: the author still sees their own message, everybody else sees a removed placeholder.
  final bool isVisible;

  /// `true` when the server removed the content for the current viewer (the author is invisible in chat).
  final bool isRemovedForViewer;
  final bool isOwnMessage;

  DateTime? get createdAt => DateTime.tryParse(createdAtUtc)?.toLocal();
}

/// Outcome of a send attempt, mapped from the server's error codes.
enum ChatSendOutcome { sent, empty, tooLong, rateLimited, failed }

/// Raised by [ChatService.sendMessage] with the server's machine-readable code (`RATE_LIMITED`, `MESSAGE_TOO_LONG`, ...).
class ChatSendException implements Exception {
  const ChatSendException(this.message, [this.code]);

  final String message;
  final String? code;

  @override
  String toString() => 'ChatSendException: $message';
}

/// Number of messages the viewer has not seen yet, mirroring `updateUnread` in the web's chat store:
/// everything counts as read while the panel is open; without a known last-seen id every message is unread;
/// an unknown last-seen id (cleared or older than the history window) also counts everything.
int computeUnreadCount({
  required List<String> messageIds,
  required String? lastSeenMessageId,
  required bool panelOpen,
}) {
  if (panelOpen) return 0;
  if (lastSeenMessageId == null) return messageIds.length;
  final index = messageIds.indexOf(lastSeenMessageId);
  if (index == -1) return messageIds.length;
  return messageIds.length - (index + 1);
}
