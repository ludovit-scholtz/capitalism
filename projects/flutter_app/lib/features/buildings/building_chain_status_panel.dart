// Port of `BuildingChainStatusPanel.vue` (simple presence-only status,
// view-mode only) and the `configWarnings` banner (BFS reachability +
// per-field checks, shown in both view and edit mode) — two independent
// mechanisms kept separate exactly as on web, see
// `building_chain_validation.dart`'s header comment.
//
// Trim: the web persists per-building dismissal in localStorage
// (`bdpanel_production_dismissed`/`bdpanel_sales_dismissed`), re-showing
// automatically once the chain becomes incomplete again. This port keeps
// dismissal as in-memory `StatefulWidget` state (lost on screen
// revisit/reload) rather than adding a SharedPreferences-backed store for
// what is a minor UX nicety, not core functionality.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_chain_validation.dart';
import 'building_grid_models.dart';

class ConfigWarningsBanner extends StatelessWidget {
  const ConfigWarningsBanner({super.key, required this.warnings});

  final List<ChainWarning> warnings;

  @override
  Widget build(BuildContext context) {
    if (warnings.isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    return Card(
      color: theme.colorScheme.errorContainer.withValues(alpha: 0.3),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('⚠ Configuration Warnings', style: theme.textTheme.titleSmall),
            const SizedBox(height: AppSpacing.xs),
            for (final warning in warnings) Text('• ${warning.message}', style: theme.textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}

class ProductionChainStatusPanel extends StatefulWidget {
  const ProductionChainStatusPanel({super.key, required this.units});

  final List<EditableGridUnit> units;

  @override
  State<ProductionChainStatusPanel> createState() => _ProductionChainStatusPanelState();
}

class _ProductionChainStatusPanelState extends State<ProductionChainStatusPanel> {
  bool _dismissed = false;

  @override
  Widget build(BuildContext context) {
    final status = getProductionChainStatus(widget.units);
    // An incomplete chain always reappears even after dismissal.
    if (_dismissed && status.isChainComplete) return const SizedBox.shrink();

    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    status.isChainComplete ? '✅ Chain Ready' : '⚠️ Configuration Needed',
                    style: theme.textTheme.titleSmall,
                  ),
                ),
                TextButton(onPressed: () => setState(() => _dismissed = true), child: const Text('Dismiss')),
              ],
            ),
            Text('Purchase: ${status.isPurchaseConfigured ? 'configured' : 'Not configured yet'}'),
            Text('Manufacturing: ${status.isManufacturingConfigured ? 'configured' : 'Not configured yet'}'),
            Text('Storage: ${status.storage != null ? 'present' : 'optional'}'),
          ],
        ),
      ),
    );
  }
}

class ShopChainStatusPanel extends StatefulWidget {
  const ShopChainStatusPanel({super.key, required this.units});

  final List<EditableGridUnit> units;

  @override
  State<ShopChainStatusPanel> createState() => _ShopChainStatusPanelState();
}

class _ShopChainStatusPanelState extends State<ShopChainStatusPanel> {
  bool _dismissed = false;

  @override
  Widget build(BuildContext context) {
    final status = getShopChainStatus(widget.units);
    if (_dismissed && status.isChainComplete) return const SizedBox.shrink();

    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(status.isChainComplete ? '✅ Ready to Sell' : '⚠️ Setup Required', style: theme.textTheme.titleSmall),
                ),
                TextButton(onPressed: () => setState(() => _dismissed = true), child: const Text('Dismiss')),
              ],
            ),
            Text('Purchase: ${status.isPurchaseConfigured ? 'configured' : 'Not configured yet'}'),
            Text('Public Sales: ${status.isPublicSalesConfigured ? 'configured' : 'Not configured yet'}'),
          ],
        ),
      ),
    );
  }
}
