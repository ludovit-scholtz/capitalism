import 'dart:async';

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../features/chat/chat_panel.dart';
import '../auth/auth_state.dart';
import '../router/nav_items.dart';
import '../services/url_opener.dart';
import '../theme/app_icons.dart';
import '../theme/app_theme.dart';
import '../theme/cosmic_background.dart';
import 'icon_badge.dart';

/// Persistent chrome (app bar, drawer, bottom nav) wrapped around every
/// route via a go_router `ShellRoute`. Equivalent to `AppHeader.vue` +
/// `App.vue`'s layout in the web frontend. Also where the app-wide
/// [CosmicBackground] backdrop is applied — every screen gets it without
/// needing to opt in individually.
class AppShell extends StatelessWidget {
  const AppShell({super.key, required this.child, this.urlOpener = const ExternalUrlOpener()});

  final Widget child;

  /// Injectable so tests can substitute a fake instead of exercising the
  /// real url_launcher platform channel.
  final UrlOpener urlOpener;

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();

    return CosmicBackground(
      child: Scaffold(
        backgroundColor: Colors.transparent,
        appBar: AppBar(
          title: const Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              FaIcon(AppIcons.brandMark, color: AppTheme.neonCyan, size: 20),
              SizedBox(width: 10),
              Text('CAPITALISM'),
            ],
          ),
        ),
        drawer: Drawer(
          child: SafeArea(
            child: ListView(
              padding: EdgeInsets.zero,
              children: [
                DrawerHeader(
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      begin: Alignment.topLeft,
                      end: Alignment.bottomRight,
                      colors: [AppTheme.neonCyan.withValues(alpha: 0.18), Colors.transparent],
                    ),
                  ),
                  child: Row(
                    children: [
                      const IconBadge(icon: AppIcons.brandMark, color: AppTheme.neonCyan, size: 36, iconSize: 16),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          'CAPITALISM',
                          style: Theme.of(context).textTheme.titleMedium,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                  ),
                ),
                for (final section in navSections) ...[
                  if (_visibleItems(section, auth).isNotEmpty) ...[
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                      child: Text(
                        section.title.toUpperCase(),
                        style: Theme.of(
                          context,
                        ).textTheme.labelMedium?.copyWith(color: AppTheme.neonCyan, letterSpacing: 1.2),
                      ),
                    ),
                    for (final item in _visibleItems(section, auth))
                      ListTile(
                        leading: FaIcon(item.icon, size: 18),
                        title: Text(item.label),
                        onTap: () => _handleTap(context, item),
                      ),
                  ],
                ],
              ],
            ),
          ),
        ),
        body: child,
        bottomNavigationBar: NavigationBar(
          selectedIndex: _bottomIndexFor(GoRouterState.of(context).uri.toString()),
          destinations: const [
            NavigationDestination(icon: FaIcon(AppIcons.home, size: 20), label: 'Home'),
            NavigationDestination(icon: FaIcon(AppIcons.dashboard, size: 20), label: 'Dashboard'),
            NavigationDestination(icon: FaIcon(AppIcons.exchange, size: 20), label: 'Exchange'),
            NavigationDestination(icon: FaIcon(AppIcons.news, size: 20), label: 'News'),
          ],
          onDestinationSelected: (index) => _handleBottomTap(context, index),
        ),
      ),
    );
  }

  List<NavItem> _visibleItems(NavSection section, AuthState auth) {
    return section.items
        .where((item) => (!item.requiresAuth || auth.isAuthenticated) && (!item.requiresAdmin || auth.isAdmin))
        .toList();
  }

  void _handleTap(BuildContext context, NavItem item) {
    Navigator.of(context).pop();

    if (item.opensChatPanel) {
      unawaited(ChatPanel.show(context));
      return;
    }
    if (item.externalUrl != null) {
      unawaited(urlOpener.open(item.externalUrl!));
      return;
    }
    if (item.route.isEmpty) return;
    context.go(item.route);
  }

  void _handleBottomTap(BuildContext context, int index) {
    switch (index) {
      case 0:
        context.go('/');
      case 1:
        context.go('/dashboard');
      case 2:
        context.go('/exchange');
      case 3:
        context.go('/news');
    }
  }

  int _bottomIndexFor(String location) {
    if (location.startsWith('/dashboard')) return 1;
    if (location.startsWith('/exchange')) return 2;
    if (location.startsWith('/news')) return 3;
    return 0;
  }
}
