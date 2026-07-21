import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class LeaderboardScreen extends StatelessWidget {
  const LeaderboardScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Leaderboard', sourceView: 'LeaderboardView.vue');
}

class PlayerProfileScreen extends StatelessWidget {
  const PlayerProfileScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Player Profile', sourceView: 'PlayerProfileView.vue');
}
