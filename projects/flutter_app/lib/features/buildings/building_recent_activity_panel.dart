// Building-level recent activity feed (ROADMAP 137), mirroring
// `buildingRecentActivity` rendering in `BuildingReadonlySidebar.vue`'s
// "Recent Activity" tab — a flat list of server-formatted description
// strings (no client-side composition of the text, same as web).

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_analytics_models.dart';

const Map<String, Color> _eventTypeColors = {
  'PURCHASED': Color(0xFF0047FF),
  'MANUFACTURED': Color(0xFFFF6D00),
  'MOVED': Color(0xFF8B949E),
  'SOLD': Color(0xFF22C55E),
  'IDLE': Color(0xFF64748B),
  'BLOCKED': Color(0xFFEF4444),
};

class BuildingRecentActivityPanel extends StatelessWidget {
  const BuildingRecentActivityPanel({super.key, required this.events});

  final List<BuildingRecentActivityEvent> events;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Recent Activity', style: theme.textTheme.titleSmall),
            const SizedBox(height: AppSpacing.sm),
            if (events.isEmpty)
              Text('No recent activity.', style: theme.textTheme.bodySmall)
            else
              for (final event in events.take(10))
                Padding(
                  key: ValueKey('activity-${event.tick}-${event.eventType}-${event.description}'),
                  padding: const EdgeInsets.symmetric(vertical: 3),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Container(
                        width: 8,
                        height: 8,
                        margin: const EdgeInsets.only(top: 5, right: AppSpacing.sm),
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          color: _eventTypeColors[event.eventType] ?? theme.colorScheme.outline,
                        ),
                      ),
                      Expanded(child: Text('T${event.tick} · ${event.description}', style: theme.textTheme.bodySmall)),
                    ],
                  ),
                ),
          ],
        ),
      ),
    );
  }
}
