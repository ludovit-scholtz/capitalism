import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class MarketIntelligenceScreen extends StatelessWidget {
  const MarketIntelligenceScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Campaigns', sourceView: 'MarketIntelligenceView.vue');
}

class MarketDashboardScreen extends StatelessWidget {
  const MarketDashboardScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Market Dashboard', sourceView: 'MarketDashboardView.vue');
}

class EnergyMarketScreen extends StatelessWidget {
  const EnergyMarketScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Energy Market', sourceView: 'EnergyMarketView.vue');
}

class GlobalEventsScreen extends StatelessWidget {
  const GlobalEventsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Global Events', sourceView: 'GlobalEventsPanel.vue');
}

class MarketingAnalyticsScreen extends StatelessWidget {
  const MarketingAnalyticsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Marketing Analytics', sourceView: 'MarketingAnalyticsView.vue');
}
