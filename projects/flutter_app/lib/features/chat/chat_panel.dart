import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';

import '../../core/theme/app_icons.dart';
import '../../core/widgets/icon_badge.dart';
import '../../l10n/generated/app_localizations.dart';
import 'chat_view.dart';

/// In-app panel opened from the drawer's Chat item, mirroring the chat side
/// panel opened by the "Chat" button in `AppHeader.vue` on the web (which is
/// likewise not a routed page). The conversation itself is [ChatView].
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
    final l10n = AppLocalizations.of(context);
    final height = MediaQuery.sizeOf(context).height * 0.8;
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(24, 24, 24, 24 + MediaQuery.viewInsetsOf(context).bottom),
        child: SizedBox(
          height: height,
          child: Column(
            children: [
              Row(
                children: [
                  const IconBadge(icon: AppIcons.chat, size: 36, iconSize: 16),
                  const SizedBox(width: 10),
                  Text(l10n.chatTitle, style: theme.textTheme.titleLarge),
                  const Spacer(),
                  IconButton(
                    icon: const FaIcon(AppIcons.close, size: 18),
                    tooltip: l10n.chatClose,
                    onPressed: () => Navigator.of(context).pop(),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              const Expanded(child: ChatView()),
            ],
          ),
        ),
      ),
    );
  }
}
