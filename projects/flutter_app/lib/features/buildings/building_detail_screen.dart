// Heavily trimmed port of `projects/frontend/src/views/BuildingDetailView.vue`
// (via `useBuildingDetail.ts`, ~5600 lines on the web — a full-fidelity port
// is out of scope for a first mobile pass). This screen covers:
// building overview (name, type, level, power status, occupancy), a
// read-only unit list with resource/product names resolved from the
// encyclopedia catalog, pending-configuration status, and exactly two quick
// actions (`scheduleUnitUpgrade`, `updatePublicSalesPrice` for PUBLIC_SALES
// units) — both real mutations, not mocked.
//
// Explicitly NOT ported (documented, not oversights — see
// `.github/copilot-instructions.md` → Flutter mobile app for the full list
// this was scoped against):
// - The drag/link-cycle grid unit editor and `storeBuildingConfiguration` /
//   `cancelBuildingConfiguration` (adding/removing/reconfiguring units) —
//   this is the web's core desktop UX and needs a genuinely different touch
//   interaction design, not a 1:1 port.
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

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import 'building_detail_models.dart';
import 'building_detail_service.dart';

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

  bool _loading = true;
  String? _error;
  BuildingDetail? _building;
  Map<String, String> _resourceNames = const {};
  Map<String, String> _productNames = const {};
  final Set<String> _actionLoadingIds = {};

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedBuildingDetailService ?? BuildingDetailService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([_service.fetchBuilding(widget.buildingId), _service.fetchCatalogNames()]);
      if (!mounted) return;
      final building = results[0] as BuildingDetail?;
      final catalog = results[1] as (Map<String, String>, Map<String, String>);
      setState(() {
        _building = building;
        _resourceNames = catalog.$1;
        _productNames = catalog.$2;
        _loading = false;
      });
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
    final controller = TextEditingController(text: unit.minPrice?.toStringAsFixed(2) ?? '');
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Update sale price'),
        content: TextField(
          controller: controller,
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
      await _service.updatePublicSalesPrice(unitId: unit.id, newMinPrice: double.tryParse(controller.text) ?? 0);
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

    final building = _building;
    if (building == null) {
      return const Center(child: Text('Building not found.'));
    }

    final theme = Theme.of(context);
    final pending = building.pendingConfiguration;

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
                  ],
                ),
              ),
            ),
          ],
          const SizedBox(height: 16),
          Text('Units', style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          if (building.units.isEmpty)
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
