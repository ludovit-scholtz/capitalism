import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class CitiesScreen extends StatelessWidget {
  const CitiesScreen({super.key});

  @override
  Widget build(BuildContext context) => const PlaceholderScreen(title: 'Cities', sourceView: 'CitiesView.vue');
}

class WorldMapScreen extends StatelessWidget {
  const WorldMapScreen({super.key});

  @override
  Widget build(BuildContext context) => const PlaceholderScreen(title: 'World Map', sourceView: 'WorldMapView.vue');
}
