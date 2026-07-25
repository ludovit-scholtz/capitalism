// Per-unit configuration editing (ROADMAP 123), the upgrade preview
// (ROADMAP 126), and inventory visualization (ROADMAP 127) combined into
// one scrollable bottom sheet — a deliberate simplification of the web's
// 4-tab layout (`UnitConfigurationTabView.vue`: Config/Energy/Performance/
// Maintenance) into a single sheet with sections, since a 4-tab `TabBar`
// adds real navigation complexity for a mobile bottom sheet without adding
// capability. The Energy tab (building-level dispatch/priority/max-bid
// settings, not per-unit) is out of scope here — it belongs with the
// Power Plant control panel, a separate still-open ROADMAP item.
//
// Field set per unit type verified against `BuildingUnitConfigFields.vue`.
// Trimmed from that component (documented, not oversights):
// - Item pickers are plain catalog dropdowns (full `resourceTypes`/
//   `productTypes` list), not the web's reachability/ranked-product
//   filtering (`getManufacturingSelectableItems`, `rankedProductTypes`,
//   `getSalesUnitProductOptions`) — that filtering needs its own set of
//   GraphQL queries and is a materially separate scope from "can the
//   player edit these fields at all".
// - No vendor-lock company picker, no media-house selector (needs its own
//   `cityMediaHouses` query — MARKETING's `mediaHouseBuildingId` is edited
//   as a plain text field for the building id here), no Exchange
//   sourcing-candidate panel. These map to their own separate ROADMAP items
//   (Global Exchange sourcing/vendor-selector, Media House control panel).

import 'dart:async';

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import '../exchange/forex_models.dart' show MarketEvent;
import 'building_analytics_models.dart';
import 'building_analytics_service.dart';
import 'building_detail_service.dart';
import 'building_grid_draft_controller.dart';
import 'building_grid_models.dart';
import 'building_inventory_fill_bar.dart';
import 'building_public_sales_panel.dart';
import 'building_sales_models.dart';
import 'building_sales_service.dart';
import 'building_sourcing_models.dart';
import 'building_sourcing_panel.dart';
import 'building_sourcing_service.dart';
import 'building_unit_history_panel.dart';

class UnitConfigSheet extends StatefulWidget {
  const UnitConfigSheet({
    super.key,
    required this.controller,
    required this.service,
    required this.salesService,
    required this.sourcingService,
    required this.analyticsService,
    required this.unit,
    required this.resourceNames,
    required this.productNames,
    required this.unitResourceHistories,
    required this.cityId,
    required this.onChanged,
    required this.onRemove,
  });

  final BuildingGridDraftController controller;
  final BuildingDetailService service;
  final BuildingSalesService salesService;
  final BuildingSourcingService sourcingService;
  final BuildingAnalyticsService analyticsService;
  final EditableGridUnit unit;
  final Map<String, String> resourceNames;
  final Map<String, String> productNames;

  /// Full building-wide resource history (fetched once per building load),
  /// filtered to this unit's id in [build] — avoids re-querying
  /// `buildingUnitResourceHistories` on every sheet open.
  final List<UnitResourceHistoryPoint> unitResourceHistories;
  final String? cityId;
  final VoidCallback onChanged;
  final VoidCallback onRemove;

  @override
  State<UnitConfigSheet> createState() => _UnitConfigSheetState();
}

class _UnitConfigSheetState extends State<UnitConfigSheet> {
  UnitUpgradeInfo? _upgradeInfo;
  BuildingUnitInventorySummary? _inventory;
  bool _loadingExtras = true;

  // PUBLIC_SALES tools (ROADMAP 135).
  PublicSalesAnalytics? _publicSalesAnalytics;
  List<MarketEvent> _marketEvents = const [];
  bool _salesLoading = false;

  // Global Exchange sourcing (ROADMAP 136).
  ProcurementPreview? _procurementPreview;
  List<SourcingCandidate> _sourcingCandidates = const [];
  bool _sourcingLoading = false;

  // Per-unit product analytics (ROADMAP 137, MANUFACTURING only).
  UnitProductAnalytics? _productAnalytics;
  bool _productAnalyticsLoading = false;

  // Flush-storage also applies to STORAGE/MINING/MANUFACTURING on web, not
  // just PUBLIC_SALES (which gets its own button inside
  // `PublicSalesToolsPanel`) — this is that button's loading flag.
  bool _flushingOther = false;

  static const _flushableUnitTypes = {'STORAGE', 'MINING', 'MANUFACTURING'};

  bool get _isPersistedUnit => !widget.unit.id.startsWith('draft-');

  @override
  void initState() {
    super.initState();
    _loadExtras();
  }

  Future<void> _loadExtras() async {
    if (!_isPersistedUnit) {
      setState(() => _loadingExtras = false);
      return;
    }
    try {
      final results = await Future.wait([
        widget.service.fetchUnitUpgradeInfo(widget.unit.id),
        widget.service.fetchInventorySummaries(widget.controller.building!.id),
      ]);
      if (!mounted) return;
      final upgradeInfo = results[0] as UnitUpgradeInfo?;
      final summaries = results[1] as List<BuildingUnitInventorySummary>;
      BuildingUnitInventorySummary? matched;
      for (final s in summaries) {
        if (s.buildingUnitId == widget.unit.id) {
          matched = s;
          break;
        }
      }
      setState(() {
        _upgradeInfo = upgradeInfo;
        _inventory = matched;
        _loadingExtras = false;
      });
    } catch (_) {
      if (mounted) setState(() => _loadingExtras = false);
    }
    unawaited(_loadTypeSpecificExtras());
  }

  Future<void> _loadTypeSpecificExtras() async {
    switch (widget.unit.unitType) {
      case 'PUBLIC_SALES':
        setState(() => _salesLoading = true);
        try {
          final results = await Future.wait([
            widget.salesService.fetchPublicSalesAnalytics(widget.unit.id),
            widget.salesService.fetchActiveMarketEvents(widget.cityId),
          ]);
          if (mounted) {
            setState(() {
              _publicSalesAnalytics = results[0] as PublicSalesAnalytics?;
              _marketEvents = results[1] as List<MarketEvent>;
              _salesLoading = false;
            });
          }
        } catch (_) {
          if (mounted) setState(() => _salesLoading = false);
        }
      case 'PURCHASE':
        setState(() => _sourcingLoading = true);
        try {
          final results = await Future.wait([
            widget.sourcingService.fetchProcurementPreview(widget.unit.id),
            widget.sourcingService.fetchSourcingCandidates(widget.unit.id),
          ]);
          if (mounted) {
            setState(() {
              _procurementPreview = results[0] as ProcurementPreview?;
              _sourcingCandidates = results[1] as List<SourcingCandidate>;
              _sourcingLoading = false;
            });
          }
        } catch (_) {
          if (mounted) setState(() => _sourcingLoading = false);
        }
      case 'MANUFACTURING':
        setState(() => _productAnalyticsLoading = true);
        try {
          final analytics = await widget.analyticsService.fetchUnitProductAnalytics(widget.unit.id);
          if (mounted) {
            setState(() {
              _productAnalytics = analytics;
              _productAnalyticsLoading = false;
            });
          }
        } catch (_) {
          if (mounted) setState(() => _productAnalyticsLoading = false);
        }
    }
  }

  Future<void> _saveInventoryThreshold(double? threshold) async {
    await widget.salesService.setInventoryAlertThreshold(buildingUnitId: widget.unit.id, threshold: threshold);
    widget.unit.lowInventoryAlertThreshold = threshold;
    widget.onChanged();
  }

  Future<void> _flushStorage() async {
    final result = await widget.salesService.flushStorage(widget.unit.id);
    if (mounted) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text('Discarded ${result.discardedItemCount} item(s) worth ${result.totalDiscardedValue.toStringAsFixed(0)}.')));
    }
    widget.onChanged();
    await _loadExtras();
  }

  Future<void> _confirmAndFlushOther() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Discard all inventory?'),
        content: const Text('This permanently discards everything currently stored in this unit. This cannot be undone.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Yes, Discard All')),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _flushingOther = true);
    try {
      await _flushStorage();
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not flush storage. Please try again.')));
    } finally {
      if (mounted) setState(() => _flushingOther = false);
    }
  }

  void _notify() {
    widget.onChanged();
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final unit = widget.unit;
    return DraggableScrollableSheet(
      initialChildSize: 0.75,
      minChildSize: 0.4,
      maxChildSize: 0.95,
      expand: false,
      builder: (context, scrollController) => SafeArea(
        child: ListView(
          controller: scrollController,
          padding: const EdgeInsets.all(AppSpacing.md),
          children: [
            Text('${unit.unitType} · Level ${unit.level}', style: theme.textTheme.titleMedium),
            Text('Position (${unit.gridX}, ${unit.gridY})', style: theme.textTheme.bodySmall),
            const SizedBox(height: AppSpacing.md),
            ..._configFieldsFor(unit),
            if (_inventory != null) ...[
              const SizedBox(height: AppSpacing.md),
              Text('Inventory', style: theme.textTheme.titleSmall),
              const SizedBox(height: AppSpacing.xs),
              InventoryFillBar(summary: _inventory!, isPublicSales: unit.unitType == 'PUBLIC_SALES'),
            ],
            if (_upgradeInfo != null) ...[const SizedBox(height: AppSpacing.md), _upgradePreviewCard(theme, _upgradeInfo!)],
            if (_loadingExtras) const Padding(padding: EdgeInsets.symmetric(vertical: AppSpacing.md), child: LinearProgressIndicator()),
            if (_isPersistedUnit) UnitResourceHistoryPanel(history: widget.unitResourceHistories.where((p) => p.buildingUnitId == unit.id).toList()),
            if (_isPersistedUnit && unit.unitType == 'PUBLIC_SALES')
              PublicSalesToolsPanel(
                analytics: _publicSalesAnalytics,
                analyticsLoading: _salesLoading,
                marketEvents: _marketEvents,
                currentThreshold: unit.lowInventoryAlertThreshold,
                onSaveThreshold: _saveInventoryThreshold,
                onFlushStorage: _flushStorage,
              ),
            if (_isPersistedUnit && unit.unitType == 'PURCHASE')
              SourcingComparisonPanel(preview: _procurementPreview, candidates: _sourcingCandidates, loading: _sourcingLoading),
            if (_isPersistedUnit && unit.unitType == 'MANUFACTURING')
              UnitProductAnalyticsPanel(analytics: _productAnalytics, loading: _productAnalyticsLoading),
            if (_isPersistedUnit && _flushableUnitTypes.contains(unit.unitType)) ...[
              const SizedBox(height: AppSpacing.md),
              OutlinedButton.icon(
                icon: _flushingOther
                    ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Icon(Icons.delete_sweep_outlined, size: 18),
                label: const Text('Discard All Inventory'),
                onPressed: _flushingOther ? null : _confirmAndFlushOther,
              ),
            ],
            const SizedBox(height: AppSpacing.md),
            OutlinedButton.icon(
              icon: const Icon(Icons.delete_outline, size: 18),
              label: const Text('Remove unit'),
              onPressed: () {
                widget.onRemove();
                Navigator.of(context).pop();
              },
            ),
          ],
        ),
      ),
    );
  }

  Widget _upgradePreviewCard(ThemeData theme, UnitUpgradeInfo info) {
    final staged = widget.controller.draftUpgradeUnitIds.contains(widget.unit.id);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Unit Upgrade', style: theme.textTheme.titleSmall),
            if (info.isMaxLevel)
              const Text('Max Level — this unit is fully upgraded.')
            else if (!info.isUpgradable)
              const Text('This unit type cannot be upgraded.')
            else ...[
              Text('Level ${info.currentLevel} → Level ${info.nextLevel}'),
              Text('${info.statLabel}: ${info.currentStat.toStringAsFixed(1)} → ${info.nextStat.toStringAsFixed(1)}'),
              Text('Storage capacity: ${info.currentStorageCapacity.toStringAsFixed(0)} → ${info.nextStorageCapacity.toStringAsFixed(0)}'),
              Text('Labor cost/tick: ${info.currentLaborCostPerTick.toStringAsFixed(2)} → ${info.nextLaborCostPerTick.toStringAsFixed(2)}'),
              Text('Energy cost/tick: ${info.currentEnergyCostPerTick.toStringAsFixed(2)} → ${info.nextEnergyCostPerTick.toStringAsFixed(2)}'),
              Text('This unit will be offline during the upgrade — ${info.upgradeTicks} ticks of downtime.'),
              const SizedBox(height: AppSpacing.sm),
              Text('Cost: ${info.upgradeCost.toStringAsFixed(0)} · Duration: ${info.upgradeTicks} ticks'),
              const SizedBox(height: AppSpacing.sm),
              OutlinedButton(
                onPressed: () {
                  widget.controller.toggleStagedUpgrade(widget.unit.id);
                  _notify();
                },
                child: Text(staged ? 'Remove from queue' : 'Stage Upgrade'),
              ),
            ],
          ],
        ),
      ),
    );
  }

  List<Widget> _configFieldsFor(EditableGridUnit unit) {
    final theme = Theme.of(context);
    switch (unit.unitType) {
      case 'PURCHASE':
        return [
          _itemDropdown(unit, includeResources: true, includeProducts: true),
          _numberField('Max price', unit.maxPrice, (v) => unit.maxPrice = v),
          _numberField('Min quality (0-1)', unit.minQuality, (v) => unit.minQuality = v),
          _stringOptions('Purchase source', unit.purchaseSource, const ['OPTIMAL', 'EXCHANGE', 'LOCAL'], (v) => unit.purchaseSource = v),
        ];
      case 'MANUFACTURING':
        return [_itemDropdown(unit, includeResources: false, includeProducts: true)];
      case 'B2B_SALES':
        return [
          _itemDropdown(unit, includeResources: false, includeProducts: true),
          _numberField('Min price', unit.minPrice, (v) => unit.minPrice = v),
          _stringOptions('Sale visibility', unit.saleVisibility, const ['PUBLIC', 'COMPANY', 'GROUP'], (v) => unit.saleVisibility = v),
        ];
      case 'PUBLIC_SALES':
        return [
          _itemDropdown(unit, includeResources: false, includeProducts: true),
          _numberField('Min price', unit.minPrice, (v) => unit.minPrice = v),
        ];
      case 'MARKETING':
        return [
          _numberField('Budget', unit.budget, (v) => unit.budget = v),
          _textField('Media house building id', unit.mediaHouseBuildingId, (v) => unit.mediaHouseBuildingId = v),
        ];
      case 'BRANDING':
        return [_stringOptions('Brand scope', unit.brandScope, const ['PRODUCT', 'CATEGORY', 'COMPANY'], (v) => unit.brandScope = v)];
      case 'PRODUCT_QUALITY':
        return [_itemDropdown(unit, includeResources: false, includeProducts: true)];
      case 'BRAND_QUALITY':
        return [
          _stringOptions('Brand scope', unit.brandScope, const ['PRODUCT', 'CATEGORY', 'COMPANY'], (value) {
            unit.brandScope = value;
            if (value != 'PRODUCT') unit.productTypeId = null;
            if (value != 'CATEGORY') unit.industryCategory = null;
          }),
          if (unit.brandScope == 'PRODUCT') _itemDropdown(unit, includeResources: false, includeProducts: true),
          if (unit.brandScope == 'CATEGORY')
            _stringOptions(
              'Industry category',
              unit.industryCategory,
              const ['FURNITURE', 'FOOD_PROCESSING', 'HEALTHCARE', 'ELECTRONICS', 'CONSTRUCTION'],
              (v) => unit.industryCategory = v,
            ),
        ];
      case 'MINING':
        return [_itemDropdown(unit, includeResources: true, includeProducts: false)];
      case 'STORAGE':
        return [Text('Storage is universal — holds whatever inventory routes into it.', style: theme.textTheme.bodySmall)];
      default:
        return [Text('This unit type has no per-unit fields — see the Power Plant control panel.', style: theme.textTheme.bodySmall)];
    }
  }

  Widget _itemDropdown(EditableGridUnit unit, {required bool includeResources, required bool includeProducts}) {
    final options = <(String value, String label, bool isResource)>[
      if (includeResources) for (final entry in widget.resourceNames.entries) (entry.key, entry.value, true),
      if (includeProducts) for (final entry in widget.productNames.entries) (entry.key, entry.value, false),
    ];
    final currentValue = unit.resourceTypeId ?? unit.productTypeId;
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: DropdownButtonFormField<String>(
        key: const ValueKey('unit-item-dropdown'),
        initialValue: currentValue,
        decoration: const InputDecoration(labelText: 'Item'),
        items: [
          const DropdownMenuItem(value: null, child: Text('None')),
          for (final option in options) DropdownMenuItem(value: option.$1, child: Text(option.$2)),
        ],
        onChanged: (value) {
          (String value, String label, bool isResource)? selected;
          for (final option in options) {
            if (option.$1 == value) {
              selected = option;
              break;
            }
          }
          unit.resourceTypeId = selected != null && selected.$3 ? value : null;
          unit.productTypeId = selected != null && !selected.$3 ? value : null;
          _notify();
        },
      ),
    );
  }

  Widget _numberField(String label, double? value, void Function(double?) onSet) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: TextFormField(
        key: ValueKey('unit-field-$label'),
        initialValue: value?.toString() ?? '',
        decoration: InputDecoration(labelText: label),
        keyboardType: const TextInputType.numberWithOptions(decimal: true),
        onChanged: (text) {
          onSet(text.isEmpty ? null : double.tryParse(text));
          _notify();
        },
      ),
    );
  }

  Widget _textField(String label, String? value, void Function(String?) onSet) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: TextFormField(
        key: ValueKey('unit-field-$label'),
        initialValue: value ?? '',
        decoration: InputDecoration(labelText: label),
        onChanged: (text) {
          onSet(text.isEmpty ? null : text);
          _notify();
        },
      ),
    );
  }

  Widget _stringOptions(String label, String? value, List<String> options, void Function(String?) onSet) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: DropdownButtonFormField<String>(
        key: ValueKey('unit-field-$label'),
        initialValue: value,
        decoration: InputDecoration(labelText: label),
        items: [
          const DropdownMenuItem(value: null, child: Text('None')),
          for (final option in options) DropdownMenuItem(value: option, child: Text(option)),
        ],
        onChanged: (v) {
          onSet(v);
          _notify();
        },
      ),
    );
  }
}
