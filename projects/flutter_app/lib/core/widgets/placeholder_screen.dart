import 'package:flutter/material.dart';

import '../theme/app_icons.dart';
import 'icon_badge.dart';

/// Shared body for every not-yet-implemented screen in this scaffold.
/// [sourceView] names the Vue view in `projects/frontend/src/views/` this
/// screen mirrors, so it's easy to find the reference implementation.
///
/// Renders bare content, not its own `Scaffold`/`AppBar` — every screen is
/// shown as the `body` of `AppShell`'s outer `Scaffold` (via the go_router
/// `ShellRoute`), so wrapping here would stack a second app bar underneath.
class PlaceholderScreen extends StatelessWidget {
  const PlaceholderScreen({
    super.key,
    required this.title,
    required this.sourceView,
  });

  final String title;
  final String sourceView;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const IconBadge(icon: AppIcons.construction, size: 64, iconSize: 28),
            const SizedBox(height: 16),
            Text(title, style: theme.textTheme.headlineSmall, textAlign: TextAlign.center),
            const SizedBox(height: 8),
            Text(
              'Not implemented yet. Mirrors $sourceView in the web frontend.',
              style: theme.textTheme.bodyMedium,
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}
