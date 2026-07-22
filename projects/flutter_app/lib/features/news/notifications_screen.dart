// Ported from `projects/frontend/src/views/NotificationsView.vue`.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'notification_models.dart';
import 'notifications_service.dart';

IconData _iconFor(String type) {
  switch (type) {
    case 'SHIPMENT_ARRIVED':
      return Icons.check_circle_outline;
    case 'LOGISTICS_MARGIN_EROSION':
      return Icons.warning_amber_outlined;
    default:
      return Icons.notifications_outlined;
  }
}

Color _colorFor(BuildContext context, String severity) {
  final scheme = Theme.of(context).colorScheme;
  switch (severity) {
    case 'CRITICAL':
      return scheme.error;
    case 'WARNING':
      return scheme.tertiary;
    default:
      return scheme.primary;
  }
}

String _dayKey(String createdAtUtc) {
  final parsed = DateTime.tryParse(createdAtUtc);
  if (parsed == null) return 'Unknown date';
  final local = parsed.toLocal();
  return '${local.year}-${local.month.toString().padLeft(2, '0')}-${local.day.toString().padLeft(2, '0')}';
}

class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key, GraphQlService? graphQlService, NotificationsService? notificationsService})
    : _injectedGraphQlService = graphQlService,
      _injectedNotificationsService = notificationsService;

  final GraphQlService? _injectedGraphQlService;
  final NotificationsService? _injectedNotificationsService;

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  late final NotificationsService _service;

  bool _loading = true;
  String? _error;
  NotificationInbox? _inbox;
  bool _markingAllRead = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedNotificationsService ?? NotificationsService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final inbox = await _service.fetchInbox();
      if (!mounted) return;
      setState(() {
        _inbox = inbox;
        _loading = false;
      });

      final unreadIds = inbox.items.where((n) => !n.isRead).map((n) => n.id).toList();
      if (unreadIds.isNotEmpty) {
        // Fire-and-forget, mirroring the web's auto-mark-on-visit.
        unawaited(_service.markRead(unreadIds));
      }
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load notifications. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _markAllRead() async {
    setState(() => _markingAllRead = true);
    try {
      await _service.markAllRead();
      await _load();
    } finally {
      if (mounted) setState(() => _markingAllRead = false);
    }
  }

  Future<void> _handleTap(PlayerNotification notification) async {
    if (!notification.isRead) {
      unawaited(_service.markRead([notification.id]));
    }
    if (mounted) context.go(notification.navigationTarget);
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _load, child: const Text('Try again')),
            ],
          ),
        ),
      );
    }

    final items = _inbox?.items ?? const [];
    if (items.isEmpty) {
      return const Center(child: Text('No notifications yet.'));
    }

    final grouped = <String, List<PlayerNotification>>{};
    for (final item in items) {
      grouped.putIfAbsent(_dayKey(item.createdAtUtc), () => []).add(item);
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Row(
            children: [
              Expanded(child: Text('Notifications', style: Theme.of(context).textTheme.headlineSmall)),
              OutlinedButton(
                onPressed: items.isEmpty || _markingAllRead ? null : _markAllRead,
                child: const Text('Mark all read'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          for (final day in grouped.keys) ...[
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(day, style: Theme.of(context).textTheme.labelLarge),
            ),
            for (final notification in grouped[day]!) _NotificationTile(notification: notification, onTap: () => _handleTap(notification)),
            const SizedBox(height: 12),
          ],
        ],
      ),
    );
  }
}

class _NotificationTile extends StatelessWidget {
  const _NotificationTile({required this.notification, required this.onTap});

  final PlayerNotification notification;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      color: notification.isRead ? null : Theme.of(context).colorScheme.surfaceContainerHigh,
      child: ListTile(
        key: ValueKey('notification-${notification.id}'),
        leading: Icon(_iconFor(notification.type), color: _colorFor(context, notification.severity)),
        title: Text(notification.title ?? notification.type),
        subtitle: notification.message != null ? Text(notification.message!) : null,
        onTap: onTap,
      ),
    );
  }
}
