// Port of `BuildingPowerPlantPanel.vue` (ROADMAP 132) — rendered as a
// sibling panel above the grid for POWER_PLANT buildings, exactly like the
// web. Also folds in the "power priority" control that on web lives in the
// separate `BuildingEnergyPanel.vue` (the grid editor's per-unit Energy
// tab) — that component's *other* control, max spot-market bid price, is
// explicitly rejected by the backend for POWER_PLANT buildings
// (`INVALID_BUILDING_TYPE`), so it's omitted here; it belongs with a future
// consumer-building energy settings port.
//
// Trimmed from the web (documented, not oversights):
// - The per-tick P&L bar chart (`powerPlantAnalytics.timeline`) — the
//   aggregate P&L totals below cover the same information without chart
//   rendering.
// - The static 7-unit-type description guide cards — pure documentation,
//   no state, not load-bearing for gameplay.
// - The "projected next-tick economics" block on the city-power-status
//   card — a client-side estimate using hardcoded constants that must be
//   kept in sync with `GameConstants.GridSurplusIncomePerMwTick`/
//   `GridFinePerMwTick`; the already-ported P&L totals give the same
//   information from real settled ledger data instead of a projection.
//
// Spot-market listing here reuses the *same* `listEnergyForSale`/
// `cancelEnergyListing` mutations already wired into the global
// `EnergyMarketScreen` (`lib/features/market/`) — this panel is a
// building-scoped convenience UI over the same operations, not a
// duplicate feature.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_detail_models.dart';
import 'building_panel_models.dart';

class BuildingPowerPlantPanel extends StatefulWidget {
  const BuildingPowerPlantPanel({
    super.key,
    required this.building,
    required this.analytics,
    required this.cityPowerBalance,
    required this.onSetDispatch,
    required this.onSetPriority,
    required this.onListEnergy,
    required this.onCancelListing,
  });

  final BuildingDetail building;
  final PowerPlantAnalytics? analytics;
  final CityPowerBalance? cityPowerBalance;
  final Future<void> Function(int dispatchPercent) onSetDispatch;
  final Future<void> Function(int priority) onSetPriority;
  final Future<void> Function(double pricePerKwh, double capacityKw) onListEnergy;
  final Future<void> Function(String listingId) onCancelListing;

  @override
  State<BuildingPowerPlantPanel> createState() => _BuildingPowerPlantPanelState();
}

class _BuildingPowerPlantPanelState extends State<BuildingPowerPlantPanel> {
  late int _draftDispatch = widget.building.dispatchTargetPercent ?? 100;
  late int _draftPriority = widget.building.powerPriority ?? 5;
  bool _savingDispatch = false;
  bool _savingPriority = false;
  bool _showListingForm = false;
  final _priceController = TextEditingController();
  final _capacityController = TextEditingController();
  bool _savingListing = false;
  bool _cancellingListing = false;

  @override
  void dispose() {
    _priceController.dispose();
    _capacityController.dispose();
    super.dispose();
  }

  bool get _isThermal => widget.building.powerPlantType == 'COAL' || widget.building.powerPlantType == 'GAS';

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final building = widget.building;
    final analytics = widget.analytics;
    final balance = widget.cityPowerBalance;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('⚡ ${building.powerOutput?.toStringAsFixed(0) ?? '?'} MW · ${building.powerPlantType ?? 'Power Plant'}', style: theme.textTheme.titleSmall),
            if (balance != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text('City Power Status', style: theme.textTheme.labelLarge),
              Text(_cityStatusLabel(balance.status)),
              Text('Supply ${balance.totalSupplyMw.toStringAsFixed(0)} MW · Demand ${balance.totalDemandMw.toStringAsFixed(0)} MW · Balance ${balance.reserveMw.toStringAsFixed(0)} MW'),
            ],
            const SizedBox(height: AppSpacing.md),
            Text('Dispatch Control', style: theme.textTheme.labelLarge),
            Row(
              children: [
                Expanded(
                  child: Slider(
                    value: _draftDispatch.toDouble(),
                    min: 0,
                    max: 100,
                    divisions: 20,
                    label: '$_draftDispatch%',
                    onChanged: (v) => setState(() => _draftDispatch = v.round()),
                  ),
                ),
                Text('$_draftDispatch%'),
              ],
            ),
            OutlinedButton(
              onPressed: (_savingDispatch || _draftDispatch == (building.dispatchTargetPercent ?? 100))
                  ? null
                  : () async {
                      setState(() => _savingDispatch = true);
                      try {
                        await widget.onSetDispatch(_draftDispatch);
                      } finally {
                        if (mounted) setState(() => _savingDispatch = false);
                      }
                    },
              child: Text(_savingDispatch ? 'Applying…' : 'Apply'),
            ),
            const SizedBox(height: AppSpacing.md),
            Text('Power Priority', style: theme.textTheme.labelLarge),
            Text('Higher priority buildings stay online first during a grid shortage.', style: theme.textTheme.bodySmall),
            Row(
              children: [
                Expanded(
                  child: Slider(
                    value: _draftPriority.toDouble(),
                    min: 1,
                    max: 10,
                    divisions: 9,
                    label: '$_draftPriority',
                    onChanged: (v) => setState(() => _draftPriority = v.round()),
                  ),
                ),
                Text('$_draftPriority'),
              ],
            ),
            OutlinedButton(
              onPressed: (_savingPriority || _draftPriority == (building.powerPriority ?? 5))
                  ? null
                  : () async {
                      setState(() => _savingPriority = true);
                      try {
                        await widget.onSetPriority(_draftPriority);
                      } finally {
                        if (mounted) setState(() => _savingPriority = false);
                      }
                    },
              child: Text(_savingPriority ? 'Saving…' : 'Save priority'),
            ),
            const SizedBox(height: AppSpacing.md),
            Text('Spot Market Listing', style: theme.textTheme.labelLarge),
            if (analytics?.activeListing != null)
              _activeListingCard(theme, analytics!.activeListing!)
            else
              _createListingForm(theme),
            if (_isThermal && analytics != null) ...[const SizedBox(height: AppSpacing.md), _fuelReserveSection(theme, analytics)],
            if (analytics != null) ...[const SizedBox(height: AppSpacing.md), _pnlSummary(theme, analytics)],
          ],
        ),
      ),
    );
  }

  String _cityStatusLabel(String status) {
    switch (status) {
      case 'BALANCED':
        return 'Fully powered';
      case 'CONSTRAINED':
        return 'Constrained (50–99% demand met)';
      default:
        return 'Critical shortage (>50% demand unmet)';
    }
  }

  Widget _activeListingCard(ThemeData theme, EnergyListing listing) {
    return Card(
      color: theme.colorScheme.surfaceContainer,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.sm),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('${listing.pricePerKwhLocal.toStringAsFixed(4)}/kWh · ${listing.capacityKw.toStringAsFixed(0)} kW listed · ${listing.availableKw.toStringAsFixed(0)} kW available'),
            const SizedBox(height: AppSpacing.xs),
            OutlinedButton(
              onPressed: _cancellingListing
                  ? null
                  : () async {
                      setState(() => _cancellingListing = true);
                      try {
                        await widget.onCancelListing(listing.listingId);
                      } finally {
                        if (mounted) setState(() => _cancellingListing = false);
                      }
                    },
              child: Text(_cancellingListing ? 'Cancelling…' : 'Cancel listing'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _createListingForm(ThemeData theme) {
    if (!_showListingForm) {
      return OutlinedButton(onPressed: () => setState(() => _showListingForm = true), child: const Text('List surplus energy for sale'));
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextField(
          controller: _priceController,
          decoration: const InputDecoration(labelText: 'Price per kWh (local currency)'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
        ),
        TextField(
          controller: _capacityController,
          decoration: const InputDecoration(labelText: 'Capacity to list (kW)'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
        ),
        const SizedBox(height: AppSpacing.xs),
        Row(
          children: [
            FilledButton(
              onPressed: _savingListing
                  ? null
                  : () async {
                      final price = double.tryParse(_priceController.text);
                      final capacity = double.tryParse(_capacityController.text);
                      if (price == null || price <= 0 || capacity == null || capacity <= 0) return;
                      setState(() => _savingListing = true);
                      try {
                        await widget.onListEnergy(price, capacity);
                        if (mounted) setState(() => _showListingForm = false);
                      } finally {
                        if (mounted) setState(() => _savingListing = false);
                      }
                    },
              child: Text(_savingListing ? 'Creating…' : 'Create listing'),
            ),
            const SizedBox(width: AppSpacing.sm),
            TextButton(onPressed: () => setState(() => _showListingForm = false), child: const Text('Cancel')),
          ],
        ),
      ],
    );
  }

  Widget _fuelReserveSection(ThemeData theme, PowerPlantAnalytics analytics) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Fuel Reserve (${analytics.fuelTypeLabel ?? widget.building.powerPlantType})', style: theme.textTheme.labelLarge),
        if (analytics.maxFuelReserveMwh == 0)
          Text('No FUEL_PURCHASE units installed.', style: theme.textTheme.bodySmall)
        else ...[
          Text('${analytics.fuelReserveMwh.toStringAsFixed(0)} / ${analytics.maxFuelReserveMwh.toStringAsFixed(0)} MWh'),
          ClipRRect(
            borderRadius: BorderRadius.circular(AppRadius.sm),
            child: LinearProgressIndicator(value: (analytics.fuelReservePercent / 100).clamp(0, 1), minHeight: 8),
          ),
          Text('Procurement: ${analytics.fuelPurchaseCapacityMwhPerTick.toStringAsFixed(1)} MWh/tick', style: theme.textTheme.bodySmall),
          if (analytics.fuelConstrainedOutputMw > 0)
            Text('⚠ Output constrained by ${analytics.fuelConstrainedOutputMw.toStringAsFixed(0)} MW due to low fuel.', style: theme.textTheme.bodySmall),
          if (analytics.energyProducingCapacityMw == 0)
            Text('No ENERGY_PRODUCING units installed — fuel cannot be converted to power.', style: theme.textTheme.bodySmall),
        ],
      ],
    );
  }

  Widget _pnlSummary(ThemeData theme, PowerPlantAnalytics analytics) {
    return Wrap(
      spacing: AppSpacing.sm,
      runSpacing: AppSpacing.xs,
      children: [
        Chip(label: Text('Surplus income: ${analytics.totalSurplusIncome.toStringAsFixed(0)}')),
        Chip(label: Text('Grid fines: ${analytics.totalGridFines.toStringAsFixed(0)}')),
        Chip(label: Text('Operating costs: ${analytics.totalOperatingCosts.toStringAsFixed(0)}')),
        if (_isThermal) Chip(label: Text('Fuel costs: ${analytics.totalFuelCosts.toStringAsFixed(0)}')),
        if (analytics.totalSpotMarketRevenue > 0) Chip(label: Text('Spot revenue: ${analytics.totalSpotMarketRevenue.toStringAsFixed(0)}')),
        Chip(
          label: Text('Net profit: ${analytics.totalNetProfit.toStringAsFixed(0)}'),
          backgroundColor: (analytics.totalNetProfit >= 0 ? const Color(0xFF10B981) : theme.colorScheme.error).withValues(alpha: 0.2),
        ),
      ],
    );
  }
}
