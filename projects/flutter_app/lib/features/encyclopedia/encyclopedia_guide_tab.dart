// Renders one of the 5 static guide topics (see `encyclopedia_guide_data.dart`)
// — a title/subtitle, an optional topics checklist, and a column of
// title+body reference cards. Mirrors the shared per-topic template block in
// `ManufacturingEncyclopediaView.vue` (the web repeats this markup once per
// topic; here it's a single reusable widget instead).

import 'package:flutter/material.dart';

import 'encyclopedia_guide_data.dart';

class EncyclopediaGuideTab extends StatelessWidget {
  const EncyclopediaGuideTab({super.key, required this.topic});

  final EncyclopediaGuideTopic topic;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(topic.title, style: theme.textTheme.titleLarge),
        const SizedBox(height: 4),
        Text(topic.subtitle, style: theme.textTheme.bodyMedium),
        if (topic.topics.isNotEmpty) ...[
          const SizedBox(height: 16),
          Text(topic.topicsHeading ?? 'Topics', style: theme.textTheme.titleSmall),
          const SizedBox(height: 8),
          for (final item in topic.topics)
            Padding(
              padding: const EdgeInsets.only(bottom: 4),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('•  '),
                  Expanded(child: Text(item, style: theme.textTheme.bodySmall)),
                ],
              ),
            ),
        ],
        const SizedBox(height: 16),
        for (final card in topic.cards)
          Card(
            key: ValueKey('guide-card-${topic.slug}-${card.title}'),
            margin: const EdgeInsets.only(bottom: 12),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(card.title, style: theme.textTheme.titleSmall),
                  const SizedBox(height: 8),
                  Text(card.body, style: theme.textTheme.bodyMedium),
                ],
              ),
            ),
          ),
      ],
    );
  }
}
