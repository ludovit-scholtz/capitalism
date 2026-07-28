// Per-city financial breakdown grid for the Ledger screen, ported from the
// "city-financial-section" of `LedgerMainContent.vue` — revenue/costs/
// profit per city plus a small revenue-trend sparkline.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import 'ledger_models.dart';

class LedgerCityBreakdownPanel extends StatelessWidget {
  const LedgerCityBreakdownPanel({super.key, required this.breakdown});

  final List<CityFinancialBreakdown> breakdown;

  @override
  Widget build(BuildContext context) {
    if (breakdown.isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('🏙️ FINANCIALS BY CITY', style: theme.textTheme.labelLarge?.copyWith(fontWeight: FontWeight.bold, letterSpacing: 0.5)),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [for (final summary in breakdown) _CityCard(summary: summary, languageCode: languageCode)],
            ),
          ],
        ),
      ),
    );
  }
}

class _CityCard extends StatelessWidget {
  const _CityCard({required this.summary, required this.languageCode});

  final CityFinancialBreakdown summary;
  final String languageCode;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final maxRevenue = summary.revenueTrend.fold<double>(0, (max, p) => p.revenue > max ? p.revenue : max);

    return Container(
      width: 240,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(border: Border.all(color: theme.colorScheme.outlineVariant), borderRadius: BorderRadius.circular(10)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(summary.cityName, style: theme.textTheme.titleSmall),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                decoration: BoxDecoration(color: theme.colorScheme.primary.withValues(alpha: 0.15), borderRadius: BorderRadius.circular(4)),
                child: Text(summary.currencyCode, style: TextStyle(color: theme.colorScheme.primary, fontSize: 10, fontWeight: FontWeight.bold)),
              ),
            ],
          ),
          const SizedBox(height: 8),
          _row(theme, 'Revenue', summary.revenue, Colors.green.shade600),
          _row(theme, 'Costs', -summary.costs, Colors.red.shade600),
          _row(theme, 'Profit', summary.profit, summary.profit >= 0 ? Colors.green.shade600 : Colors.red.shade600),
          if (summary.revenueTrend.isNotEmpty) ...[
            const SizedBox(height: 8),
            SizedBox(
              height: 32,
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  for (final point in summary.revenueTrend)
                    Expanded(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 0.5),
                        child: FractionallySizedBox(
                          alignment: Alignment.bottomCenter,
                          heightFactor: maxRevenue <= 0 ? 0.05 : (point.revenue / maxRevenue).clamp(0.05, 1.0),
                          child: Container(color: theme.colorScheme.primary.withValues(alpha: 0.6)),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _row(ThemeData theme, String label, double amount, Color color) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 1),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: theme.textTheme.bodySmall),
          Text(
            AppNumberFormat.money(amount, currencyCode: summary.currencyCode, languageCode: languageCode),
            style: theme.textTheme.bodySmall?.copyWith(color: color, fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}
