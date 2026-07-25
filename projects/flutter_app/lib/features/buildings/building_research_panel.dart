// Port of `BuildingResearchPanel.vue` (ROADMAP 133) — rendered as a sibling
// panel above the grid for RESEARCH_DEVELOPMENT buildings, exactly like the
// web (`v-if="building.type === 'RESEARCH_DEVELOPMENT'"` right alongside
// `BuildingPowerPlantPanel`/`BuildingChainStatusPanel`, not nested inside
// the grid editor).
//
// 100% read-only — a company-wide summary of `Brand`/`ProductResearchBudget`
// progress (`companyBrands(companyId)`), NOT a per-unit or per-building
// query despite the roadmap's "per-unit" wording: multiple PRODUCT_QUALITY
// units targeting the same product (even across different buildings owned
// by the same company) all feed the same underlying budget/brand row
// server-side, confirmed against `Api/Engine/Phases/ResearchPhase.cs`.
//
// Deviation from web: this query includes `marketingQuality`, which the
// web's own `companyBrands` selection omits despite the field existing and
// being populated server-side — omitting it makes the web's own "Lv" badge
// and price-premium block evaluate `1-(1-quality)*(1-undefined)` to `NaN`
// whenever `quality > 0`. Fixed here rather than replicated since it's a
// strict correctness improvement with zero new backend surface.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_panel_models.dart';

double _combinedQuality(CompanyBrand brand) => 1 - (1 - brand.quality) * (1 - brand.marketingQuality);

double _qualityLevelDisplay(CompanyBrand brand) {
  final combined = _combinedQuality(brand);
  final level = (combined * 10).clamp(0, 10);
  return (level * 10).round() / 10;
}

double _qualityPremiumPct(CompanyBrand brand) => ((_combinedQuality(brand) * 50) * 10).round() / 10;

Color _qualityLevelColor(BuildContext context, double level) {
  final theme = Theme.of(context);
  if (level >= 8) return const Color(0xFFF59E0B);
  if (level >= 5) return const Color(0xFF10B981);
  if (level >= 2) return theme.colorScheme.primary;
  return theme.colorScheme.outline;
}

String _scopeLabel(String scope) {
  switch (scope) {
    case 'PRODUCT':
      return 'Product';
    case 'CATEGORY':
      return 'Category';
    default:
      return 'Company';
  }
}

class BuildingResearchPanel extends StatelessWidget {
  const BuildingResearchPanel({super.key, required this.brands, required this.loading, required this.hasConfiguredRdUnits});

  final List<CompanyBrand> brands;
  final bool loading;
  final bool hasConfiguredRdUnits;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('🔬 Research Progress', style: theme.textTheme.titleSmall),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'Research advances each tick. Each Product Quality unit converts a fraction of its operating costs '
              'into a cumulative research budget for the assigned product, which decays 0.1% per tick. Brand '
              'Quality research improves how efficiently your marketing budget converts into brand awareness.',
              style: theme.textTheme.bodySmall,
            ),
            const SizedBox(height: AppSpacing.sm),
            if (loading)
              const Padding(padding: EdgeInsets.symmetric(vertical: AppSpacing.md), child: LinearProgressIndicator())
            else if (brands.isEmpty)
              Text(
                hasConfiguredRdUnits
                    ? 'Research units are configured and pending activation. Progress will appear here once the simulation tick runs.'
                    : 'No research recorded yet. Configure Product Quality or Brand Quality units and let the simulation run to see progress here.',
                style: theme.textTheme.bodySmall,
              )
            else
              for (final brand in brands) _brandCard(context, brand),
          ],
        ),
      ),
    );
  }

  Widget _brandCard(BuildContext context, CompanyBrand brand) {
    final theme = Theme.of(context);
    final level = _qualityLevelDisplay(brand);
    final showLevelBadge = brand.quality > 0 || brand.marketingQuality > 0;
    return Card(
      key: ValueKey('brand-${brand.id}'),
      margin: const EdgeInsets.only(top: AppSpacing.sm),
      color: theme.colorScheme.surfaceContainer,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.sm),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text(brand.productName ?? brand.name, style: theme.textTheme.titleSmall)),
                Chip(label: Text(_scopeLabel(brand.scope)), visualDensity: VisualDensity.compact),
                if (showLevelBadge) ...[
                  const SizedBox(width: AppSpacing.xs),
                  Chip(
                    label: Text('Lv ${level.toStringAsFixed(1)}'),
                    backgroundColor: _qualityLevelColor(context, level).withValues(alpha: 0.2),
                    labelStyle: TextStyle(color: _qualityLevelColor(context, level)),
                    visualDensity: VisualDensity.compact,
                  ),
                ],
              ],
            ),
            if (brand.industryCategory != null) Text(brand.industryCategory!, style: theme.textTheme.bodySmall),
            if (brand.quality > 0) _progressRow(context, 'Product Quality', brand.quality),
            if (brand.marketingEfficiencyMultiplier > 1)
              _progressRow(
                context,
                'Marketing Efficiency',
                (brand.marketingEfficiencyMultiplier - 1) / 1,
                label2: '${brand.marketingEfficiencyMultiplier.toStringAsFixed(2)}×',
              ),
            if (brand.awareness > 0) _progressRow(context, 'Brand Awareness', brand.awareness),
            if (brand.scope == 'PRODUCT' && (brand.accumulatedResearchBudget != null || brand.baseResearchBudget != null)) ...[
              const Divider(height: AppSpacing.md),
              if (brand.accumulatedResearchBudget != null)
                Text('Research budget invested: ${brand.accumulatedResearchBudget!.toStringAsFixed(0)} USD', style: theme.textTheme.bodySmall),
              if (brand.baseResearchBudget != null)
                Text('Target for 100% quality (uncontested): ${brand.baseResearchBudget!.toStringAsFixed(0)} USD', style: theme.textTheme.bodySmall),
              if (brand.maxCompetitorBudget != null && brand.maxCompetitorBudget! > (brand.accumulatedResearchBudget ?? 0))
                Text('Top competitor budget: ${brand.maxCompetitorBudget!.toStringAsFixed(0)} USD', style: theme.textTheme.bodySmall),
              Text('Quality price premium: +${_qualityPremiumPct(brand).toStringAsFixed(1)}%', style: theme.textTheme.bodySmall),
            ],
            if (brand.quality > 0) ...[
              const SizedBox(height: AppSpacing.xs),
              Text(
                'Quality price premium unlocked: +${_qualityPremiumPct(brand).toStringAsFixed(1)}% above base price in demand calculation.',
                style: theme.textTheme.bodySmall?.copyWith(fontStyle: FontStyle.italic),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _progressRow(BuildContext context, String label, double value, {String? label2}) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.xs),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('$label: ${label2 ?? '${(value * 100).toStringAsFixed(0)}%'}', style: theme.textTheme.bodySmall),
          ClipRRect(
            borderRadius: BorderRadius.circular(AppRadius.sm),
            child: LinearProgressIndicator(value: value.clamp(0, 1), minHeight: 6),
          ),
        ],
      ),
    );
  }
}
