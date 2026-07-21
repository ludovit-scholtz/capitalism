import 'package:flutter/material.dart';

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
  });

  final String label;
  final IconData icon;

  /// Empty when the item does not navigate to a route (e.g. it opens a
  /// panel or an external link instead).
  final String route;
  final bool requiresAuth;
  final bool requiresAdmin;
  final String? externalUrl;
}

class NavSection {
  const NavSection({required this.title, required this.items});

  final String title;
  final List<NavItem> items;
}

/// Section/item structure copied 1:1 from the web app's `AppHeader.vue` nav
/// so both clients present the same menu. Keep this in sync when the web
/// nav changes.
const List<NavSection> navSections = <NavSection>[
  NavSection(
    title: 'Main',
    items: [
      NavItem(label: 'Home', route: '/', icon: Icons.home_outlined),
      NavItem(label: 'Dashboard', route: '/dashboard', icon: Icons.dashboard_outlined, requiresAuth: true),
      NavItem(label: 'Leaderboard', route: '/leaderboard', icon: Icons.leaderboard_outlined),
      NavItem(label: 'Cities', route: '/cities', icon: Icons.location_city_outlined),
      NavItem(label: 'News', route: '/news', icon: Icons.article_outlined),
      NavItem(label: 'Tutorial', route: '/tutorial', icon: Icons.school_outlined),
    ],
  ),
  NavSection(
    title: 'Economy',
    items: [
      NavItem(label: 'Exchange', route: '/exchange', icon: Icons.swap_horiz),
      NavItem(label: 'Stocks', route: '/stocks', icon: Icons.show_chart),
      NavItem(label: 'Forex', route: '/forex', icon: Icons.currency_exchange, requiresAuth: true),
      NavItem(label: 'Contracts', route: '/contracts', icon: Icons.description_outlined, requiresAuth: true),
      NavItem(label: 'Banking', route: '/banking', icon: Icons.account_balance_outlined),
      NavItem(label: 'Bank Statement', route: '/bank-statement', icon: Icons.receipt_long_outlined, requiresAuth: true),
      NavItem(label: 'Campaigns', route: '/market-intelligence', icon: Icons.campaign_outlined, requiresAuth: true),
      NavItem(label: 'Market Dashboard', route: '/market', icon: Icons.storefront_outlined),
      NavItem(label: 'Energy', route: '/energy-market', icon: Icons.bolt_outlined),
      NavItem(label: 'Trade Routes', route: '/trade-routes', icon: Icons.alt_route_outlined, requiresAuth: true),
    ],
  ),
  NavSection(
    title: 'Build',
    items: [
      NavItem(label: 'Building Market', route: '/buildings/market', icon: Icons.apartment_outlined),
      NavItem(label: 'Encyclopedia', route: '/encyclopedia', icon: Icons.menu_book_outlined),
    ],
  ),
  NavSection(
    title: 'Social',
    items: [
      NavItem(label: 'Chat', icon: Icons.chat_bubble_outline, requiresAuth: true),
      NavItem(label: 'Discord', icon: Icons.forum_outlined, externalUrl: 'https://discord.gg/PhHSxJvDn6'),
    ],
  ),
  NavSection(
    title: 'Administration',
    items: [
      NavItem(label: 'Operations', route: '/operations/statistics', icon: Icons.admin_panel_settings_outlined, requiresAdmin: true),
    ],
  ),
];
