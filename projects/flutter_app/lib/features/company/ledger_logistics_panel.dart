// Cross-city shipment tracking table for the Ledger screen, ported from
// the "logistics-section" of `LedgerMainContent.vue` — shows scheduled and
// in-transit shipments between this company's buildings across cities,
// with a progress bar and delayed-shipment highlighting.

import 'package:flutter/material.dart';

import '../trade/trade_models.dart';

class LedgerLogisticsPanel extends StatelessWidget {
  const LedgerLogisticsPanel({super.key, required this.shipments, required this.currentTick});

  final List<TradeRoute> shipments;
  final int? currentTick;

  List<TradeRoute> get _active => shipments.where((s) => s.status == 'SCHEDULED' || s.status == 'IN_TRANSIT').toList();

  int _progress(TradeRoute shipment) {
    if (shipment.status == 'DELIVERED' || shipment.status == 'COMPLETED' || shipment.status == 'FAILED') return 100;
    final total = (shipment.expectedArrivalTick - shipment.scheduledDepartureTick).clamp(1, 1 << 30);
    final elapsed = ((currentTick ?? shipment.scheduledDepartureTick) - shipment.scheduledDepartureTick).clamp(0, 1 << 30);
    return ((elapsed / total) * 100).round().clamp(0, 100);
  }

  bool _isDelayed(TradeRoute shipment) => shipment.status == 'IN_TRANSIT' && currentTick != null && currentTick! > shipment.expectedArrivalTick;

  String _statusText(TradeRoute shipment) {
    if (_isDelayed(shipment)) return 'Delayed';
    return shipment.status == 'SCHEDULED' ? 'On schedule' : 'In transit';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final active = _active;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('🚚 CROSS-CITY SHIPMENTS', style: theme.textTheme.labelLarge?.copyWith(fontWeight: FontWeight.bold, letterSpacing: 0.5)),
            const SizedBox(height: 4),
            Text(
              'Goods currently moving between this company\'s buildings across cities.',
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 12),
            if (active.isEmpty)
              const Padding(padding: EdgeInsets.symmetric(vertical: 8), child: Text('No shipments currently in transit.'))
            else
              for (var i = 0; i < active.length; i++) ...[
                _ShipmentRow(shipment: active[i], progress: _progress(active[i]), delayed: _isDelayed(active[i]), statusText: _statusText(active[i])),
                if (i < active.length - 1) const Divider(height: 16),
              ],
          ],
        ),
      ),
    );
  }
}

class _ShipmentRow extends StatelessWidget {
  const _ShipmentRow({required this.shipment, required this.progress, required this.delayed, required this.statusText});

  final TradeRoute shipment;
  final int progress;
  final bool delayed;
  final String statusText;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = delayed ? Colors.red : theme.colorScheme.primary;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('${shipment.sourceCityName} → ${shipment.destinationCityName}', style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600)),
        Text(
          '${shipment.sourceBuildingName} → ${shipment.destinationBuildingName}',
          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
        const SizedBox(height: 4),
        Text('${shipment.itemName} · qty ${shipment.quantity.toStringAsFixed(0)} · arrives tick ${shipment.expectedArrivalTick}', style: theme.textTheme.bodySmall),
        const SizedBox(height: 4),
        Row(
          children: [
            Expanded(
              child: ClipRRect(
                borderRadius: BorderRadius.circular(3),
                child: LinearProgressIndicator(value: progress / 100, minHeight: 6, backgroundColor: theme.colorScheme.surfaceContainerHighest, color: color),
              ),
            ),
            const SizedBox(width: 8),
            Text(statusText, style: theme.textTheme.labelSmall?.copyWith(color: color, fontWeight: FontWeight.bold)),
          ],
        ),
      ],
    );
  }
}
