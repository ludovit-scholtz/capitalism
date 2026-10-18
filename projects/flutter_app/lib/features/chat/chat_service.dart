import '../../core/graphql/graphql_service.dart';
import 'chat_models.dart';

const _chatMessagesQuery = r'''
  query ChatMessages($cityId: UUID, $lastN: Int!) {
    chatMessages(cityId: $cityId, lastN: $lastN) {
      id authorPlayerId authorDisplayName cityId content createdAtUtc
      isVisible isRemovedForViewer isOwnMessage
    }
  }
''';

const _sendChatMessageMutation = r'''
  mutation SendChatMessage($input: SendChatMessageInput!) {
    sendChatMessage(input: $input) {
      id
    }
  }
''';

/// GraphQL calls for in-game chat, matching `projects/frontend/src/composables/useChat.ts`'s exact operations.
/// Discord-bridged messages arrive through the same `chatMessages` query, so no extra call is needed.
class ChatService {
  const ChatService(this._graphQlService);

  final GraphQlService _graphQlService;

  /// Newest [lastN] messages of the global channel (`cityId == null`) or of one city, oldest first as the server returns them.
  Future<List<ChatMessage>> fetchMessages({String? cityId, int lastN = chatHistorySize}) async {
    final data = await _graphQlService.request(_chatMessagesQuery, variables: {'cityId': cityId, 'lastN': lastN});
    final raw = data['chatMessages'] as List<dynamic>? ?? const [];
    return raw.map((item) => ChatMessage.fromJson(item as Map<String, dynamic>)).toList();
  }

  /// Sends [content]; throws [ChatSendException] carrying the server's code on failure.
  Future<void> sendMessage({String? cityId, required String content}) async {
    try {
      await _graphQlService.request(
        _sendChatMessageMutation,
        variables: {
          'input': {'cityId': cityId, 'content': content},
        },
      );
    } on GraphQlException catch (e) {
      throw ChatSendException(e.message, e.code);
    }
  }
}
