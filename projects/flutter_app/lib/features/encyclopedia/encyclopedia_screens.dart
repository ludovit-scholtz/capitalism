import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class EncyclopediaScreen extends StatelessWidget {
  const EncyclopediaScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Encyclopedia', sourceView: 'ManufacturingEncyclopediaView.vue');
}

class ResourceDetailScreen extends StatelessWidget {
  const ResourceDetailScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Resource Detail', sourceView: 'ResourceDetailView.vue');
}
