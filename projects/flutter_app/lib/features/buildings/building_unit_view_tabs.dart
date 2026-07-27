// Selected-unit tabs, view mode — port of `BuildingReadonlySidebar.vue` /
// `unitDetailTabs` in `useBuildingDetail.ts`. Shown for a selected grid cell
// while the building is NOT in edit mode. Tabs (type-gated, matching web):
// - Basic Info (always) — unit type/level/position/links/config summary.
// - Quick Actions (`PUBLIC_SALES` only) — price update.
// - Inventory (always) — fill/quantity/quality + flush-storage action.
// - History (always) — resource movement history.
// - Market Intelligence (`PURCHASE`/`PUBLIC_SALES`/`MANUFACTURING`) —
//   sourcing/procurement or public-sales analytics panels.
//
// Used both inline in the desktop right column and inside the mobile
// bottom sheet (`UnitConfigSheet`) via `BuildingTabStrip`, so the tab
// structure is identical on every screen size — only the container differs.
// No upgrade panel here: matching web, the upgrade preview/stage/confirm
// flow lives only in edit mode (`BuildingUnitEditTabs`'s Maintenance tab).

import 'dart:async';

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import '../exchange/forex_models.dart' show MarketEvent;
import 'building_analytics_models.dart';
import 'building_analytics_service.dart';
import 'building_detail_models.dart';
import 'building_detail_service.dart';
import 'building_grid_models.dart';
import 'building_inventory_fill_bar.dart';
import 'building_public_sales_panel.dart';
import 'building_sales_models.dart';
import 'building_sales_service.dart';
import 'building_sourcing_models.dart';
import 'building_sourcing_panel.dart';
import 'building_sourcing_service.dart';
import 'building_tab_strip.dart';
import 'building_unit_grid.dart' show unitTypeColors, unitTypeShortLabel;
import 'building_unit_history_panel.dart';

const _marketIntelligenceUnitTypes = {'PURCHASE', 'PUBLIC_SALES', 'MANUFACTURING'};

class BuildingUnitViewTabs extends StatefulWidget {
  const BuildingUnitViewTabs({
    super.key,
    required this.unit,
    required this.buildingId,
    required this.cityId,
    required this.itemNameFor,
    required this.unitResourceHistories,
    required this.service,
    required this.salesService,
    required this.sourcingService,
    required this.analyticsService,
    required this.onUpdatePrice,
    required this.isPriceUpdating,
    this.onClose,
  });

  final BuildingUnitDetail unit;
  final String buildingId;
  final String? cityId;
  final String Function(BuildingUnitDetail unit) itemNameFor;
  final List<UnitResourceHistoryPoint> unitResourceHistories;
  final BuildingDetailService service;
  final BuildingSalesService salesService;
  final BuildingSourcingService sourcingService;
  final BuildingAnalyticsService analyticsService;
  final void Function(BuildingUnitDetail unit) onUpdatePrice;
  final bool isPriceUpdating;
  final VoidCallback? onClose;

  @override
  State<BuildingUnitViewTabs> createState() => _BuildingUnitViewTabsState();
}

class _BuildingUnitViewTabsState extends State<BuildingUnitViewTabs> {
  BuildingUnitInventorySummary? _inventory;
  bool _loadingInventory = true;

  PublicSalesAnalytics? _publicSalesAnalytics;
  List<MarketEvent> _marketEvents = const [];
  bool _salesLoading = false;

  ProcurementPreview? _procurementPreview;
  List<SourcingCandidate> _sourcingCandidates = const [];
  bool _sourcingLoading = false;

  UnitProductAnalytics? _productAnalytics;
  bool _productAnalyticsLoading = false;

  bool _flushing = false;
  static const _flushableUnitTypes = {'STORAGE', 'MINING', 'MANUFACTURING'};

  @override
  void initState() {
    super.initState();
    _loadInventory();
    unawaited(_loadTypeSpecificExtras());
  }

  @override
  void didUpdateWidget(covariant BuildingUnitViewTabs oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.unit.id != widget.unit.id) {
      _loadInventory();
      unawaited(_loadTypeSpecificExtras());
    }
  }

  Future<void> _loadInventory() async {
    setState(() => _loadingInventory = true);
    try {
      final summaries = await widget.service.fetchInventorySummaries(widget.buildingId);
      if (!mounted) return;
      BuildingUnitInventorySummary? matched;
      for (final s in summaries) {
        if (s.buildingUnitId == widget.unit.id) {
          matched = s;
          break;
        }
      }
      setState(() {
        _inventory = matched;
        _loadingInventory = false;
      });
    } catch (_) {
      if (mounted) setState(() => _loadingInventory = false);
    }
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
  }

  Future<void> _flushStorage() async {
    setState(() => _flushing = true);
    try {
      final result = await widget.salesService.flushStorage(widget.unit.id);
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text('Discarded ${result.discardedItemCount} item(s) worth ${result.totalDiscardedValue.toStringAsFixed(0)}.')));
      }
      await _loadInventory();
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not flush storage. Please try again.')));
    } finally {
      if (mounted) setState(() => _flushing = false);
    }
  }

  Future<void> _confirmFlush() async {
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
    if (confirmed == true) await _flushStorage();
  }

  @override
  Widget build(BuildContext context) {
    final unit = widget.unit;
    final tabs = <BuildingTab>[
      BuildingTab(key: 'basicInfo', label: 'Basic Info', builder: (context) => _basicInfoTab(context)),
      if (unit.unitType == 'PUBLIC_SALES') BuildingTab(key: 'quickActions', label: 'Quick Actions', builder: (context) => _quickActionsTab(context)),
      BuildingTab(key: 'inventory', label: 'Inventory', builder: (context) => _inventoryTab(context)),
      BuildingTab(key: 'history', label: 'History', builder: (context) => _historyTab(context)),
      if (_marketIntelligenceUnitTypes.contains(unit.unitType))
        BuildingTab(key: 'marketIntelligence', label: 'Market Intelligence', builder: (context) => _marketIntelligenceTab(context)),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text('${unit.unitType} · Level ${unit.level}', style: Theme.of(context).textTheme.titleMedium)),
            if (widget.onClose != null) IconButton(icon: const Icon(Icons.close), onPressed: widget.onClose),
          ],
        ),
        const SizedBox(height: AppSpacing.sm),
        BuildingTabStrip(key: ValueKey('unit-view-tabs-${unit.id}'), tabs: tabs),
      ],
    );
  }

  Widget _basicInfoTab(BuildContext context) {
    final theme = Theme.of(context);
    final unit = widget.unit;
    final accent = unitTypeColors[unit.unitType] ?? theme.colorScheme.primary;
    final itemName = widget.itemNameFor(unit);
    final links = <String>[
      if (unit.linkUp) 'Up',
      if (unit.linkDown) 'Down',
      if (unit.linkLeft) 'Left',
      if (unit.linkRight) 'Right',
      if (unit.linkUpLeft) 'Up-Left',
      if (unit.linkUpRight) 'Up-Right',
      if (unit.linkDownLeft) 'Down-Left',
      if (unit.linkDownRight) 'Down-Right',
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            CircleAvatar(
              radius: 12,
              backgroundColor: accent.withValues(alpha: 0.2),
              child: Text(unitTypeShortLabel(unit.unitType).characters.first.toUpperCase(), style: TextStyle(color: accent, fontWeight: FontWeight.bold)),
            ),
            const SizedBox(width: AppSpacing.sm),
            Expanded(child: Text(unitTypeShortLabel(unit.unitType), style: theme.textTheme.titleSmall)),
          ],
        ),
        const SizedBox(height: AppSpacing.sm),
        Wrap(
          spacing: 12,
          runSpacing: 4,
          children: [
            Text('Level ${unit.level}'),
            Text('Position (${unit.gridX}, ${unit.gridY})'),
            if (itemName.isNotEmpty) Text(itemName),
            if (unit.minPrice != null) Text('Min price: ${unit.minPrice!.toStringAsFixed(2)}'),
            if (unit.maxPrice != null) Text('Max price: ${unit.maxPrice!.toStringAsFixed(2)}'),
            if (unit.purchaseSource != null) Text('Purchase source: ${unit.purchaseSource}'),
            if (unit.saleVisibility != null) Text('Sale visibility: ${unit.saleVisibility}'),
            if (unit.budget != null) Text('Budget: ${unit.budget!.toStringAsFixed(2)}'),
            if (unit.brandScope != null) Text('Brand scope: ${unit.brandScope}'),
          ],
        ),
        if (links.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.sm),
          Text('Links: ${links.join(', ')}', style: theme.textTheme.bodySmall),
        ],
      ],
    );
  }

  Widget _quickActionsTab(BuildContext context) {
    final unit = widget.unit;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Sale price', style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 4),
            Text(unit.minPrice != null ? 'Current: ${unit.minPrice!.toStringAsFixed(2)}' : 'No price configured yet.'),
            const SizedBox(height: AppSpacing.sm),
            FilledButton(
              onPressed: widget.isPriceUpdating ? null : () => widget.onUpdatePrice(unit),
              child: widget.isPriceUpdating ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Text('Update price'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _inventoryTab(BuildContext context) {
    if (_loadingInventory) return const Padding(padding: EdgeInsets.all(AppSpacing.md), child: LinearProgressIndicator());
    final inventory = _inventory;
    if (inventory == null) return const Text('No inventory recorded for this unit.');

    final unit = widget.unit;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        InventoryFillBar(summary: inventory, isPublicSales: unit.unitType == 'PUBLIC_SALES'),
        if (_flushableUnitTypes.contains(unit.unitType)) ...[
          const SizedBox(height: AppSpacing.md),
          OutlinedButton.icon(
            icon: _flushing ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.delete_sweep_outlined, size: 18),
            label: const Text('Discard All Inventory'),
            onPressed: _flushing || inventory.quantity == 0 ? null : _confirmFlush,
          ),
        ],
        if (unit.unitType == 'PUBLIC_SALES') ...[
          const SizedBox(height: AppSpacing.md),
          _LowInventoryThresholdField(currentThreshold: unit.lowInventoryAlertThreshold, onSave: _saveInventoryThreshold),
        ],
      ],
    );
  }

  Widget _historyTab(BuildContext context) {
    final history = widget.unitResourceHistories.where((p) => p.buildingUnitId == widget.unit.id).toList();
    if (history.isEmpty) return const Text('No movement history recorded yet.');
    return UnitResourceHistoryPanel(history: history);
  }

  Widget _marketIntelligenceTab(BuildContext context) {
    final unit = widget.unit;
    if (unit.unitType == 'PUBLIC_SALES') {
      return PublicSalesToolsPanel(
        analytics: _publicSalesAnalytics,
        analyticsLoading: _salesLoading,
        marketEvents: _marketEvents,
        currentThreshold: unit.lowInventoryAlertThreshold,
        onSaveThreshold: _saveInventoryThreshold,
        onFlushStorage: _flushStorage,
      );
    }
    if (unit.unitType == 'PURCHASE') {
      return SourcingComparisonPanel(preview: _procurementPreview, candidates: _sourcingCandidates, loading: _sourcingLoading);
    }
    return UnitProductAnalyticsPanel(analytics: _productAnalytics, loading: _productAnalyticsLoading);
  }
}

/// Mobile (narrow-screen) container for [BuildingUnitViewTabs] — the
/// view-mode counterpart to `UnitConfigSheet`'s edit-mode sheet. On wide
/// screens the same [BuildingUnitViewTabs] renders inline in the right
/// column instead.
class BuildingUnitViewSheet extends StatelessWidget {
  const BuildingUnitViewSheet({
    super.key,
    required this.unit,
    required this.buildingId,
    required this.cityId,
    required this.itemNameFor,
    required this.unitResourceHistories,
    required this.service,
    required this.salesService,
    required this.sourcingService,
    required this.analyticsService,
    required this.onUpdatePrice,
    required this.isPriceUpdating,
  });

  final BuildingUnitDetail unit;
  final String buildingId;
  final String? cityId;
  final String Function(BuildingUnitDetail unit) itemNameFor;
  final List<UnitResourceHistoryPoint> unitResourceHistories;
  final BuildingDetailService service;
  final BuildingSalesService salesService;
  final BuildingSourcingService sourcingService;
  final BuildingAnalyticsService analyticsService;
  final void Function(BuildingUnitDetail unit) onUpdatePrice;
  final bool isPriceUpdating;

  @override
  Widget build(BuildContext context) {
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
            BuildingUnitViewTabs(
              unit: unit,
              buildingId: buildingId,
              cityId: cityId,
              itemNameFor: itemNameFor,
              unitResourceHistories: unitResourceHistories,
              service: service,
              salesService: salesService,
              sourcingService: sourcingService,
              analyticsService: analyticsService,
              onUpdatePrice: onUpdatePrice,
              isPriceUpdating: isPriceUpdating,
              onClose: () => Navigator.of(context).maybePop(),
            ),
          ],
        ),
      ),
    );
  }
}

class _LowInventoryThresholdField extends StatefulWidget {
  const _LowInventoryThresholdField({required this.currentThreshold, required this.onSave});

  final double? currentThreshold;
  final Future<void> Function(double? threshold) onSave;

  @override
  State<_LowInventoryThresholdField> createState() => _LowInventoryThresholdFieldState();
}

class _LowInventoryThresholdFieldState extends State<_LowInventoryThresholdField> {
  late final TextEditingController _controller = TextEditingController(text: widget.currentThreshold?.toString() ?? '');
  bool _saving = false;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() => _saving = true);
    try {
      final text = _controller.text.trim();
      await widget.onSave(text.isEmpty ? null : double.tryParse(text));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: TextField(
            controller: _controller,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: const InputDecoration(labelText: 'Low-inventory alert threshold'),
          ),
        ),
        const SizedBox(width: AppSpacing.sm),
        FilledButton(
          onPressed: _saving ? null : _save,
          child: _saving ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Text('Save'),
        ),
      ],
    );
  }
}
