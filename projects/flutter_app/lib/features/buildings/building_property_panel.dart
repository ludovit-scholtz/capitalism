// Port of `BuildingPropertyPanel.vue` (ROADMAP 134) — the full replacement
// for the grid/unit-list area on APARTMENT and COMMERCIAL buildings (these
// two types are not grid-eligible, matching `isMultiUnitBuilding` on web).
// One unified panel/query/mutation serves both types (confirmed: no
// `commercialBuildingDetail` variant exists) — only the fallback area
// (1800 vs 1400 sqm when `totalAreaSqm` is unset) and chart copy differ.
//
// Editable: only the "schedule a new rent" dialog (`setRentPerSqm` —
// changes apply after a 1 in-game-day delay, matching the server). Trimmed:
// the `RentReferenceChart.vue` SVG occupancy-cap curve is replaced by the
// same underlying price-position tiering as plain text/color (same
// information, no custom chart-painting investment for a single curve).

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_detail_models.dart';
import 'building_panel_models.dart';

class BuildingPropertyPanel extends StatefulWidget {
  const BuildingPropertyPanel({super.key, required this.building, required this.detail, required this.onScheduleRent});

  final BuildingDetail building;
  final ApartmentBuildingDetail? detail;
  final Future<void> Function(double rentPerSqm) onScheduleRent;

  @override
  State<BuildingPropertyPanel> createState() => _BuildingPropertyPanelState();
}

class _BuildingPropertyPanelState extends State<BuildingPropertyPanel> {
  bool _saving = false;
  String? _saveError;

  double get _areaSqm {
    final area = widget.building.totalAreaSqm;
    if (area != null && area > 0) return area;
    return widget.building.type == 'APARTMENT' ? 1800 : 1400;
  }

  Future<void> _openRentDialog() async {
    final controller = TextEditingController(
      text: (widget.building.pendingPricePerSqm ?? widget.building.pricePerSqm)?.toStringAsFixed(2) ?? '',
    );
    _saveError = null;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('Schedule Rent Change'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'The new rent will take effect after one in-game day. Occupancy adjusts gradually based on how your price compares to the local market average.',
              ),
              const SizedBox(height: AppSpacing.sm),
              if (widget.detail != null)
                Text('Location rate: ${widget.detail!.adjustedMarketRentPerSqm.toStringAsFixed(2)} / m²'),
              const SizedBox(height: AppSpacing.sm),
              TextField(
                controller: controller,
                decoration: const InputDecoration(labelText: 'Rent per m²'),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
              ),
              if (_saveError != null) Text(_saveError!, style: TextStyle(color: Theme.of(dialogContext).colorScheme.error)),
            ],
          ),
          actions: [
            TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
            FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Schedule Change')),
          ],
        ),
      ),
    );
    if (confirmed != true) return;
    final value = double.tryParse(controller.text);
    if (value == null || value < 0) return;

    setState(() => _saving = true);
    try {
      await widget.onScheduleRent(value);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final building = widget.building;
    final detail = widget.detail;
    final occupancy = building.occupancyPercent ?? 0;
    final occupiedArea = (_areaSqm * occupancy / 100).round();

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text('Property Management', style: theme.textTheme.titleSmall)),
                FilledButton(onPressed: _saving ? null : _openRentDialog, child: const Text('Set Rent')),
              ],
            ),
            const SizedBox(height: AppSpacing.sm),
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.xs,
              children: [
                Chip(label: Text('Total area: ${_areaSqm.toStringAsFixed(0)} m²')),
                Chip(label: Text('Occupancy: ${occupancy.toStringAsFixed(1)}%')),
                Chip(
                  label: Text(building.pricePerSqm != null ? 'Rent: ${building.pricePerSqm!.toStringAsFixed(2)} / m²' : 'Rent: Not set'),
                ),
                Chip(label: Text('Occupied: $occupiedArea / ${_areaSqm.toStringAsFixed(0)} m²')),
              ],
            ),
            if (building.adjustedMarketRentPerSqm != null) ...[
              const SizedBox(height: AppSpacing.md),
              Text('Market Rate Guidance', style: theme.textTheme.titleSmall),
              const SizedBox(height: AppSpacing.xs),
              Text('City average rent: ${building.cityReferenceRentPerSqm?.toStringAsFixed(2) ?? '—'}'),
              Text(
                'Your location rate: ${building.adjustedMarketRentPerSqm!.toStringAsFixed(2)}'
                '${building.populationIndex != null ? ' (×${building.populationIndex!.toStringAsFixed(2)})' : ''}',
              ),
              if (building.pricePerSqm != null) ..._pricePosition(theme, building.pricePerSqm!, building.adjustedMarketRentPerSqm!),
            ],
            if (building.pendingPricePerSqm != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Card(
                color: theme.colorScheme.tertiaryContainer.withValues(alpha: 0.3),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.sm),
                  child: Text(
                    'Rent change scheduled: ${building.pendingPricePerSqm!.toStringAsFixed(2)} / m² '
                    'activates at tick ${building.pendingPriceActivationTick}',
                  ),
                ),
              ),
            ],
            if (detail != null && detail.revenueHistory.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.md),
              Text('Revenue History (last 100 ticks)', style: theme.textTheme.titleSmall),
              const SizedBox(height: AppSpacing.xs),
              Text('City avg: ${detail.cityAverageRentPerSqm.toStringAsFixed(2)} / m²', style: theme.textTheme.bodySmall),
              const SizedBox(height: AppSpacing.xs),
              _revenueSparkline(theme, detail.revenueHistory),
            ],
            if (occupancy == 0 && building.pricePerSqm == null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text('Set a rent per m² to start earning income from this property.', style: theme.textTheme.bodySmall),
            ],
          ],
        ),
      ),
    );
  }

  List<Widget> _pricePosition(ThemeData theme, double rent, double marketRate) {
    final ratio = marketRate == 0 ? 1.0 : rent / marketRate;
    late final String label;
    late final Color color;
    if (ratio > 1.1) {
      label = 'Overpriced – occupancy will drift toward 50%';
      color = theme.colorScheme.error;
    } else if (ratio > 1.0) {
      label = 'Above market – maximum occupancy limited to 90%';
      color = const Color(0xFFF59E0B);
    } else if (ratio >= 0.9) {
      label = 'At market rate – target ~92% occupancy';
      color = const Color(0xFF10B981);
    } else if (ratio >= 0.6) {
      label = 'Good – target 90–100% occupancy';
      color = const Color(0xFF10B981);
    } else {
      label = 'Very attractive – can reach 100% occupancy';
      color = const Color(0xFF3B82F6);
    }
    final vsMarketPct = ((ratio - 1) * 100).toStringAsFixed(0);
    return [
      const SizedBox(height: AppSpacing.xs),
      Text(label, style: theme.textTheme.bodySmall?.copyWith(color: color, fontWeight: FontWeight.bold)),
      Text('($vsMarketPct% vs. market rate)', style: theme.textTheme.bodySmall),
    ];
  }

  Widget _revenueSparkline(ThemeData theme, List<RentalTickSnapshot> history) {
    final maxRevenue = history.map((h) => h.revenue).fold<double>(0, (a, b) => b > a ? b : a);
    return SizedBox(
      height: 48,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          for (final point in history)
            Expanded(
              child: FractionallySizedBox(
                heightFactor: maxRevenue > 0 ? (point.revenue / maxRevenue).clamp(0.02, 1.0) : 0.02,
                alignment: Alignment.bottomCenter,
                child: Container(margin: const EdgeInsets.symmetric(horizontal: 0.5), color: theme.colorScheme.primary),
              ),
            ),
        ],
      ),
    );
  }
}
