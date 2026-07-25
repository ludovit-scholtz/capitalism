import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_icons.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/icon_badge.dart';
import 'dashboard_models.dart';

class DashboardCompanyCard extends StatelessWidget {
  const DashboardCompanyCard({super.key, required this.company, this.onRemoveBuilding, this.removingBuildingIds = const {}});

  final DashboardCompany company;

  /// Invoked with a destroyed building's id when the player confirms
  /// removal from `DashboardBuildingTile`'s remove action (ROADMAP 139).
  final Future<void> Function(String buildingId)? onRemoveBuilding;

  /// Building ids currently mid-removal, so their tile can show a spinner
  /// and disable the remove action while the mutation is in flight.
  final Set<String> removingBuildingIds;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.only(bottom: 16),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(company.name, style: theme.textTheme.titleLarge),
            const SizedBox(height: 4),
            Text('Cash: \$${company.cash.toStringAsFixed(0)} · ${company.buildings.length} building(s)'),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                OutlinedButton(
                  onPressed: () => context.go('/buy-building/${company.id}'),
                  child: const Text('Buy Building'),
                ),
                OutlinedButton(onPressed: () => context.go('/ledger/${company.id}'), child: const Text('Ledger')),
                OutlinedButton(
                  onPressed: () => context.go('/company/${company.id}/contracts'),
                  child: const Text('Contracts'),
                ),
                OutlinedButton(
                  onPressed: () => context.go('/company/${company.id}/research'),
                  child: const Text('Research'),
                ),
                OutlinedButton(
                  onPressed: () => context.go('/company/${company.id}/settings'),
                  child: const Text('Settings'),
                ),
              ],
            ),
            if (company.buildings.isNotEmpty) ...[
              const SizedBox(height: 16),
              Text('Buildings', style: theme.textTheme.titleMedium),
              const SizedBox(height: 4),
              for (final building in company.buildings)
                DashboardBuildingTile(
                  building: building,
                  removing: removingBuildingIds.contains(building.id),
                  onRemove: onRemoveBuilding == null ? null : () => onRemoveBuilding!(building.id),
                ),
            ],
          ],
        ),
      ),
    );
  }
}

class DashboardBuildingTile extends StatelessWidget {
  const DashboardBuildingTile({super.key, required this.building, this.onRemove, this.removing = false});

  final DashboardBuilding building;

  /// Called after the player confirms removal in the dialog this widget
  /// shows itself — mirrors `removeDestroyedBuilding` on web, scoped to the
  /// dashboard tile per ROADMAP 139 rather than the building detail screen.
  final VoidCallback? onRemove;
  final bool removing;

  Future<void> _confirmRemove(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Remove destroyed building?'),
        content: Text('"${building.name}" was destroyed and can be removed from your dashboard. This cannot be undone.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Remove')),
        ],
      ),
    );
    if (confirmed == true) onRemove?.call();
  }

  @override
  Widget build(BuildContext context) {
    return ListTile(
      key: ValueKey('building-${building.id}'),
      contentPadding: EdgeInsets.zero,
      leading: const IconBadge(icon: AppIcons.factory, size: 36, iconSize: 16),
      title: Text(building.name),
      subtitle: Text('${building.type} · Lv.${building.level} · ${building.unitCount} unit(s)'),
      trailing: Wrap(
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: 4,
        children: [
          if (building.isDestroyed) const Chip(label: Text('Destroyed')),
          if (building.hasDefaultedCollateralLoan) const Chip(label: Text('Loan default')),
          if (building.hasPowerIssue) Chip(label: Text(building.powerStatus)),
          if (building.isDestroyed && onRemove != null)
            removing
                ? const SizedBox(
                    width: 32,
                    height: 32,
                    child: Padding(padding: EdgeInsets.all(6), child: CircularProgressIndicator(strokeWidth: 2)),
                  )
                : IconButton(
                    icon: const FaIcon(AppIcons.trash, size: 16),
                    tooltip: 'Remove from dashboard',
                    onPressed: () => _confirmRemove(context),
                  ),
        ],
      ),
      onTap: () => context.go(building.type == 'BANK' ? '/bank/${building.id}' : '/building/${building.id}'),
    );
  }
}

class DashboardPendingActionsSection extends StatelessWidget {
  const DashboardPendingActionsSection({super.key, required this.actions});

  final List<ScheduledAction> actions;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Pending Actions', style: theme.textTheme.titleMedium),
            const SizedBox(height: 8),
            if (actions.isEmpty)
              const Text('No pending actions.')
            else
              for (final action in actions)
                ListTile(
                  key: ValueKey('action-${action.id}'),
                  contentPadding: EdgeInsets.zero,
                  leading: const IconBadge(icon: AppIcons.clock, size: 36, iconSize: 16, color: AppTheme.neonAmber),
                  title: Text('${action.actionType} · ${action.buildingName}'),
                  subtitle: Text('${action.ticksRemaining} ticks remaining'),
                ),
          ],
        ),
      ),
    );
  }
}
