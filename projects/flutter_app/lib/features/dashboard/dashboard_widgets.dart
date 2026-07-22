import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'dashboard_models.dart';

class DashboardCompanyCard extends StatelessWidget {
  const DashboardCompanyCard({super.key, required this.company});

  final DashboardCompany company;

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
              for (final building in company.buildings) DashboardBuildingTile(building: building),
            ],
          ],
        ),
      ),
    );
  }
}

class DashboardBuildingTile extends StatelessWidget {
  const DashboardBuildingTile({super.key, required this.building});

  final DashboardBuilding building;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      key: ValueKey('building-${building.id}'),
      contentPadding: EdgeInsets.zero,
      leading: const Icon(Icons.factory_outlined),
      title: Text(building.name),
      subtitle: Text('${building.type} · Lv.${building.level} · ${building.unitCount} unit(s)'),
      trailing: Wrap(
        spacing: 4,
        children: [
          if (building.isDestroyed) const Chip(label: Text('Destroyed')),
          if (building.hasDefaultedCollateralLoan) const Chip(label: Text('Loan default')),
          if (building.hasPowerIssue) Chip(label: Text(building.powerStatus)),
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
                  leading: const Icon(Icons.schedule_outlined),
                  title: Text('${action.actionType} · ${action.buildingName}'),
                  subtitle: Text('${action.ticksRemaining} ticks remaining'),
                ),
          ],
        ),
      ),
    );
  }
}
