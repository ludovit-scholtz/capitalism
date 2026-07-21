import 'package:flutter/material.dart';

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
                Icon(Icons.chat_bubble_outline, color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Text('Chat', style: theme.textTheme.titleLarge),
                const Spacer(),
                IconButton(
                  icon: const Icon(Icons.close),
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
