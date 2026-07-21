import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class BuildingMarketScreen extends StatelessWidget {
  const BuildingMarketScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Building Market', sourceView: 'BuildingMarketView.vue');
}

class BuyBuildingScreen extends StatelessWidget {
  const BuyBuildingScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Buy Building', sourceView: 'BuyBuildingView.vue');
}

class BuildingDetailScreen extends StatelessWidget {
  const BuildingDetailScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Building Detail', sourceView: 'BuildingDetailView.vue');
}

class SellBuildingScreen extends StatelessWidget {
  const SellBuildingScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Sell Building', sourceView: 'SellBuildingView.vue');
}
