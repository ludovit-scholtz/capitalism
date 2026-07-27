// Selected-unit tabs, edit mode — port of `UnitConfigurationTabView.vue`.
// Shown for a selected grid cell while the building IS in edit mode. Tabs:
// - Config — unit-type field editor (per-unit config fields), link summary,
//   remove-unit action.
// - Energy — building-wide power priority/max-bid controls
//   (`BuildingEnergySettingsPanel`), shown with this unit's label for
//   context — matches web reusing the same `BuildingEnergySettingsTab` at
//   both the outer building-edit level and here.
// - Performance — inventory load/quality/table for this unit.
// - Maintenance — upgrade preview + stage/confirm upgrade flow.
//
// Field set per unit type verified against `BuildingUnitConfigFields.vue`;
// same documented trims as the previous single-sheet `UnitConfigSheet`:
// plain catalog dropdowns rather than reachability/ranked-product filtering,
// no vendor-lock company picker or media-house selector. Also trimmed here
// (documented, not an oversight): web's Config tab additionally shows an
// Exchange sourcing-offers panel and PURCHASE price/quality history inline;
// this Config tab omits both — the equivalent Exchange/sourcing content is
// still reachable read-only via the view-mode Market Intelligence tab
// (`building_unit_view_tabs.dart`) once the unit is saved.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_detail_service.dart';
import 'building_energy_settings_panel.dart';
import 'building_grid_draft_controller.dart';
import 'building_grid_models.dart';
import 'building_inventory_fill_bar.dart';
import 'building_panel_service.dart';
import 'building_tab_strip.dart';
import 'building_unit_grid.dart' show unitTypeShortLabel;

/// Self-fetching wrapper around [BuildingUnitEditTabs] — loads this unit's
/// upgrade preview + inventory summary (edit-mode-only data) once per
/// selected unit, then renders the tabs. Used both inline in the desktop
/// right column and inside `UnitConfigSheet`'s mobile bottom sheet, so the
/// fetch only lives in one place.
class BuildingUnitEditPane extends StatefulWidget {
  const BuildingUnitEditPane({
    super.key,
    required this.controller,
    required this.service,
    required this.panelService,
    required this.unit,
    required this.resourceNames,
    required this.productNames,
    required this.onChanged,
    required this.onRemove,
    this.onClose,
  });

  final BuildingGridDraftController controller;
  final BuildingDetailService service;
  final BuildingPanelService panelService;
  final EditableGridUnit unit;
  final Map<String, String> resourceNames;
  final Map<String, String> productNames;
  final VoidCallback onChanged;
  final VoidCallback onRemove;
  final VoidCallback? onClose;

  @override
  State<BuildingUnitEditPane> createState() => _BuildingUnitEditPaneState();
}

class _BuildingUnitEditPaneState extends State<BuildingUnitEditPane> {
  UnitUpgradeInfo? _upgradeInfo;
  BuildingUnitInventorySummary? _inventory;
  bool _loading = true;

  bool get _isPersistedUnit => !widget.unit.id.startsWith('draft-');

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didUpdateWidget(covariant BuildingUnitEditPane oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.unit.id != widget.unit.id) _load();
  }

  Future<void> _load() async {
    if (!_isPersistedUnit) {
      setState(() {
        _upgradeInfo = null;
        _inventory = null;
        _loading = false;
      });
      return;
    }
    setState(() => _loading = true);
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
        _loading = false;
      });
    } catch (_) {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (_loading) const Padding(padding: EdgeInsets.symmetric(vertical: AppSpacing.sm), child: LinearProgressIndicator()),
        BuildingUnitEditTabs(
          controller: widget.controller,
          unit: widget.unit,
          resourceNames: widget.resourceNames,
          productNames: widget.productNames,
          panelService: widget.panelService,
          upgradeInfo: _upgradeInfo,
          inventory: _inventory,
          onChanged: widget.onChanged,
          onRemove: widget.onRemove,
          onClose: widget.onClose,
        ),
      ],
    );
  }
}

class BuildingUnitEditTabs extends StatefulWidget {
  const BuildingUnitEditTabs({
    super.key,
    required this.controller,
    required this.unit,
    required this.resourceNames,
    required this.productNames,
    required this.panelService,
    required this.upgradeInfo,
    required this.inventory,
    required this.onChanged,
    required this.onRemove,
    this.onClose,
  });

  final BuildingGridDraftController controller;
  final EditableGridUnit unit;
  final Map<String, String> resourceNames;
  final Map<String, String> productNames;
  final BuildingPanelService panelService;
  final UnitUpgradeInfo? upgradeInfo;
  final BuildingUnitInventorySummary? inventory;
  final VoidCallback onChanged;
  final VoidCallback onRemove;
  final VoidCallback? onClose;

  @override
  State<BuildingUnitEditTabs> createState() => _BuildingUnitEditTabsState();
}

class _BuildingUnitEditTabsState extends State<BuildingUnitEditTabs> {
  void _notify() {
    widget.onChanged();
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final unit = widget.unit;
    final tabs = <BuildingTab>[
      BuildingTab(key: 'config', label: 'Config', builder: (context) => _configTab(context)),
      BuildingTab(key: 'energy', label: 'Energy', builder: (context) => _energyTab(context)),
      BuildingTab(key: 'performance', label: 'Performance', builder: (context) => _performanceTab(context)),
      BuildingTab(key: 'maintenance', label: 'Maintenance', builder: (context) => _maintenanceTab(context)),
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
        Text('Position (${unit.gridX}, ${unit.gridY})', style: Theme.of(context).textTheme.bodySmall),
        const SizedBox(height: AppSpacing.sm),
        BuildingTabStrip(key: ValueKey('unit-edit-tabs-${unit.id}'), tabs: tabs),
      ],
    );
  }

  Widget _configTab(BuildContext context) {
    final unit = widget.unit;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        ..._configFieldsFor(unit),
        const SizedBox(height: AppSpacing.md),
        OutlinedButton.icon(
          icon: const Icon(Icons.delete_outline, size: 18),
          label: const Text('Remove unit'),
          onPressed: () {
            widget.onRemove();
            Navigator.maybeOf(context)?.maybePop();
          },
        ),
      ],
    );
  }

  Widget _energyTab(BuildContext context) {
    final building = widget.controller.building;
    if (building == null) return const SizedBox.shrink();
    return BuildingEnergySettingsPanel(
      buildingId: building.id,
      buildingType: building.type,
      currentPriority: building.powerPriority,
      currentMaxBidPrice: building.maxEnergyBidPrice,
      panelService: widget.panelService,
      selectedUnitLabel: unitTypeShortLabel(widget.unit.unitType),
    );
  }

  Widget _performanceTab(BuildContext context) {
    final inventory = widget.inventory;
    if (inventory == null) return const Text('No inventory recorded for this unit yet.');
    return InventoryFillBar(summary: inventory, isPublicSales: widget.unit.unitType == 'PUBLIC_SALES');
  }

  Widget _maintenanceTab(BuildContext context) {
    final theme = Theme.of(context);
    final info = widget.upgradeInfo;
    if (info == null) return const Text('Upgrade information is not available for this unit.');

    final staged = widget.controller.draftUpgradeUnitIds.contains(widget.unit.id);
    if (info.isMaxLevel) return const Text('Max Level — this unit is fully upgraded.');
    if (!info.isUpgradable) return const Text('This unit type cannot be upgraded.');

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Level ${info.currentLevel} → Level ${info.nextLevel}', style: theme.textTheme.titleSmall),
            const SizedBox(height: 6),
            Text('${info.statLabel}: ${info.currentStat.toStringAsFixed(1)} → ${info.nextStat.toStringAsFixed(1)}'),
            Text('Storage capacity: ${info.currentStorageCapacity.toStringAsFixed(0)} → ${info.nextStorageCapacity.toStringAsFixed(0)}'),
            Text('Labor cost/tick: ${info.currentLaborCostPerTick.toStringAsFixed(2)} → ${info.nextLaborCostPerTick.toStringAsFixed(2)}'),
            Text('Energy cost/tick: ${info.currentEnergyCostPerTick.toStringAsFixed(2)} → ${info.nextEnergyCostPerTick.toStringAsFixed(2)}'),
            const SizedBox(height: 6),
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
