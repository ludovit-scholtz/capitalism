import 'package:capitalism_app/features/news/notification_models.dart';
import 'package:capitalism_app/features/news/notifications_service.dart';

class FakeNotificationsService implements NotificationsService {
  FakeNotificationsService({this.inbox = const NotificationInbox(unreadCount: 0, items: []), this.fetchError});

  final NotificationInbox inbox;
  final Object? fetchError;

  final List<String> calls = [];
  List<String> markedReadIds = [];
  int markAllReadCallCount = 0;

  @override
  Future<NotificationInbox> fetchInbox({int limit = 50}) async {
    calls.add('fetchInbox');
    if (fetchError != null) throw fetchError!;
    return inbox;
  }

  @override
  Future<void> markRead(List<String> ids) async {
    calls.add('markRead');
    markedReadIds = [...markedReadIds, ...ids];
  }

  @override
  Future<void> markAllRead() async {
    calls.add('markAllRead');
    markAllReadCallCount++;
  }
}
