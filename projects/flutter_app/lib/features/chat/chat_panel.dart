import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';

import '../../core/theme/app_icons.dart';
import '../../core/widgets/icon_badge.dart';

/// In-app panel opened from the drawer's Chat item, mirroring the chat side
/// panel opened by the "Chat" button in `AppHeader.vue` on the web (which is
/// likewise not a routed page). Placeholder content only — see ROADMAP.md.
class ChatPanel extends StatelessWidget {
  const ChatPanel({super.key});

  static Future<void> show(BuildContext context) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (context) => const ChatPanel(),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const IconBadge(icon: AppIcons.chat, size: 36, iconSize: 16),
                const SizedBox(width: 10),
                Text('Chat', style: theme.textTheme.titleLarge),
                const Spacer(),
                IconButton(
                  icon: const FaIcon(AppIcons.close, size: 18),
                  tooltip: 'Close',
                  onPressed: () => Navigator.of(context).pop(),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Text(
              'Not implemented yet. Mirrors the chat side panel in AppHeader.vue.',
              style: theme.textTheme.bodyMedium,
            ),
          ],
        ),
      ),
    );
  }
}
