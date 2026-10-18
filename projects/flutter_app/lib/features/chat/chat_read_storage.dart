import 'package:shared_preferences/shared_preferences.dart';

/// Where [ChatState] persists the id of the last message the player has seen
/// (the web keeps it in `localStorage` under `chat_last_seen_message_id`).
abstract class ChatReadStorage {
  Future<String?> read();
  Future<void> write(String messageId);
  Future<void> clear();
}

class SharedPreferencesChatReadStorage implements ChatReadStorage {
  static const _key = 'chat_last_seen_message_id';

  @override
  Future<String?> read() async => (await SharedPreferences.getInstance()).getString(_key);

  @override
  Future<void> write(String messageId) async => (await SharedPreferences.getInstance()).setString(_key, messageId);

  @override
  Future<void> clear() async => (await SharedPreferences.getInstance()).remove(_key);
}
