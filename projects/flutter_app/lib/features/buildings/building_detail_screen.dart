// Port of `projects/frontend/src/views/BuildingDetailView.vue` (via
// `useBuildingDetail.ts`, ~5600 lines on the web). Covers: building
// overview, the read-only unit list (non-grid building types), the full
// grid editor for grid-eligible types (view + edit mode, placing/removing
// units, 8-directional links, per-unit config, upgrade preview, inventory
// visualization, clipboard, draft-change summary, starter layouts, and
// chain-completeness warnings/status — see `building_grid_draft_controller.dart`
// and its sibling files for exactly what each ROADMAP item covers and any
// documented trims), and the two original quick-action mutations
// (`scheduleUnitUpgrade`, `updatePublicSalesPrice`) for non-grid types.
//
// Explicitly NOT ported (documented, not oversights — see
// `.github/copilot-instructions.md` → Flutter mobile app for the full list
// this was scoped against):
// - Media house, power plant, and rent-scheduling panels and their
//   mutations (`setMediaHouseContentBudget`, `upgradeMediaHouse`,
//   `configureMediaHouseUnit`, `setRentPerSqm`, `setPlantDispatch`,
//   `setPowerPriority`, `listEnergyForSale`, `cancelEnergyListing`,
//   `setMaxEnergyBidPrice`).
// - `flushStorage`, `setPublicSalesInventoryAlertThreshold`,
//   `removeDestroyedBuilding`.
// - All analytics/history panels (`mediaHouseAnalytics`, `publicSalesAnalytics`,
//   `buildingUnitResourceHistories`, `buildingFinancialTimeline`,
//   `powerPlantAnalytics`, `unitProductAnalytics`, `buildingRecentActivity`,
//   `buildingSupplyChain`, market-event banners, tutorial overlays).
// - Global Exchange sourcing/vendor-selector flow for PURCHASE units.
//
// Selling/destroying a building lives on its own screen (`/building/:id/sell`
// — `SellBuildingScreen`), linked from here rather than duplicated.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/context/recent_building_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import '../../core/theme/app_spacing.dart';
import 'building_chain_status_panel.dart';
import 'building_chain_validation.dart';
import 'building_detail_models.dart';
import 'building_detail_service.dart';
import 'building_draft_summary_panel.dart';
import 'building_grid_draft_controller.dart';
import 'building_grid_editor.dart';
import 'building_grid_models.dart';
import 'building_starter_layout_banner.dart';
import 'building_unit_clipboard.dart';
import 'building_unit_config_sheet.dart';
import 'building_unit_grid.dart';
import 'building_unit_picker_sheet.dart';

/// Mirrors `isMultiUnitBuilding` in `BuildingDetailView.vue` — these three
/// building types manage a single implicit unit via their own dedicated
/// panels rather than the 4x4 grid, on both web and here.
bool _isMultiUnitBuildingType(String type) => type != 'APARTMENT' && type != 'COMMERCIAL' && type != 'MEDIA_HOUSE';

class BuildingDetailScreen extends StatefulWidget {
  const BuildingDetailScreen({
    super.key,
    required this.buildingId,
    GraphQlService? graphQlService,
    BuildingDetailService? buildingDetailService,
  }) : _injectedGraphQlService = graphQlService,
       _injectedBuildingDetailService = buildingDetailService;

  final String buildingId;
  final GraphQlService? _injectedGraphQlService;
  final BuildingDetailService? _injectedBuildingDetailService;

  @override
  State<BuildingDetailScreen> createState() => _BuildingDetailScreenState();
}

class _BuildingDetailScreenState extends State<BuildingDetailScreen> {
  late final BuildingDetailService _service;
  final BuildingGridDraftController _controller = BuildingGridDraftController();

  bool _loading = true;
  String? _error;
  Map<String, String> _resourceNames = const {};
  Map<String, String> _productNames = const {};
  final Set<String> _actionLoadingIds = {};

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedBuildingDetailService ?? BuildingDetailService(graphQlService);
    // Deferred past this build: recordVisit's notifyListeners() would
    // otherwise fire synchronously during this widget's own initState/mount,
    // which trips "setState() called during build" for the RecentBuildingState
    // provider scope above it in the tree.
    final recentBuildingState = context.read<RecentBuildingState>();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      unawaited(recentBuildingState.recordVisit(widget.buildingId));
    });
    _controller.addListener(_onControllerChanged);
    _load();
  }

  @override
  void dispose() {
    _controller.removeListener(_onControllerChanged);
    super.dispose();
  }

  void _onControllerChanged() {
    if (mounted) setState(() {});
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final building = await _service.fetchBuilding(widget.buildingId);
      final catalogFuture = _service.fetchCatalog();
      final fxFuture = _service.fetchCityFxRate(building?.cityId);
      final cashFuture = building == null ? Future.value(null) : _service.fetchCompanyCash(building.companyId);
      final results = await Future.wait([catalogFuture, fxFuture, cashFuture]);
      if (!mounted) return;
      final catalog = results[0] as BuildingCatalog;
      final fxRate = results[1] as double;
      final cash = results[2] as double?;
      setState(() {
        _resourceNames = catalog.resourceNames;
        _productNames = catalog.productNames;
        _loading = false;
      });
      if (building != null) {
        _controller.loadFrom(building.withCityFxRate(fxRate), catalog: catalog, companyCash: cash);
      } else {
        _controller.building = null;
      }
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load this building. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _upgradeUnit(BuildingUnitDetail unit) async {
    setState(() => _actionLoadingIds.add(unit.id));
    try {
      await _service.scheduleUnitUpgrade(unit.id);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not schedule the upgrade.')));
      }
    } finally {
      if (mounted) setState(() => _actionLoadingIds.remove(unit.id));
    }
  }

  Future<void> _updatePrice(BuildingUnitDetail unit) async {
    final priceController = TextEditingController(text: unit.minPrice?.toStringAsFixed(2) ?? '');
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Update sale price'),
        content: TextField(
          controller: priceController,
          decoration: const InputDecoration(labelText: 'New minimum price'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Save')),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _actionLoadingIds.add(unit.id));
    try {
      await _service.updatePublicSalesPrice(unitId: unit.id, newMinPrice: double.tryParse(priceController.text) ?? 0);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not update the price.')));
      }
    } finally {
      if (mounted) setState(() => _actionLoadingIds.remove(unit.id));
    }
  }

  String _itemNameFor(BuildingUnitDetail unit) {
    if (unit.resourceTypeId != null) return _resourceNames[unit.resourceTypeId] ?? 'Unknown resource';
    if (unit.productTypeId != null) return _productNames[unit.productTypeId] ?? 'Unknown product';
    return '';
  }

  String _itemNameForIds(String? resourceTypeId, String? productTypeId) {
    if (resourceTypeId != null) return _resourceNames[resourceTypeId] ?? 'Unknown resource';
    if (productTypeId != null) return _productNames[productTypeId] ?? 'Unknown product';
    return '';
  }

  void _openUnitPicker() {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (_) => UnitPickerSheet(
        allowedUnitTypes: _controller.allowedUnitTypes,
        cityFxRate: _controller.cityFxRate,
        onSelect: (unitType) {
          _controller.placeUnit(unitType);
          Navigator.of(context).pop();
        },
      ),
    );
  }

  void _openUnitConfigSheet(int x, int y) {
    final unit = _controller.draftUnitAt(x, y);
    if (unit == null) return;
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (_) => UnitConfigSheet(
        controller: _controller,
        service: _service,
        unit: unit,
        resourceNames: _resourceNames,
        productNames: _productNames,
        onChanged: () => _controller.notify(),
        onRemove: () => _controller.removeDraftUnit(x, y),
      ),
    );
  }

  void _onCellTap(int x, int y) {
    if (_controller.showUnitPicker) {
      _openUnitPicker();
    } else {
      _openUnitConfigSheet(x, y);
    }
  }

  Future<void> _save() async {
    final ok = await _controller.storeConfiguration(_service);
    if (ok) await _load();
  }

  Future<void> _cancelPlan() async {
    final ok = await _controller.cancelPlan(_service);
    if (ok) await _load();
    if (!ok && _controller.cancelPlanError != null && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(_controller.cancelPlanError!)));
    }
  }

  Future<void> _copyUnit() async {
    await _controller.copySelectedUnit();
    if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Unit config copied.')));
  }

  Future<void> _pasteUnit() async {
    final error = await _controller.pasteToSelectedUnit();
    if (!mounted) return;
    if (error != null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(_clipboardErrorMessage(error))));
    } else {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Unit config pasted.')));
    }
  }

  String _clipboardErrorMessage(ClipboardParseError error) {
    switch (error) {
      case ClipboardParseError.empty:
        return 'Clipboard is empty.';
      case ClipboardParseError.invalidJson:
        return 'Clipboard does not contain a copied unit config.';
      case ClipboardParseError.schemaMismatch:
        return 'This clipboard content is from an incompatible app version.';
      case ClipboardParseError.incompatibleType:
        return 'That unit config is not compatible with this cell.';
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _load, child: const Text('Try again')),
            ],
          ),
        ),
      );
    }

    final building = _controller.building;
    if (building == null) {
      return const Center(child: Text('Building not found.'));
    }

    final theme = Theme.of(context);
    final pending = building.pendingConfiguration;
    final isGridBuilding = _isMultiUnitBuildingType(building.type);
    final isEditing = _controller.isEditing;
    final showStarterBanner =
        (building.type == 'FACTORY' || building.type == 'SALES_SHOP') &&
        building.units.isEmpty &&
        pending == null &&
        !isEditing;
    final validationUnits = isEditing ? _controller.draftUnits : (pending != null ? _controller.pendingUnits : _controller.activeUnits);
    final configWarnings = isGridBuilding ? getConfigWarnings(building.type, validationUnits) : const <ChainWarning>[];

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Row(
            children: [
              Expanded(child: Text(building.name, style: theme.textTheme.headlineSmall)),
              if (building.isForSale) const Chip(label: Text('For sale')),
            ],
          ),
          Text('${building.type} · Level ${building.level}', style: theme.textTheme.bodyMedium),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            children: [
              if (building.powerStatus != null) Chip(label: Text(building.powerStatus!)),
              if (building.occupancyPercent != null) Chip(label: Text('${building.occupancyPercent!.toStringAsFixed(0)}% occupied')),
            ],
          ),
          if (pending != null) ...[
            const SizedBox(height: 12),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Configuration in progress', style: theme.textTheme.titleSmall),
                    Text('Applies at tick ${pending.appliesAtTick} (${pending.totalTicksRequired} ticks total)'),
                    if (pending.blockReason != null) Text('Blocked: ${pending.blockReason}'),
                    if (isGridBuilding && !isEditing) ...[
                      const SizedBox(height: 8),
                      OutlinedButton(
                        onPressed: _controller.cancellingPlan ? null : _cancelPlan,
                        child: _controller.cancellingPlan
                            ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2))
                            : const Text('Cancel pending plan'),
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ],
          if (isGridBuilding && configWarnings.isNotEmpty) ...[
            const SizedBox(height: 12),
            ConfigWarningsBanner(warnings: configWarnings),
          ],
          if (showStarterBanner) ...[
            const SizedBox(height: 12),
            StarterLayoutBanner(
              isShop: building.type == 'SALES_SHOP',
              onApply: building.type == 'SALES_SHOP' ? _controller.applyShopStarterLayout : _controller.applyStarterLayout,
            ),
          ],
          if (isGridBuilding && !isEditing && !showStarterBanner && building.type == 'FACTORY')
            ProductionChainStatusPanel(units: validationUnits),
          if (isGridBuilding && !isEditing && !showStarterBanner && building.type == 'SALES_SHOP')
            ShopChainStatusPanel(units: validationUnits),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(child: Text('Units', style: theme.textTheme.titleMedium)),
              if (isGridBuilding && !isEditing)
                TextButton(onPressed: _controller.startEditing, child: const Text('Edit Building')),
            ],
          ),
          const SizedBox(height: 8),
          if (isGridBuilding && isEditing)
            BuildingGridEditor(controller: _controller, itemNameFor: _itemNameForIds, onCellTap: _onCellTap)
          else if (isGridBuilding)
            BuildingUnitGrid(
              units: building.units,
              itemNameFor: _itemNameFor,
              actionLoadingIds: _actionLoadingIds,
              onUpgrade: (unit) => _upgradeUnit(unit),
              onUpdatePrice: (unit) => _updatePrice(unit),
            )
          else if (building.units.isEmpty)
            const Text('No units configured yet.')
          else
            for (final unit in building.units)
              Card(
                key: ValueKey('unit-${unit.id}'),
                margin: const EdgeInsets.only(bottom: 8),
                child: ListTile(
                  title: Text('${unit.unitType} · Level ${unit.level}'),
                  subtitle: Text(_itemNameFor(unit).isEmpty ? '—' : _itemNameFor(unit)),
                  trailing: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (unit.unitType == 'PUBLIC_SALES')
                        IconButton(
                          icon: const FaIcon(AppIcons.sell, size: 16),
                          tooltip: 'Update price',
                          onPressed: _actionLoadingIds.contains(unit.id) ? null : () => _updatePrice(unit),
                        ),
                      IconButton(
                        icon: const FaIcon(AppIcons.upgrade, size: 16),
                        tooltip: 'Upgrade',
                        onPressed: _actionLoadingIds.contains(unit.id) ? null : () => _upgradeUnit(unit),
                      ),
                    ],
                  ),
                ),
              ),
          if (isEditing) ...[
            const SizedBox(height: AppSpacing.md),
            BuildingDraftSummaryPanel(
              controller: _controller,
              onSave: _save,
              onCancel: _controller.cancelEditing,
              onCopy: _copyUnit,
              onPaste: _pasteUnit,
            ),
          ],
          const SizedBox(height: 20),
          OutlinedButton(
            onPressed: () => context.go('/building/${building.id}/sell'),
            child: const Text('Sell or destroy this building'),
          ),
        ],
      ),
    );
  }
}
