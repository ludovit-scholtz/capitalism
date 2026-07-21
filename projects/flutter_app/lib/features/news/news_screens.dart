import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class NewsScreen extends StatelessWidget {
  const NewsScreen({super.key});

  @override
  Widget build(BuildContext context) => const PlaceholderScreen(title: 'News', sourceView: 'NewsView.vue');
}

class NotificationsScreen extends StatelessWidget {
  const NotificationsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Notifications', sourceView: 'NotificationsView.vue');
}
