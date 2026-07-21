import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../auth/auth_state.dart';
import '../router/nav_items.dart';

/// Persistent chrome (app bar, drawer, bottom nav) wrapped around every
/// route via a go_router `ShellRoute`. Equivalent to `AppHeader.vue` +
/// `App.vue`'s layout in the web frontend.
class AppShell extends StatelessWidget {
  const AppShell({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();

    return Scaffold(
      appBar: AppBar(title: const Text('Capitalism')),
      drawer: Drawer(
        child: SafeArea(
          child: ListView(
            padding: EdgeInsets.zero,
            children: [
              const DrawerHeader(child: Center(child: Text('Capitalism'))),
              for (final section in navSections) ...[
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                  child: Text(section.title, style: Theme.of(context).textTheme.labelLarge),
                ),
                for (final item in section.items)
                  if ((!item.requiresAuth || auth.isAuthenticated) && (!item.requiresAdmin || auth.isAdmin))
                    ListTile(
                      leading: Icon(item.icon),
                      title: Text(item.label),
                      onTap: () => _handleTap(context, item),
                    ),
              ],
            ],
          ),
        ),
      ),
      body: child,
      bottomNavigationBar: NavigationBar(
        selectedIndex: _bottomIndexFor(GoRouterState.of(context).uri.toString()),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.home_outlined), label: 'Home'),
          NavigationDestination(icon: Icon(Icons.dashboard_outlined), label: 'Dashboard'),
          NavigationDestination(icon: Icon(Icons.swap_horiz), label: 'Exchange'),
          NavigationDestination(icon: Icon(Icons.article_outlined), label: 'News'),
        ],
        onDestinationSelected: (index) => _handleBottomTap(context, index),
      ),
    );
  }

  void _handleTap(BuildContext context, NavItem item) {
    Navigator.of(context).pop();
    if (item.externalUrl != null || item.route.isEmpty) {
      // External links (Discord) and panel-only items (Chat) have no route
      // yet — wire up url_launcher / a side panel when those are built.
      return;
    }
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
