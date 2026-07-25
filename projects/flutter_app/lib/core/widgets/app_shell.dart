import 'dart:async';

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../features/chat/chat_panel.dart';
import '../auth/auth_state.dart';
import '../context/account_context_service.dart';
import '../context/recent_building_state.dart';
import '../game_state/game_state_service.dart';
import '../router/nav_items.dart';
import '../services/url_opener.dart';
import '../theme/app_icons.dart';
import '../theme/app_theme.dart';
import '../theme/cosmic_background.dart';
import 'context_switcher.dart';
import 'game_status_bar.dart';
import 'icon_badge.dart';

/// Persistent chrome (app bar, drawer, bottom nav) wrapped around every
/// route via a go_router `ShellRoute`. Equivalent to `AppHeader.vue` +
/// `App.vue`'s layout in the web frontend. Also where the app-wide
/// [CosmicBackground] backdrop is applied — every screen gets it without
/// needing to opt in individually.
class AppShell extends StatelessWidget {
  const AppShell({
    super.key,
    required this.child,
    this.urlOpener = const ExternalUrlOpener(),
    this.accountContextService,
    this.gameStateService,
  });

  final Widget child;

  /// Injectable so tests can substitute a fake instead of exercising the
  /// real url_launcher platform channel.
  final UrlOpener urlOpener;

  /// Injectable so tests can fake [ContextSwitcher]'s GraphQL calls instead
  /// of hitting a real backend, same pattern as [urlOpener].
  final AccountContextService? accountContextService;

  /// Injectable so tests can fake [GameStatusBar]'s GraphQL calls, same
  /// pattern as [accountContextService].
  final GameStateService? gameStateService;

  /// Below this width the [ContextSwitcher] moves out of the app bar and
  /// into the drawer, leaving only the balance/tick [GameStatusBar] in the
  /// bar itself. Matches the web's `lg` (1024px) breakpoint
  /// (`AppHeader.vue`), the point at which it collapses its full nav into a
  /// hamburger menu.
  static const double _wideScreenBreakpoint = 1024;

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();

    // Read available width from the incoming layout constraints rather than
    // `MediaQuery.sizeOf` — in `flutter test`, `WidgetTester.binding.
    // setSurfaceSize` (this app's established viewport-sizing convention;
    // see `test/support/app_harness.dart`) only changes the constraints
    // reaching the render tree, not `MediaQuery`'s underlying `FlutterView`,
    // so a MediaQuery-based breakpoint would silently never see anything but
    // the default 800x600 test viewport.
    return LayoutBuilder(
      builder: (context, constraints) => _buildScaffold(context, auth, constraints.maxWidth >= _wideScreenBreakpoint),
    );
  }

  Widget _buildScaffold(BuildContext context, AuthState auth, bool isWideScreen) {
    return CosmicBackground(
      child: Scaffold(
        backgroundColor: Colors.transparent,
        appBar: AppBar(
          title: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const FaIcon(AppIcons.brandMark, color: AppTheme.neonCyan, size: 20),
              const SizedBox(width: 10),
              if (auth.isAuthenticated && isWideScreen)
                Flexible(child: ContextSwitcher(accountContextService: accountContextService))
              else if (!auth.isAuthenticated)
                const Text('CAPITALISM'),
            ],
          ),
          actions: [
            if (auth.isAuthenticated)
              GameStatusBar(gameStateService: gameStateService, accountContextService: accountContextService),
            const SizedBox(width: 12),
          ],
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
                // On narrow screens the app bar has no room for the context
                // switcher (it's occupied by the balance/tick status chips),
                // so it moves here instead — still the same control/widget,
                // just relocated.
                if (auth.isAuthenticated && !isWideScreen)
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                    child: Align(
                      alignment: Alignment.centerLeft,
                      child: ContextSwitcher(accountContextService: accountContextService),
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
            NavigationDestination(icon: FaIcon(AppIcons.factory, size: 20), label: 'Last Building'),
            NavigationDestination(icon: FaIcon(AppIcons.bankStatement, size: 20), label: 'Bank Statement'),
            NavigationDestination(icon: FaIcon(AppIcons.forex, size: 20), label: 'Forex'),
            NavigationDestination(icon: FaIcon(AppIcons.stocks, size: 20), label: 'Stocks'),
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
        final lastBuildingId = context.read<RecentBuildingState>().lastBuildingId;
        context.go(lastBuildingId != null ? '/building/$lastBuildingId' : '/buildings/market');
      case 3:
        context.go('/bank-statement');
      case 4:
        context.go('/forex');
      case 5:
        context.go('/stocks');
      case 6:
        context.go('/news');
    }
  }

  int _bottomIndexFor(String location) {
    if (location.startsWith('/dashboard')) return 1;
    if (location.startsWith('/building')) return 2;
    if (location.startsWith('/bank-statement')) return 3;
    if (location.startsWith('/forex')) return 4;
    if (location.startsWith('/stocks')) return 5;
    if (location.startsWith('/news')) return 6;
    return 0;
  }
}
