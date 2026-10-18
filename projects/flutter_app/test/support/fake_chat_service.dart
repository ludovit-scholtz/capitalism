import 'package:capitalism_app/features/chat/chat_models.dart';
import 'package:capitalism_app/features/chat/chat_read_storage.dart';
import 'package:capitalism_app/features/chat/chat_service.dart';

/// In-memory [ChatService] for tests: serves [messages], records what was sent and can fail on demand.
class FakeChatService implements ChatService {
  FakeChatService({List<ChatMessage>? messages}) : messages = messages ?? [];

  /// What `fetchMessages` returns (shared by all channels unless [byCity] has an entry).
  List<ChatMessage> messages;
  final Map<String, List<ChatMessage>> byCity = {};

  Object? fetchError;

  /// Error code the next sends fail with (`RATE_LIMITED`, `MESSAGE_TOO_LONG`, ...); `null` succeeds.
  String? sendErrorCode;
  bool sendThrowsUnexpected = false;

  int fetchCount = 0;
  final List<String?> fetchedCityIds = [];
  final List<({String? cityId, String content})> sent = [];

  /// When a send succeeds, append it to the history like the server would.
  bool appendSentToHistory = true;

  @override
  Future<List<ChatMessage>> fetchMessages({String? cityId, int lastN = chatHistorySize}) async {
    fetchCount++;
    fetchedCityIds.add(cityId);
    if (fetchError != null) throw fetchError!;
    return List.of(cityId != null ? (byCity[cityId] ?? const []) : messages);
  }

  @override
  Future<void> sendMessage({String? cityId, required String content}) async {
    if (sendThrowsUnexpected) throw StateError('boom');
    if (sendErrorCode != null) throw ChatSendException('send failed', sendErrorCode);
    sent.add((cityId: cityId, content: content));
    if (appendSentToHistory) {
      final message = chatMessage('sent-${sent.length}', content: content, own: true, cityId: cityId);
      if (cityId != null) {
        byCity.putIfAbsent(cityId, () => []).add(message);
      } else {
        messages = [...messages, message];
      }
    }
  }
}

ChatMessage chatMessage(
  String id, {
  String author = 'Alice',
  String content = 'Hello',
  bool own = false,
  bool visible = true,
  bool removed = false,
  String? cityId,
  String createdAtUtc = '2026-10-17T10:00:00Z',
}) => ChatMessage(
  id: id,
  authorPlayerId: 'player-$author',
  authorDisplayName: author,
  cityId: cityId,
  content: content,
  createdAtUtc: createdAtUtc,
  isVisible: visible,
  isRemovedForViewer: removed,
  isOwnMessage: own,
);

class InMemoryChatReadStorage implements ChatReadStorage {
  InMemoryChatReadStorage([this.value]);

  String? value;
  int writes = 0;

  @override
  Future<String?> read() async => value;

  @override
  Future<void> write(String messageId) async {
    writes++;
    value = messageId;
  }

  @override
  Future<void> clear() async => value = null;
}
