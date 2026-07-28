// Ported from
// `projects/frontend/src/components/cityMap/CityMediaHousesSection.vue` —
// grid of media-house cards for a city, with channel icon, ownership/status
// badges, and effectiveness/content-ranking stats.

import 'package:flutter/material.dart';

import '../buildings/building_panel_models.dart';

class CityMediaHousesSection extends StatelessWidget {
  const CityMediaHousesSection({super.key, required this.mediaHouses, required this.loading});

  final List<CityMediaHouse> mediaHouses;
  final bool loading;

  static String _channelIcon(String? mediaType) {
    switch (mediaType) {
      case 'TV':
        return '📺';
      case 'RADIO':
        return '📻';
      default:
        return '📰';
    }
  }

  static String _effectivenessHint(String? mediaType) {
    switch (mediaType) {
      case 'TV':
        return 'Highest reach, highest cost per tick.';
      case 'RADIO':
        return 'Moderate reach, moderate cost.';
      default:
        return 'Lowest reach, lowest cost per tick.';
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(children: [const Text('📺 ', style: TextStyle(fontSize: 20)), Text('Media Houses', style: theme.textTheme.titleMedium)]),
        const SizedBox(height: 4),
        Text(
          'Companies broadcasting advertising content in this city.',
          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
        const SizedBox(height: 12),
        if (loading)
          const Center(child: Padding(padding: EdgeInsets.symmetric(vertical: 24), child: CircularProgressIndicator()))
        else if (mediaHouses.isEmpty)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('No media houses have been built in this city yet.'),
                  const SizedBox(height: 4),
                  Text(
                    'Build a MEDIA_HOUSE to advertise your products here.',
                    style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                ],
              ),
            ),
          )
        else
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [for (final mh in mediaHouses) _MediaHouseCard(mediaHouse: mh, channelIcon: _channelIcon(mh.mediaType), effectivenessHint: _effectivenessHint(mh.mediaType))],
          ),
      ],
    );
  }
}

class _MediaHouseCard extends StatelessWidget {
  const _MediaHouseCard({required this.mediaHouse, required this.channelIcon, required this.effectivenessHint});

  final CityMediaHouse mediaHouse;
  final String channelIcon;
  final String effectivenessHint;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final offline = mediaHouse.powerStatus == 'OFFLINE';
    final underConstruction = mediaHouse.isUnderConstruction;

    return Opacity(
      opacity: offline && !underConstruction ? 0.6 : 1,
      child: Container(
        width: 260,
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          border: Border.all(color: underConstruction ? Colors.amber : theme.colorScheme.outlineVariant),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(padding: const EdgeInsets.only(top: 2), child: Text(channelIcon, style: const TextStyle(fontSize: 28))),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(mediaHouse.name, style: theme.textTheme.titleSmall, overflow: TextOverflow.ellipsis),
                  const SizedBox(height: 4),
                  Wrap(
                    spacing: 4,
                    runSpacing: 4,
                    children: [
                      _Badge(text: mediaHouse.mediaType ?? '?', color: theme.colorScheme.primary),
                      if (mediaHouse.isGovernmentOwned) const _Badge(text: 'GOV', color: Colors.amber),
                      if (underConstruction)
                        const _Badge(text: 'UNDER CONSTRUCTION', color: Colors.amber)
                      else if (offline)
                        const _Badge(text: 'OFFLINE', color: Colors.red),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Owner: ${mediaHouse.ownerCompanyName ?? 'Unknown'}',
                    style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                  const SizedBox(height: 2),
                  Text.rich(
                    TextSpan(
                      style: theme.textTheme.bodySmall,
                      children: [
                        const TextSpan(text: 'Effectiveness: '),
                        TextSpan(text: '×${mediaHouse.effectivenessMultiplier.toStringAsFixed(1)} ', style: const TextStyle(fontWeight: FontWeight.bold)),
                        TextSpan(text: effectivenessHint, style: TextStyle(color: theme.colorScheme.onSurfaceVariant, fontSize: 11)),
                      ],
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text.rich(
                    TextSpan(
                      style: theme.textTheme.bodySmall,
                      children: [
                        const TextSpan(text: 'Content ranking: '),
                        TextSpan(text: '${mediaHouse.contentRanking.toStringAsFixed(0)}%', style: const TextStyle(fontWeight: FontWeight.bold)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Badge extends StatelessWidget {
  const _Badge({required this.text, required this.color});

  final String text;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(color: color.withValues(alpha: 0.15), borderRadius: BorderRadius.circular(4)),
      child: Text(text, style: TextStyle(color: color, fontSize: 10, fontWeight: FontWeight.bold, letterSpacing: 0.3)),
    );
  }
}
