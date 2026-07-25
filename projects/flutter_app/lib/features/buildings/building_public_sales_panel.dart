// PUBLIC_SALES-specific tools (ROADMAP 135), embedded in `UnitConfigSheet`
// for PUBLIC_SALES units: sales analytics + the market-event banner scoped
// to the building's city (ROADMAP 138a), the low-inventory alert
// threshold, and "flush storage". Mirrors the "Market Intelligence"/
// "Inventory" sections of `BuildingReadonlySidebar.vue`. Trimmed from web
// (documented, not an oversight — matching this app's existing trim
// convention): `seasonalOutlook`'s quarter-forecast breakdown and
// `demandDrivers` are summarized as the current demand signal/trend text
// instead of their own sub-panels.

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import '../exchange/forex_models.dart' show MarketEvent;
import 'building_bar_history.dart';
import 'building_sales_models.dart';

class PublicSalesToolsPanel extends StatefulWidget {
  const PublicSalesToolsPanel({
    super.key,
    required this.analytics,
    required this.analyticsLoading,
    required this.marketEvents,
    required this.currentThreshold,
    required this.onSaveThreshold,
    required this.onFlushStorage,
  });

  final PublicSalesAnalytics? analytics;
  final bool analyticsLoading;
  final List<MarketEvent> marketEvents;
  final double? currentThreshold;
  final Future<void> Function(double? threshold) onSaveThreshold;
  final Future<void> Function() onFlushStorage;

  @override
  State<PublicSalesToolsPanel> createState() => _PublicSalesToolsPanelState();
}

class _PublicSalesToolsPanelState extends State<PublicSalesToolsPanel> {
  late final TextEditingController _thresholdController;
  bool _savingThreshold = false;
  String? _thresholdError;
  bool _flushing = false;

  @override
  void initState() {
    super.initState();
    _thresholdController = TextEditingController(text: widget.currentThreshold?.toStringAsFixed(0) ?? '');
  }

  @override
  void dispose() {
    _thresholdController.dispose();
    super.dispose();
  }

  Future<void> _saveThreshold() async {
    final text = _thresholdController.text.trim();
    double? parsed;
    if (text.isNotEmpty) {
      parsed = double.tryParse(text);
      if (parsed == null || parsed < 0) {
        setState(() => _thresholdError = 'Enter a positive number, or leave blank to disable.');
        return;
      }
    }
    setState(() {
      _savingThreshold = true;
      _thresholdError = null;
    });
    try {
      await widget.onSaveThreshold(parsed);
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Alert threshold saved.')));
    } catch (e) {
      if (mounted) setState(() => _thresholdError = 'Could not save the threshold. Please try again.');
    } finally {
      if (mounted) setState(() => _savingThreshold = false);
    }
  }

  Future<void> _confirmFlush() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Discard all inventory?'),
        content: const Text('This permanently discards everything currently stored in this unit. This cannot be undone.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Yes, Discard All')),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _flushing = true);
    try {
      await widget.onFlushStorage();
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not flush storage. Please try again.')));
    } finally {
      if (mounted) setState(() => _flushing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final analytics = widget.analytics;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: AppSpacing.md),
        Text('Market Intelligence', style: theme.textTheme.titleSmall),
        const SizedBox(height: AppSpacing.xs),
        if (widget.marketEvents.isNotEmpty) ...[_MarketEventBanner(event: widget.marketEvents.first), const SizedBox(height: AppSpacing.sm)],
        if (widget.analyticsLoading)
          const Padding(padding: EdgeInsets.symmetric(vertical: AppSpacing.sm), child: LinearProgressIndicator())
        else if (analytics == null)
          Text('No sales analytics yet.', style: theme.textTheme.bodySmall)
        else ...[
          Wrap(
            spacing: AppSpacing.md,
            runSpacing: AppSpacing.xs,
            children: [
              _Metric(label: 'Revenue', value: analytics.totalRevenue.toStringAsFixed(0)),
              _Metric(label: 'Profit', value: analytics.totalProfit.toStringAsFixed(0)),
              _Metric(label: 'Sold', value: analytics.totalQuantitySold.toStringAsFixed(0)),
              _Metric(label: 'Avg price', value: analytics.averagePricePerUnit.toStringAsFixed(2)),
              _Metric(label: 'Utilization', value: '${(analytics.recentUtilization * 100).toStringAsFixed(0)}%'),
            ],
          ),
          if (analytics.demandSignal != null || analytics.trendDirection != null) ...[
            const SizedBox(height: AppSpacing.xs),
            Text(
              [
                if (analytics.demandSignal != null) 'Demand: ${analytics.demandSignal}',
                if (analytics.trendDirection != null) 'Trend: ${analytics.trendDirection}',
              ].join(' · '),
              style: theme.textTheme.bodySmall,
            ),
          ],
          if (analytics.actionHint != null) Text(analytics.actionHint!, style: theme.textTheme.bodySmall),
          if (analytics.revenueHistory.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.sm),
            Text('Revenue history', style: theme.textTheme.labelSmall),
            BarHistoryRow(values: analytics.revenueHistory.map((p) => p.revenue).toList(), color: const Color(0xFF2563EB), height: 32),
          ],
          if (analytics.priceHistory.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.xs),
            Text('Price history', style: theme.textTheme.labelSmall),
            BarHistoryRow(values: analytics.priceHistory.map((p) => p.pricePerUnit).toList(), color: const Color(0xFF8C5CFF), height: 32),
          ],
          if (analytics.marketShare.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.sm),
            Text('Market share', style: theme.textTheme.labelSmall),
            for (final entry in analytics.marketShare)
              Text(
                '${entry.label}: ${(entry.share * 100).toStringAsFixed(0)}%${entry.isUnmet ? ' (unmet demand)' : ''}',
                style: theme.textTheme.bodySmall,
              ),
          ],
        ],
        const SizedBox(height: AppSpacing.md),
        Text('Low-Inventory Alert', style: theme.textTheme.titleSmall),
        const SizedBox(height: AppSpacing.xs),
        Text('Get notified when this unit\'s inventory drops below this quantity. Leave blank to disable.', style: theme.textTheme.bodySmall),
        const SizedBox(height: AppSpacing.xs),
        Row(
          children: [
            Expanded(
              child: TextField(
                key: const ValueKey('public-sales-threshold-input'),
                controller: _thresholdController,
                decoration: InputDecoration(labelText: 'Alert threshold', errorText: _thresholdError),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            FilledButton(
              onPressed: _savingThreshold ? null : _saveThreshold,
              child: _savingThreshold
                  ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Save'),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.md),
        OutlinedButton.icon(
          icon: _flushing
              ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2))
              : const Icon(Icons.delete_sweep_outlined, size: 18),
          label: const Text('Discard All Inventory'),
          onPressed: _flushing ? null : _confirmFlush,
        ),
      ],
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [Text(label, style: theme.textTheme.labelSmall), Text(value, style: theme.textTheme.bodyMedium)],
    );
  }
}

class _MarketEventBanner extends StatelessWidget {
  const _MarketEventBanner({required this.event});

  final MarketEvent event;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.all(AppSpacing.sm),
      decoration: BoxDecoration(
        color: const Color(0xFFFFC857).withValues(alpha: 0.12),
        border: Border.all(color: const Color(0xFFFFC857).withValues(alpha: 0.4)),
        borderRadius: BorderRadius.circular(AppRadius.sm),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(event.title, style: theme.textTheme.labelMedium?.copyWith(color: const Color(0xFFFFC857))),
          Text(event.description, style: theme.textTheme.bodySmall),
        ],
      ),
    );
  }
}
