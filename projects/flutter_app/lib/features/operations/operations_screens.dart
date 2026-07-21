import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class OperationsOverviewScreen extends StatelessWidget {
  const OperationsOverviewScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Operations Overview', sourceView: 'OperationsOverviewView.vue');
}

class OperationsMoneyFlowScreen extends StatelessWidget {
  const OperationsMoneyFlowScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Money Flow', sourceView: 'OperationsStatisticsView.vue');
}

class OperationsProductAnalyticsScreen extends StatelessWidget {
  const OperationsProductAnalyticsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Product Analytics', sourceView: 'OperationsAnalyticsView.vue');
}

class OperationsNewsScreen extends StatelessWidget {
  const OperationsNewsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Operations News', sourceView: 'OperationsNewsView.vue');
}

class OperationsPlayersScreen extends StatelessWidget {
  const OperationsPlayersScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Operations Players', sourceView: 'OperationsPlayersView.vue');
}

class OperationsPlayerDetailScreen extends StatelessWidget {
  const OperationsPlayerDetailScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Player Detail', sourceView: 'OperationsPlayerDetailView.vue');
}
