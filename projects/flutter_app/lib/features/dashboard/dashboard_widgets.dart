import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_icons.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/icon_badge.dart';
import '../buildings/building_analytics_models.dart';
import '../buildings/building_panel_models.dart';
import 'dashboard_models.dart';
import 'dashboard_power_balance_chip.dart';
import 'dashboard_supply_chain_strip.dart';

class DashboardCompanyCard extends StatelessWidget {
  const DashboardCompanyCard({
    super.key,
    required this.company,
    this.onRemoveBuilding,
    this.removingBuildingIds = const {},
    this.buildingFinancials = const {},
    this.unitStatuses = const {},
    this.cityPowerBalances = const {},
  });

  final DashboardCompany company;

  /// Invoked with a destroyed building's id when the player confirms
  /// removal from `DashboardBuildingTile`'s remove action (ROADMAP 139).
  final Future<void> Function(String buildingId)? onRemoveBuilding;

  /// Building ids currently mid-removal, so their tile can show a spinner
  /// and disable the remove action while the mutation is in flight.
  final Set<String> removingBuildingIds;

  /// Per-building compact financial strip data, keyed by building id.
  final Map<String, BuildingFinancialTimeline> buildingFinancials;

  /// Per-building unit operational statuses, keyed by building id, feeding
  /// each building's `DashboardSupplyChainStrip`.
  final Map<String, List<BuildingUnitOperationalStatus>> unitStatuses;

  /// Per-city power-grid balance, keyed by cityId — one chip is shown per
  /// distinct city among this company's buildings.
  final Map<String, CityPowerBalance> cityPowerBalances;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cityIds = {for (final b in company.buildings) if (b.cityId.isNotEmpty) b.cityId}.toList();

    return Card(
      key: ValueKey('company-card-${company.id}'),
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
                  financials: buildingFinancials[building.id],
                  unitStatuses: unitStatuses[building.id] ?? const [],
                ),
              if (cityIds.isNotEmpty) ...[
                const SizedBox(height: 8),
                for (final cityId in cityIds) DashboardPowerBalanceChip(cityId: cityId, balance: cityPowerBalances[cityId]),
              ],
            ],
          ],
        ),
      ),
    );
  }
}

class DashboardBuildingTile extends StatelessWidget {
  const DashboardBuildingTile({
    super.key,
    required this.building,
    this.onRemove,
    this.removing = false,
    this.financials,
    this.unitStatuses = const [],
  });

  final DashboardBuilding building;

  /// Called after the player confirms removal in the dialog this widget
  /// shows itself — mirrors `removeDestroyedBuilding` on web, scoped to the
  /// dashboard tile per ROADMAP 139 rather than the building detail screen.
  final VoidCallback? onRemove;
  final bool removing;

  /// `null` while still loading — the financial strip renders nothing.
  final BuildingFinancialTimeline? financials;

  /// Empty while still loading or if the building has no units — the
  /// supply-chain strip only renders when `building.units` is non-empty
  /// regardless of this list's state.
  final List<BuildingUnitOperationalStatus> unitStatuses;

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
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('${building.type} · Lv.${building.level} · ${building.unitCount} unit(s)'),
          DashboardBuildingFinancialsStrip(financials: financials),
          if (building.units.isNotEmpty) DashboardSupplyChainStrip(units: building.units, statuses: unitStatuses),
        ],
      ),
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

/// Compact revenue/costs/profit strip per building, mirroring
/// `BuildingHeaderFinancials.vue`. Reuses `BuildingFinancialTimeline`
/// (`building_analytics_models.dart`) — only the 3 totals matter here, not
/// the full per-tick timeline that backs the building-detail chart.
class DashboardBuildingFinancialsStrip extends StatelessWidget {
  const DashboardBuildingFinancialsStrip({super.key, required this.financials});

  final BuildingFinancialTimeline? financials;

  @override
  Widget build(BuildContext context) {
    final value = financials;
    if (value == null) return const SizedBox.shrink();
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(top: 6),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          _FinancialStat(label: 'Sales', value: value.totalSales, theme: theme),
          const SizedBox(width: 12),
          _FinancialStat(label: 'Costs', value: value.totalCosts, theme: theme),
          const SizedBox(width: 12),
          _FinancialStat(label: 'Profit', value: value.totalProfit, theme: theme, emphasize: true),
        ],
      ),
    );
  }
}

class _FinancialStat extends StatelessWidget {
  const _FinancialStat({required this.label, required this.value, required this.theme, this.emphasize = false});

  final String label;
  final double value;
  final ThemeData theme;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: theme.textTheme.labelSmall),
        Text(
          '\$${value.toStringAsFixed(0)}',
          style: (emphasize ? theme.textTheme.labelLarge : theme.textTheme.labelMedium)?.copyWith(
            color: emphasize ? (value >= 0 ? Colors.green : theme.colorScheme.error) : null,
          ),
        ),
      ],
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
