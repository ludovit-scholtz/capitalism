import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../features/chat/chat_panel.dart';
import '../../features/chat/chat_unread_badge.dart';
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
                        trailing: item.opensChatPanel ? const ChatUnreadBadge() : null,
                        onTap: () => _handleTap(context, item),
                      ),
                  ],
                ],
              ],
            ),
          ),
        ),
        body: Row(
          children: [
            if (isWideScreen) ...[
              Builder(
                builder: (ctx) => LayoutBuilder(
                  builder: (_, constraints) => SingleChildScrollView(
                    child: ConstrainedBox(
                      constraints: BoxConstraints(minHeight: constraints.maxHeight),
                      child: IntrinsicHeight(
                        child: NavigationRail(
                          selectedIndex: _destinationIndexFor(GoRouterState.of(ctx).uri.path),
                          labelType: NavigationRailLabelType.all,
                          backgroundColor: Colors.transparent,
                          destinations: [
                            for (final d in _destinations)
                              NavigationRailDestination(icon: FaIcon(d.icon, size: 20), label: Text(d.label)),
                          ],
                          onDestinationSelected: (index) => _handleBottomTap(ctx, index),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
              const VerticalDivider(width: 1),
            ],
            // Keyed so the active screen keeps its state when a resize or
            // rotation crosses the wide-screen breakpoint.
            Expanded(key: const ValueKey('shell-body'), child: child),
          ],
        ),
        bottomNavigationBar: isWideScreen
            ? null
            : Builder(
                builder: (ctx) => NavigationBar(
                  selectedIndex: _destinationIndexFor(GoRouterState.of(ctx).uri.path),
                  destinations: [
                    for (final d in _destinations)
                      NavigationDestination(icon: FaIcon(d.icon, size: 20), label: d.label),
                  ],
                  onDestinationSelected: (index) => _handleBottomTap(ctx, index),
                ),
              ),
      ),
    );
  }

  /// The five primary destinations (Material 3 allows 3-5). Everything else
  /// lives behind "More", which opens the drawer. Each destination owns its
  /// navigation so reordering the list can't desync labels from routes.
  static final _destinations = <_ShellDestination>[
    _ShellDestination('Home', AppIcons.home, (ctx) => ctx.go('/')),
    _ShellDestination('Dashboard', AppIcons.dashboard, (ctx) => ctx.go('/dashboard')),
    _ShellDestination('Buildings', AppIcons.factory, (ctx) {
      final lastBuildingId = ctx.read<RecentBuildingState>().lastBuildingId;
      ctx.go(lastBuildingId != null ? '/building/$lastBuildingId' : '/buildings/market');
    }),
    // Forex is auth-only; signed-out players get the public Stocks screen.
    _ShellDestination(
      'Markets',
      AppIcons.forex,
      (ctx) => ctx.go(ctx.read<AuthState>().isAuthenticated ? '/forex' : '/stocks'),
    ),
    _ShellDestination('More', AppIcons.menu, (ctx) => Scaffold.of(ctx).openDrawer()),
  ];

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
    unawaited(HapticFeedback.selectionClick());
    _destinations[index].onSelected(context);
  }

  /// True when [location] is [prefix] or a sub-route of it (segment-safe, so
  /// `/market` does not match `/market-intelligence`).
  static bool _isUnder(String location, String prefix) => location == prefix || location.startsWith('$prefix/');

  int _destinationIndexFor(String location) {
    bool under(List<String> prefixes) => prefixes.any((p) => _isUnder(location, p));
    if (under(['/dashboard'])) return 1;
    if (under(['/building', '/buildings'])) return 2;
    if (under(['/forex', '/stocks', '/stock', '/market', '/exchange', '/energy-market'])) return 3;
    if (location == '/' || under(['/login', '/register', '/auth', '/onboarding', '/forgot-password', '/reset-password'])) {
      return 0;
    }
    return 4;
  }
}

class _ShellDestination {
  const _ShellDestination(this.label, this.icon, this.onSelected);

  final String label;
  final FaIconData icon;
  final void Function(BuildContext context) onSelected;
}
