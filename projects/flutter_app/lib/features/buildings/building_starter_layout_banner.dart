// Port of the starter-layout banners in `BuildingDetailView.vue`
// (ROADMAP: "Implement the starter-layout one-click setup banners") — shown
// only for an empty FACTORY/SALES_SHOP building with no pending plan and
// not already in edit mode.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';

class StarterLayoutBanner extends StatelessWidget {
  const StarterLayoutBanner({super.key, required this.isShop, required this.onApply});

  final bool isShop;
  final VoidCallback onApply;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final title = isShop ? '🏪 New Sales Shop — Ready for Your First Product' : '🏭 New Factory — Ready to Set Up';
    final body = isShop
        ? 'This sales shop has no units configured yet. Apply the starter layout to begin selling, then configure your product and public selling price.'
        : 'This factory has no units configured yet. Apply the starter layout to begin producing, then customise units to match your strategy.';
    final desc = isShop
        ? 'Starter layout: Purchase (0,0) → Public Sales (1,0) — the minimum chain to stock products from the factory and sell them directly to the public.'
        : 'Starter layout: Purchase (0,0) → Manufacturing (1,0) → Storage (2,0) → B2B Sales (3,0) — the minimum chain to buy resources, manufacture goods, hold stock, and sell wholesale.';
    final buttonLabel = isShop ? 'Apply Starter Shop Layout' : 'Apply Starter Layout';

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: theme.textTheme.titleSmall),
            const SizedBox(height: AppSpacing.xs),
            Text(body, style: theme.textTheme.bodyMedium),
            const SizedBox(height: AppSpacing.xs),
            Text(desc, style: theme.textTheme.bodySmall),
            const SizedBox(height: AppSpacing.md),
            FilledButton(onPressed: onApply, child: Text(buttonLabel)),
          ],
        ),
      ),
    );
  }
}
