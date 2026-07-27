import 'package:font_awesome_flutter/font_awesome_flutter.dart';

import '../theme/app_icons.dart';

/// One entry in the navigation drawer. Mirrors an item from
/// `mobileNavSections`/`desktopNavSections` in
/// `projects/frontend/src/components/layout/AppHeader.vue`.
class NavItem {
  const NavItem({
    required this.label,
    required this.icon,
    this.route = '',
    this.requiresAuth = false,
    this.requiresAdmin = false,
    this.externalUrl,
    this.opensChatPanel = false,
  });

  final String label;
  final FaIconData icon;

  /// Empty when the item does not navigate to a route (e.g. it opens a
  /// panel or an external link instead).
  final String route;
  final bool requiresAuth;
  final bool requiresAdmin;
  final String? externalUrl;

  /// True for the Chat item, which opens an in-app panel rather than
  /// navigating — mirrors the side panel opened from `AppHeader.vue`'s Chat
  /// button on the web, which likewise has no route of its own.
  final bool opensChatPanel;
}

class NavSection {
  const NavSection({required this.title, required this.items});

  final String title;
  final List<NavItem> items;
}

/// Section/item structure copied 1:1 from the web app's `AppHeader.vue` nav
/// so both clients present the same menu. Keep this in sync when the web
/// nav changes. Icons come from [AppIcons], the mobile mirror of the web's
/// `fontAwesomeIcons.ts`.
///
/// The `Account` section is a mobile-only addition — `projects/frontend`
/// has no equivalent, since a single web deployment only ever talks to one
/// game shard. Mobile has no per-shard build-flavor story, so `Game
/// Servers` (`GameServerSelectionScreen`) is the in-app equivalent of
/// `projects/master-frontend`'s `GameServersView.vue`, and `Bounties`
/// (`BountyHistoryScreen`) surfaces the Master API's completed-bounty
/// history, which currently only has a `master-frontend` UI.
const List<NavSection> navSections = <NavSection>[
  NavSection(
    title: 'Main',
    items: [
      NavItem(label: 'Home', route: '/', icon: AppIcons.home),
      NavItem(label: 'Dashboard', route: '/dashboard', icon: AppIcons.dashboard, requiresAuth: true),
      NavItem(label: 'Leaderboard', route: '/leaderboard', icon: AppIcons.leaderboard),
      NavItem(label: 'Cities', route: '/cities', icon: AppIcons.cities),
      NavItem(label: 'News', route: '/news', icon: AppIcons.news),
      NavItem(label: 'Tutorial', route: '/tutorial', icon: AppIcons.tutorial),
    ],
  ),
  NavSection(
    title: 'Economy',
    items: [
      NavItem(label: 'Exchange', route: '/exchange', icon: AppIcons.exchange),
      NavItem(label: 'Stocks', route: '/stocks', icon: AppIcons.stocks),
      NavItem(label: 'Forex', route: '/forex', icon: AppIcons.forex, requiresAuth: true),
      NavItem(label: 'Contracts', route: '/contracts', icon: AppIcons.contracts, requiresAuth: true),
      NavItem(label: 'Banking', route: '/banking', icon: AppIcons.banking),
      NavItem(label: 'Bank Statement', route: '/bank-statement', icon: AppIcons.bankStatement, requiresAuth: true),
      NavItem(label: 'Campaigns', route: '/market-intelligence', icon: AppIcons.campaigns, requiresAuth: true),
      NavItem(label: 'Market Dashboard', route: '/market', icon: AppIcons.marketDashboard),
      NavItem(label: 'Energy', route: '/energy-market', icon: AppIcons.energy),
      NavItem(label: 'Trade Routes', route: '/trade-routes', icon: AppIcons.tradeRoutes, requiresAuth: true),
    ],
  ),
  NavSection(
    title: 'Build',
    items: [
      NavItem(label: 'Building Market', route: '/buildings/market', icon: AppIcons.buildingMarket),
      NavItem(label: 'Encyclopedia', route: '/encyclopedia', icon: AppIcons.encyclopedia),
    ],
  ),
  NavSection(
    title: 'Social',
    items: [
      NavItem(label: 'Chat', icon: AppIcons.chat, requiresAuth: true, opensChatPanel: true),
      NavItem(label: 'Discord', icon: AppIcons.discord, externalUrl: 'https://discord.gg/PhHSxJvDn6'),
    ],
  ),
  NavSection(
    title: 'Account',
    items: [
      NavItem(label: 'Game Servers', route: '/servers', icon: AppIcons.server),
      NavItem(label: 'Bounties', route: '/bounties', icon: AppIcons.bounty, requiresAuth: true),
    ],
  ),
  NavSection(
    title: 'Administration',
    items: [
      NavItem(label: 'Operations', route: '/operations/statistics', icon: AppIcons.operations, requiresAdmin: true),
    ],
  ),
  NavSection(
    title: 'App',
    items: [
      NavItem(label: 'Settings', route: '/settings', icon: AppIcons.settings),
      NavItem(label: 'About', route: '/about', icon: AppIcons.about),
    ],
  ),
];
