// Port of `BuildingMediaHousePanel.vue` (ROADMAP 131) — the full
// replacement for the grid/unit-list area on MEDIA_HOUSE buildings
// (MEDIA_HOUSE is internally unit-based via a dedicated `MediaHouseUnit`
// entity, entirely separate from the generic 4x4-grid `BuildingUnit` — it
// has no gridX/gridY/links, so it is not grid-eligible and gets this
// sibling panel instead, matching web).
//
// Covers the load-bearing, functionally-required parts: header metrics,
// editable Campaign Unit Configuration (`configureMediaHouseUnit`),
// editable Content Investment (`setMediaHouseContentBudget`), the Upgrade
// action (`upgradeMediaHouse`), and read-only City Rankings
// (`cityMediaHouses`).
//
// Trimmed (documented, not oversights): the "Brand Impact Analytics"
// dashboard (`mediaHouseAnalytics` — advertiser roster with
// awareness/quality bars, 30-tick income history chart, generated
// strategy-rating text) and the `mediaHouseStats` boost-delivered
// sparkline/campaign-spend-this-cycle card are both read-only, analytics-
// only surfaces layered on top of a separate query that doesn't affect
// whether the building functions — the mutations and header metrics work
// fully independently of them. Efficiency %/channel multiplier shown here
// are computed client-side from `building.level`/`mediaType`, matching the
// same formulas the web's header uses (not fetched from the trimmed
// analytics query).

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_detail_models.dart';
import 'building_panel_models.dart';

double _efficiencyPct(int level) => ((1 - 1 / (level + 1)) * 100).roundToDouble();

double _channelMultiplier(String? mediaType) {
  switch (mediaType) {
    case 'TV':
      return 2.0;
    case 'RADIO':
      return 1.5;
    default:
      return 1.0;
  }
}

const int _maxMediaHouseLevel = 5;

class BuildingMediaHousePanel extends StatefulWidget {
  const BuildingMediaHousePanel({
    super.key,
    required this.building,
    required this.units,
    required this.cityMediaHouses,
    required this.ownedCompanyNames,
    required this.onSaveBudget,
    required this.onUpgrade,
    required this.onSaveUnitConfig,
  });

  final BuildingDetail building;
  final List<MediaHouseUnitConfig> units;
  final List<CityMediaHouse> cityMediaHouses;
  final Map<String, String> ownedCompanyNames;
  final Future<void> Function(double budgetPerTick) onSaveBudget;
  final Future<void> Function() onUpgrade;
  final Future<void> Function({
    required String? unitId,
    required String targetCompanyId,
    required String mediaType,
    required double campaignBudgetPerTick,
    required bool isActive,
  })
  onSaveUnitConfig;

  @override
  State<BuildingMediaHousePanel> createState() => _BuildingMediaHousePanelState();
}

class _BuildingMediaHousePanelState extends State<BuildingMediaHousePanel> {
  final _budgetController = TextEditingController();
  bool _savingBudget = false;
  bool _upgrading = false;
  bool _savingUnit = false;

  String? _unitId;
  String? _targetCompanyId;
  String _mediaType = 'NEWSPAPER';
  final _campaignBudgetController = TextEditingController();
  bool _campaignActive = true;
  bool _seededUnitForm = false;

  @override
  void initState() {
    super.initState();
    _budgetController.text = widget.building.contentBudgetPerTick?.toStringAsFixed(0) ?? '';
    _seedUnitFormIfNeeded();
  }

  @override
  void didUpdateWidget(covariant BuildingMediaHousePanel oldWidget) {
    super.didUpdateWidget(oldWidget);
    _seedUnitFormIfNeeded();
  }

  void _seedUnitFormIfNeeded() {
    if (_seededUnitForm) return;
    if (widget.units.isNotEmpty) {
      final unit = widget.units.first;
      _unitId = unit.id;
      _targetCompanyId = unit.targetCompanyId;
      _mediaType = unit.mediaType;
      _campaignBudgetController.text = unit.campaignBudgetPerTick.toStringAsFixed(0);
      _campaignActive = unit.isActive;
      _seededUnitForm = true;
    } else if (widget.ownedCompanyNames.isNotEmpty) {
      _targetCompanyId ??= widget.building.companyId;
      _seededUnitForm = true;
    }
  }

  @override
  void dispose() {
    _budgetController.dispose();
    _campaignBudgetController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final building = widget.building;
    final level = building.level.clamp(1, _maxMediaHouseLevel);
    final isMaxLevel = level >= _maxMediaHouseLevel;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('📡 Media House Management', style: theme.textTheme.titleSmall),
            const SizedBox(height: AppSpacing.sm),
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.xs,
              children: [
                Chip(label: Text('Channel: ${building.mediaType ?? 'NEWSPAPER'}')),
                Chip(label: Text('Content: ${building.contentValue?.toStringAsFixed(0) ?? '0'}')),
                Chip(
                  label: Text(
                    building.contentBudgetPerTick != null && building.contentBudgetPerTick! > 0
                        ? 'Budget: ${building.contentBudgetPerTick!.toStringAsFixed(0)}/tick'
                        : 'No investment',
                  ),
                ),
                Chip(label: Text('Efficiency: ${_efficiencyPct(level).toStringAsFixed(0)}%')),
              ],
            ),
            const SizedBox(height: AppSpacing.md),
            Text('Campaign Unit Configuration', style: theme.textTheme.labelLarge),
            DropdownButtonFormField<String>(
              key: const ValueKey('media-house-type'),
              initialValue: _mediaType,
              decoration: const InputDecoration(labelText: 'Media Type'),
              items: const [
                DropdownMenuItem(value: 'NEWSPAPER', child: Text('📰 Newspaper')),
                DropdownMenuItem(value: 'RADIO', child: Text('📻 Radio')),
                DropdownMenuItem(value: 'TV', child: Text('📺 TV')),
              ],
              onChanged: (v) => setState(() => _mediaType = v ?? 'NEWSPAPER'),
            ),
            DropdownButtonFormField<String>(
              key: const ValueKey('media-house-target-company'),
              initialValue: _targetCompanyId,
              decoration: const InputDecoration(labelText: 'Target Company'),
              items: [
                for (final entry in widget.ownedCompanyNames.entries) DropdownMenuItem(value: entry.key, child: Text(entry.value)),
              ],
              onChanged: (v) => setState(() => _targetCompanyId = v),
            ),
            TextField(
              controller: _campaignBudgetController,
              decoration: const InputDecoration(labelText: 'Campaign Budget per Tick'),
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
            ),
            SwitchListTile(
              title: const Text('Campaign Active'),
              value: _campaignActive,
              onChanged: (v) => setState(() => _campaignActive = v),
              contentPadding: EdgeInsets.zero,
            ),
            FilledButton(
              onPressed: _savingUnit || _targetCompanyId == null
                  ? null
                  : () async {
                      final budget = double.tryParse(_campaignBudgetController.text) ?? 0;
                      setState(() => _savingUnit = true);
                      try {
                        await widget.onSaveUnitConfig(
                          unitId: _unitId,
                          targetCompanyId: _targetCompanyId!,
                          mediaType: _mediaType,
                          campaignBudgetPerTick: budget,
                          isActive: _campaignActive,
                        );
                      } finally {
                        if (mounted) setState(() => _savingUnit = false);
                      }
                    },
              child: Text(_savingUnit ? 'Saving…' : 'Save Campaign Unit'),
            ),
            const SizedBox(height: AppSpacing.md),
            Text('Upgrade', style: theme.textTheme.labelLarge),
            if (isMaxLevel)
              const Text('🏆 Maximum level reached — peak efficiency unlocked!')
            else ...[
              Text('Level $level → Level ${level + 1}: efficiency ${_efficiencyPct(level).toStringAsFixed(0)}% → ${_efficiencyPct(level + 1).toStringAsFixed(0)}%'),
              OutlinedButton(
                onPressed: (_upgrading || building.isGovernmentOwned)
                    ? null
                    : () async {
                        setState(() => _upgrading = true);
                        try {
                          await widget.onUpgrade();
                        } finally {
                          if (mounted) setState(() => _upgrading = false);
                        }
                      },
                child: Text(_upgrading ? 'Upgrading…' : 'Upgrade Now'),
              ),
            ],
            if (widget.cityMediaHouses.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.md),
              Text('City Rankings (same channel)', style: theme.textTheme.labelLarge),
              for (final mh in widget.cityMediaHouses.where((m) => m.mediaType == building.mediaType)) _rankingRow(theme, mh),
            ],
            const SizedBox(height: AppSpacing.md),
            Text('Marketing Effectiveness', style: theme.textTheme.labelLarge),
            Text('Channel reach multiplier: ${_channelMultiplier(building.mediaType).toStringAsFixed(1)}×'),
            const SizedBox(height: AppSpacing.md),
            Text('Content Investment', style: theme.textTheme.labelLarge),
            Text(
              'Each tick, ${_efficiencyPct(level).toStringAsFixed(0)}% of your content budget converts to accumulated content. Content decays 0.5% per tick.',
              style: theme.textTheme.bodySmall,
            ),
            TextField(
              controller: _budgetController,
              decoration: const InputDecoration(labelText: 'Content spend per tick'),
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
            ),
            Row(
              children: [
                FilledButton(
                  onPressed: _savingBudget
                      ? null
                      : () async {
                          final value = double.tryParse(_budgetController.text) ?? 0;
                          setState(() => _savingBudget = true);
                          try {
                            await widget.onSaveBudget(value);
                          } finally {
                            if (mounted) setState(() => _savingBudget = false);
                          }
                        },
                  child: Text(_savingBudget ? 'Saving…' : 'Save Budget'),
                ),
                if ((building.contentBudgetPerTick ?? 0) > 0) ...[
                  const SizedBox(width: AppSpacing.sm),
                  OutlinedButton(
                    onPressed: _savingBudget
                        ? null
                        : () async {
                            setState(() {
                              _budgetController.text = '0';
                              _savingBudget = true;
                            });
                            try {
                              await widget.onSaveBudget(0);
                            } finally {
                              if (mounted) setState(() => _savingBudget = false);
                            }
                          },
                    child: const Text('Stop Investment'),
                  ),
                ],
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _rankingRow(ThemeData theme, CityMediaHouse mh) {
    final isOwn = mh.id == widget.building.id;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          Expanded(child: Text(mh.name, style: isOwn ? const TextStyle(fontWeight: FontWeight.bold) : null)),
          Expanded(
            flex: 2,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(AppRadius.sm),
              child: LinearProgressIndicator(value: (mh.contentRanking / 100).clamp(0, 1), minHeight: 8),
            ),
          ),
          const SizedBox(width: AppSpacing.xs),
          Text('${mh.contentRanking.toStringAsFixed(0)}%'),
          if (isOwn) ...[const SizedBox(width: AppSpacing.xs), const Chip(label: Text('YOU'), visualDensity: VisualDensity.compact)],
        ],
      ),
    );
  }
}
