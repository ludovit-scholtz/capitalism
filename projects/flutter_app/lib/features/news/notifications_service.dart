import '../../core/graphql/graphql_service.dart';
import 'notification_models.dart';

const _myNotificationsQuery = r'''
  query MyNotifications($first: Int!, $onlyUnread: Boolean!) {
    myNotifications(first: $first, onlyUnread: $onlyUnread) {
      edges {
        node {
          id type severity title message isRead createdAtUtc
          companyId buildingId bankAccountId loanId
        }
      }
    }
  }
''';

const _notificationCountQuery = r'''
  query NotificationCount {
    notificationCount
  }
''';

const _markReadMutation = r'''
  mutation MarkNotificationsRead($ids: [UUID!]!) {
    markNotificationsRead(ids: $ids)
  }
''';

const _markAllReadMutation = r'''
  mutation MarkAllNotificationsRead {
    markAllNotificationsRead
  }
''';

/// GraphQL calls for the personal notification inbox, matching
/// `projects/frontend/src/stores/notifications.ts`'s exact two-query +
/// two-mutation contract (not the alternative single combined
/// `playerNotificationInbox` query the backend also exposes but the web
/// doesn't use).
class NotificationsService {
  const NotificationsService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<NotificationInbox> fetchInbox({int limit = 50}) async {
    final results = await Future.wait([
      _graphQlService.request(_myNotificationsQuery, variables: {'first': limit, 'onlyUnread': false}),
      _graphQlService.request(_notificationCountQuery),
    ]);

    final edges = (results[0]['myNotifications'] as Map<String, dynamic>)['edges'] as List<dynamic>? ?? const [];
    final items = edges
        .map((e) => PlayerNotification.fromJson((e as Map<String, dynamic>)['node'] as Map<String, dynamic>))
        .toList();
    final unreadCount = (results[1]['notificationCount'] as num?)?.toInt() ?? 0;

    return NotificationInbox(unreadCount: unreadCount, items: items);
  }

  Future<void> markRead(List<String> ids) async {
    if (ids.isEmpty) return;
    await _graphQlService.request(_markReadMutation, variables: {'ids': ids});
  }

  Future<void> markAllRead() async {
    await _graphQlService.request(_markAllReadMutation);
  }
}
