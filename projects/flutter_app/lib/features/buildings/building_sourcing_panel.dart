// Global Exchange sourcing/vendor comparison for PURCHASE units (ROADMAP
// 136), embedded in `UnitConfigSheet`. Read-only comparison, matching the
// three named operations (`sourcingCandidates`/`globalExchangeOffers`/
// `procurementPreview`) — no vendor-lock mutation. Table-as-cards for
// mobile instead of `BuildingReadonlySidebar.vue`'s "Sourcing Comparison"
// table.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_sourcing_models.dart';

class SourcingComparisonPanel extends StatelessWidget {
  const SourcingComparisonPanel({super.key, required this.preview, required this.candidates, required this.loading});

  final ProcurementPreview? preview;
  final List<SourcingCandidate> candidates;
  final bool loading;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: AppSpacing.md),
        Text('Sourcing', style: theme.textTheme.titleSmall),
        const SizedBox(height: AppSpacing.xs),
        if (loading)
          const Padding(padding: EdgeInsets.symmetric(vertical: AppSpacing.sm), child: LinearProgressIndicator())
        else ...[
          if (preview != null) _ProcurementPreviewCard(preview: preview!),
          if (candidates.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.sm),
            Text('Sourcing Comparison', style: theme.textTheme.labelMedium),
            const SizedBox(height: AppSpacing.xs),
            for (final candidate in candidates)
              Padding(
                key: ValueKey('sourcing-candidate-${candidate.rank}-${candidate.sourceType}'),
                padding: const EdgeInsets.only(bottom: AppSpacing.xs),
                child: _SourcingCandidateCard(candidate: candidate),
              ),
          ] else if (preview == null)
            Text('No sourcing options available.', style: theme.textTheme.bodySmall),
        ],
      ],
    );
  }
}

class _ProcurementPreviewCard extends StatelessWidget {
  const _ProcurementPreviewCard({required this.preview});

  final ProcurementPreview preview;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final blocked = !preview.canExecute;
    final accent = blocked ? const Color(0xFFEF4444) : const Color(0xFF22C55E);
    return Container(
      padding: const EdgeInsets.all(AppSpacing.sm),
      decoration: BoxDecoration(
        color: accent.withValues(alpha: 0.1),
        border: Border.all(color: accent.withValues(alpha: 0.4)),
        borderRadius: BorderRadius.circular(AppRadius.sm),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            blocked ? 'Blocked next tick' : 'Will execute next tick',
            style: theme.textTheme.labelMedium?.copyWith(color: accent),
          ),
          if (blocked && preview.blockMessage != null) Text(preview.blockMessage!, style: theme.textTheme.bodySmall),
          if (preview.sourceVendorName != null) Text('Vendor: ${preview.sourceVendorName}', style: theme.textTheme.bodySmall),
          if (preview.sourceCityName != null) Text('From: ${preview.sourceCityName}', style: theme.textTheme.bodySmall),
          if (preview.deliveredPricePerUnit != null)
            Text('Delivered price: ${preview.deliveredPricePerUnit!.toStringAsFixed(2)}', style: theme.textTheme.bodySmall),
          if (preview.estimatedQuality != null)
            Text('Quality: ${(preview.estimatedQuality! * 100).toStringAsFixed(0)}%', style: theme.textTheme.bodySmall),
        ],
      ),
    );
  }
}

class _SourcingCandidateCard extends StatelessWidget {
  const _SourcingCandidateCard({required this.candidate});

  final SourcingCandidate candidate;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final accent = !candidate.isEligible
        ? const Color(0xFFEF4444)
        : candidate.isRecommended
        ? const Color(0xFF22C55E)
        : theme.colorScheme.outline;
    return Container(
      padding: const EdgeInsets.all(AppSpacing.sm),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainer,
        border: Border.all(color: accent.withValues(alpha: 0.5)),
        borderRadius: BorderRadius.circular(AppRadius.sm),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              if (candidate.isRecommended) const Padding(padding: EdgeInsets.only(right: 4), child: Text('★', style: TextStyle(color: Color(0xFF22C55E)))),
              Expanded(
                child: Text(
                  candidate.sourceVendorName ?? candidate.sourceCityName ?? candidate.sourceType,
                  style: theme.textTheme.labelMedium,
                ),
              ),
              if (!candidate.isEligible) const Chip(label: Text('Blocked', style: TextStyle(fontSize: 11))),
            ],
          ),
          if (candidate.deliveredPricePerUnit != null)
            Text('Delivered: ${candidate.deliveredPricePerUnit!.toStringAsFixed(2)}', style: theme.textTheme.bodySmall),
          if (candidate.transitCostPerUnit != null) Text('Transit: ${candidate.transitCostPerUnit!.toStringAsFixed(2)}', style: theme.textTheme.bodySmall),
          if (candidate.estimatedQuality != null) Text('Quality: ${(candidate.estimatedQuality! * 100).toStringAsFixed(0)}%', style: theme.textTheme.bodySmall),
          if (!candidate.isEligible && candidate.blockMessage != null)
            Text(candidate.blockMessage!, style: theme.textTheme.bodySmall?.copyWith(color: accent)),
        ],
      ),
    );
  }
}
