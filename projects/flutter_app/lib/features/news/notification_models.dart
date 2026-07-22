// Data models for the personal notification inbox, mirroring
// `projects/frontend/src/views/NotificationsView.vue` — a separate data
// source from the game-wide news feed (`news_models.dart`). GraphQL field
// names verified against `projects/Api/Types/Query.Notifications.cs`.

class PlayerNotification {
  const PlayerNotification({
    required this.id,
    required this.type,
    required this.severity,
    required this.title,
    required this.message,
    required this.isRead,
    required this.createdAtUtc,
    required this.companyId,
    required this.buildingId,
    required this.bankAccountId,
    required this.loanId,
  });

  final String id;
  final String type;

  /// `INFO`, `WARNING`, or `CRITICAL`.
  final String severity;
  final String? title;
  final String? message;
  final bool isRead;
  final String createdAtUtc;
  final String? companyId;
  final String? buildingId;
  final String? bankAccountId;
  final String? loanId;

  factory PlayerNotification.fromJson(Map<String, dynamic> json) => PlayerNotification(
    id: json['id'] as String,
    type: (json['type'] as String?) ?? 'INFO',
    severity: (json['severity'] as String?) ?? 'INFO',
    title: json['title'] as String?,
    message: json['message'] as String?,
    isRead: json['isRead'] as bool? ?? false,
    createdAtUtc: (json['createdAtUtc'] as String?) ?? '',
    companyId: json['companyId'] as String?,
    buildingId: json['buildingId'] as String?,
    bankAccountId: json['bankAccountId'] as String?,
    loanId: json['loanId'] as String?,
  );

  /// Mirrors `handleNotificationClick`'s priority order in
  /// `NotificationsView.vue` — first match wins.
  String get navigationTarget {
    if (buildingId != null) return '/building/$buildingId';
    if (type == 'SHIPMENT_ARRIVED' || type == 'LOGISTICS_MARGIN_EROSION') return '/trade-routes';
    if (type == 'LOAN_REPAYMENT_DUE_SOON' || type == 'LOAN_PAYMENT_DUE' || type == 'LOAN_DEFAULT' || loanId != null) {
      return '/banking';
    }
    if (type == 'BANK_ACCOUNT_LOW_BALANCE' || bankAccountId != null) return '/bank-statement';
    if (type == 'TAKEOVER_ALERT' || companyId != null) return '/stocks';
    return '/dashboard';
  }
}

class NotificationInbox {
  const NotificationInbox({required this.unreadCount, required this.items});

  final int unreadCount;
  final List<PlayerNotification> items;
}
