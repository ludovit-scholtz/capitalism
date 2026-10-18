import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/generated/app_localizations.dart';
import 'chat_state.dart';

/// Unread-message pill for the drawer's Chat item (the web shows the same count on its header Chat button).
class ChatUnreadBadge extends StatelessWidget {
  const ChatUnreadBadge({super.key});

  @override
  Widget build(BuildContext context) {
    final count = context.watch<ChatState>().unreadCount;
    if (count <= 0) return const SizedBox.shrink();
    final theme = Theme.of(context);
    return Semantics(
      label: AppLocalizations.of(context).chatUnreadBadge(count),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
        decoration: BoxDecoration(color: theme.colorScheme.error, borderRadius: BorderRadius.circular(999)),
        child: Text(
          count > 99 ? '99+' : '$count',
          style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onError, fontWeight: FontWeight.bold),
        ),
      ),
    );
  }
}
