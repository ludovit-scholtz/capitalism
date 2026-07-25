// Port of the unit picker inside `BuildingEditingSidebar.vue`
// (`showUnitPicker`) — a list of the building type's allowed unit types
// with a color swatch and construction-cost preview, tap to place.
//
// Cost is FX-converted to the city's currency here (the web's picker shows
// the raw unconverted EUR number with the city's currency symbol — a
// display bug flagged during porting; fixed rather than replicated, see
// `building_grid_draft_controller_summary.dart`'s `draftConstructionCost`
// doc comment for the same call made there).

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_unit_economics.dart';
import 'building_unit_grid.dart';

class UnitPickerSheet extends StatelessWidget {
  const UnitPickerSheet({super.key, required this.allowedUnitTypes, required this.cityFxRate, required this.onSelect});

  final List<String> allowedUnitTypes;
  final double cityFxRate;
  final void Function(String unitType) onSelect;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Select unit type', style: theme.textTheme.titleMedium),
            const SizedBox(height: AppSpacing.sm),
            ConstrainedBox(
              constraints: BoxConstraints(maxHeight: MediaQuery.of(context).size.height * 0.6),
              child: ListView.separated(
                shrinkWrap: true,
                itemCount: allowedUnitTypes.length,
                separatorBuilder: (context, index) => const SizedBox(height: AppSpacing.xs),
                itemBuilder: (context, index) {
                  final unitType = allowedUnitTypes[index];
                  final color = unitTypeColors[unitType] ?? theme.colorScheme.primary;
                  final cost = getUnitConstructionCost(unitType) * cityFxRate;
                  return ListTile(
                    key: ValueKey('picker-$unitType'),
                    leading: CircleAvatar(backgroundColor: color.withValues(alpha: 0.2), foregroundColor: color, child: Text(unitType.substring(0, 1))),
                    title: Text(unitTypeShortLabel(unitType)),
                    subtitle: Text('Cost: ${cost.toStringAsFixed(0)}'),
                    onTap: () => onSelect(unitType),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}
