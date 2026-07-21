import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class GlobalExchangeScreen extends StatelessWidget {
  const GlobalExchangeScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Exchange', sourceView: 'GlobalExchangeView.vue');
}

class StockExchangeScreen extends StatelessWidget {
  const StockExchangeScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Stocks', sourceView: 'StockExchangeView.vue');
}

class StockTradingScreen extends StatelessWidget {
  const StockTradingScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Trade Stock', sourceView: 'StockTradingView.vue');
}

class ForexExchangeScreen extends StatelessWidget {
  const ForexExchangeScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Forex', sourceView: 'ForexExchangeView.vue');
}
