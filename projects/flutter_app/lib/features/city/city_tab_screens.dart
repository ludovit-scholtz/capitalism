import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class CityOverviewScreen extends StatelessWidget {
  const CityOverviewScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'City Overview', sourceView: 'CityOverviewTab.vue');
}

class CityEconomyScreen extends StatelessWidget {
  const CityEconomyScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'City Economy', sourceView: 'CityEconomyTab.vue');
}

class CityBuildingsScreen extends StatelessWidget {
  const CityBuildingsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'City Buildings', sourceView: 'CityBuildingsTab.vue');
}

class CityMarketScreen extends StatelessWidget {
  const CityMarketScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'City Market', sourceView: 'CityMarketTab.vue');
}

class CityContractsScreen extends StatelessWidget {
  const CityContractsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'City Contracts', sourceView: 'CityContractsTab.vue');
}

class CityCompetitorsScreen extends StatelessWidget {
  const CityCompetitorsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'City Competitors', sourceView: 'CityCompetitorsTab.vue');
}
