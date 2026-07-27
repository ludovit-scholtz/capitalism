// Port of `BuildingEnergyPanel.vue` — the building-wide power priority
// (grid dispatch order under scarcity, 1-10) and, for non-`POWER_PLANT`
// buildings, an optional max spot-market energy bid price. Reused in two
// places, matching the web: the outer edit-mode "Energy" tab
// (`BuildingEditingTabs`, building-scoped, no unit selected) and the
// per-unit edit-mode "Energy" tab (`BuildingUnitEditTabs`), where it's the
// same building-level control just shown with the selected unit's label for
// context (`BuildingEnergySettingsTab.vue` wraps this same panel both ways).
//
// `POWER_PLANT` dispatch/fuel/spot-listing controls live in the separate
// `BuildingPowerPlantPanel` sibling panel — this widget only ever handles
// priority + max bid, matching the web's `v-if="building?.type !==
// 'POWER_PLANT'"` guard on the max-bid section.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_panel_service.dart';

class BuildingEnergySettingsPanel extends StatefulWidget {
  const BuildingEnergySettingsPanel({
    super.key,
    required this.buildingId,
    required this.buildingType,
    required this.currentPriority,
    required this.currentMaxBidPrice,
    required this.panelService,
    this.selectedUnitLabel,
    this.onSaved,
  });

  final String buildingId;
  final String buildingType;
  final int? currentPriority;
  final double? currentMaxBidPrice;
  final BuildingPanelService panelService;
  final String? selectedUnitLabel;
  final VoidCallback? onSaved;

  @override
  State<BuildingEnergySettingsPanel> createState() => _BuildingEnergySettingsPanelState();
}

class _BuildingEnergySettingsPanelState extends State<BuildingEnergySettingsPanel> {
  late int _draftPriority = widget.currentPriority ?? 5;
  late final TextEditingController _maxBidController = TextEditingController(text: widget.currentMaxBidPrice?.toString() ?? '');
  bool _prioritySaving = false;
  bool _maxBidSaving = false;
  String? _priorityError;
  String? _maxBidError;
  bool _prioritySuccess = false;
  bool _maxBidSuccess = false;

  @override
  void dispose() {
    _maxBidController.dispose();
    super.dispose();
  }

  Future<void> _savePriority() async {
    setState(() {
      _prioritySaving = true;
      _priorityError = null;
      _prioritySuccess = false;
    });
    try {
      await widget.panelService.setPowerPriority(buildingId: widget.buildingId, priority: _draftPriority);
      if (mounted) setState(() => _prioritySuccess = true);
      widget.onSaved?.call();
    } catch (_) {
      if (mounted) setState(() => _priorityError = 'Could not save priority. Please try again.');
    } finally {
      if (mounted) setState(() => _prioritySaving = false);
    }
  }

  Future<void> _saveMaxBid() async {
    final text = _maxBidController.text.trim();
    final parsed = text.isEmpty ? null : double.tryParse(text);
    if (text.isNotEmpty && parsed == null) {
      setState(() => _maxBidError = 'Enter a valid number.');
      return;
    }
    setState(() {
      _maxBidSaving = true;
      _maxBidError = null;
      _maxBidSuccess = false;
    });
    try {
      await widget.panelService.setMaxEnergyBidPrice(buildingId: widget.buildingId, maxBidPricePerKwh: parsed);
      if (mounted) setState(() => _maxBidSuccess = true);
      widget.onSaved?.call();
    } catch (_) {
      if (mounted) setState(() => _maxBidError = 'Could not save max bid price. Please try again.');
    } finally {
      if (mounted) setState(() => _maxBidSaving = false);
    }
  }

  void _clearMaxBid() {
    _maxBidController.text = '';
    _saveMaxBid();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Text('⚡', style: theme.textTheme.titleMedium),
                const SizedBox(width: 6),
                Text('Energy Settings', style: theme.textTheme.titleMedium),
              ],
            ),
            if (widget.selectedUnitLabel != null) ...[
              const SizedBox(height: 4),
              Chip(label: Text(widget.selectedUnitLabel!), visualDensity: VisualDensity.compact),
            ],
            const SizedBox(height: AppSpacing.md),
            Text('Grid dispatch priority', style: theme.textTheme.labelLarge),
            Text(
              'Higher priority buildings keep power first when the city grid is under strain.',
              style: theme.textTheme.bodySmall,
            ),
            const SizedBox(height: 6),
            Row(
              children: [
                Expanded(
                  child: DropdownButtonFormField<int>(
                    key: const ValueKey('energy-priority-dropdown'),
                    initialValue: _draftPriority,
                    items: [for (var n = 1; n <= 10; n++) DropdownMenuItem(value: n, child: Text('$n'))],
                    onChanged: (v) => setState(() => _draftPriority = v ?? _draftPriority),
                  ),
                ),
                const SizedBox(width: AppSpacing.sm),
                FilledButton(
                  key: const ValueKey('energy-priority-save'),
                  onPressed: _prioritySaving ? null : _savePriority,
                  child: _prioritySaving ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Text('Save'),
                ),
              ],
            ),
            if (_prioritySuccess) const Padding(padding: EdgeInsets.only(top: 4), child: Text('Saved.', style: TextStyle(color: Colors.green))),
            if (_priorityError != null) Padding(padding: const EdgeInsets.only(top: 4), child: Text(_priorityError!, style: const TextStyle(color: Colors.red))),
            if (widget.buildingType != 'POWER_PLANT') ...[
              const SizedBox(height: AppSpacing.md),
              Text('Max spot-market bid (per kWh)', style: theme.textTheme.labelLarge),
              Text(
                'Leave blank to buy energy at any price when locally generated power is insufficient.',
                style: theme.textTheme.bodySmall,
              ),
              const SizedBox(height: 6),
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      key: const ValueKey('energy-max-bid-field'),
                      controller: _maxBidController,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(labelText: 'Max bid price'),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  FilledButton(
                    key: const ValueKey('energy-max-bid-save'),
                    onPressed: _maxBidSaving ? null : _saveMaxBid,
                    child: _maxBidSaving ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Text('Save'),
                  ),
                  TextButton(onPressed: _maxBidSaving ? null : _clearMaxBid, child: const Text('Clear')),
                ],
              ),
              if (_maxBidSuccess) const Padding(padding: EdgeInsets.only(top: 4), child: Text('Saved.', style: TextStyle(color: Colors.green))),
              if (_maxBidError != null) Padding(padding: const EdgeInsets.only(top: 4), child: Text(_maxBidError!, style: const TextStyle(color: Colors.red))),
            ],
          ],
        ),
      ),
    );
  }
}
