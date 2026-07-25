// Port of the always-visible "before you save" summary in
// `BuildingUnitGrid.vue` (ROADMAP: "Show draft-change summaries before
// committing") — stat pills (ticks/cost/cash-after) plus the link- and
// unit-change lists, with Save/Cancel and (ROADMAP 128) clipboard actions.
// Not a separate confirmation step — matches the web: Save fires the
// mutations directly, this panel is just always-visible context while
// editing.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_grid_draft_controller.dart';

class BuildingDraftSummaryPanel extends StatelessWidget {
  const BuildingDraftSummaryPanel({
    super.key,
    required this.controller,
    required this.onSave,
    required this.onCancel,
    required this.onCopy,
    required this.onPaste,
  });

  final BuildingGridDraftController controller;
  final VoidCallback onSave;
  final VoidCallback onCancel;
  final VoidCallback onCopy;
  final VoidCallback onPaste;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final linkChanges = controller.draftLinkChanges;
    final unitChanges = controller.draftUnitChanges;
    final projectedCash = controller.projectedCompanyCashAfterApply;
    final canActOnSelection = controller.isEditing && controller.selectedCell != null;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text('Editing configuration', style: theme.textTheme.titleSmall)),
                IconButton(
                  key: const ValueKey('copy-unit-button'),
                  icon: const Icon(Icons.copy, size: 18),
                  tooltip: 'Copy unit config',
                  onPressed: canActOnSelection ? onCopy : null,
                ),
                IconButton(
                  key: const ValueKey('paste-unit-button'),
                  icon: const Icon(Icons.paste, size: 18),
                  tooltip: 'Paste unit config',
                  onPressed: canActOnSelection ? onPaste : null,
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.xs,
              children: [
                Chip(label: Text('${controller.draftTotalTicks} ticks')),
                Chip(label: Text('Cost: ${controller.draftConstructionCost.toStringAsFixed(0)}')),
                if (projectedCash != null) Chip(label: Text('Cash after: ${projectedCash.toStringAsFixed(0)}')),
              ],
            ),
            if (unitChanges.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.sm),
              Text('Unit changes', style: theme.textTheme.labelLarge),
              for (final change in unitChanges)
                Text(
                  '${_unitChangeGlyph(change.changeType)} (${change.gridX},${change.gridY}) '
                  '${change.previousUnitType != null ? '${change.previousUnitType} → ' : ''}${change.unitType} '
                  '· ${change.ticks}t · ${change.cost.toStringAsFixed(0)}',
                  style: theme.textTheme.bodySmall,
                ),
            ],
            if (linkChanges.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.sm),
              Text('Link changes', style: theme.textTheme.labelLarge),
              for (final change in linkChanges)
                Text('${change.added ? '+' : '−'} ${change.description}', style: theme.textTheme.bodySmall),
            ],
            if (controller.saveError != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text('⚠ ${controller.saveError}', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error)),
            ],
            const SizedBox(height: AppSpacing.md),
            Row(
              children: [
                Expanded(child: OutlinedButton(onPressed: controller.saving ? null : onCancel, child: const Text('Cancel'))),
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: FilledButton(
                    onPressed: (controller.saving || !controller.hasDraftChanges) ? null : onSave,
                    child: controller.saving
                        ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2))
                        : Text(
                            controller.draftUpgradeUnitIds.isNotEmpty
                                ? 'Store Upgrade (${controller.draftUpgradeUnitIds.length} queued)'
                                : 'Store Configuration',
                          ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  String _unitChangeGlyph(String changeType) {
    switch (changeType) {
      case 'added':
        return '+';
      case 'removed':
        return '−';
      default:
        return '↺';
    }
  }
}
