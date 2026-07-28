// Ported from `projects/frontend/src/components/cityMap/CityDemandPanel.vue`
// — top-selling-products demand panel for a city's market. The web's
// "View all products →" link to `/market` becomes an in-app navigation
// callback here since there's no browser URL bar to follow.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import 'city_market_models.dart';

class CityDemandPanel extends StatelessWidget {
  const CityDemandPanel({super.key, required this.summary, required this.loading, this.onViewAllProducts});

  final CityDemandSummary? summary;
  final bool loading;
  final VoidCallback? onViewAllProducts;

  static Color _satisfactionColor(double rate) {
    if (rate >= 0.8) return Colors.green;
    if (rate >= 0.4) return Colors.amber;
    return Colors.red;
  }

  Widget _buildProductRow(BuildContext context, ThemeData theme, String languageCode, ProductDemandEntry product) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(product.productName, style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600)),
              ),
              Text(
                AppNumberFormat.money(
                  product.averageClearingPrice,
                  currencyCode: summary?.currencyCode ?? 'EUR',
                  languageCode: languageCode,
                ),
                style: theme.textTheme.bodySmall,
              ),
            ],
          ),
          const SizedBox(height: 4),
          Row(
            children: [
              Expanded(
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(3),
                  child: LinearProgressIndicator(
                    value: product.satisfactionRate.clamp(0.0, 1.0),
                    minHeight: 6,
                    backgroundColor: theme.colorScheme.surfaceContainerHighest,
                    color: _satisfactionColor(product.satisfactionRate),
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Text(
                '${(product.satisfactionRate * 100).round()}%',
                style: theme.textTheme.labelSmall?.copyWith(
                  color: _satisfactionColor(product.satisfactionRate),
                  fontWeight: FontWeight.bold,
                ),
              ),
            ],
          ),
          const SizedBox(height: 2),
          Text(
            'Sellers: ${product.sellerCount}  ·  Sold: ${product.totalQuantitySold.round()}',
            style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;
    final products = summary?.products ?? const <ProductDemandEntry>[];

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'TOP-SELLING PRODUCTS',
              style: theme.textTheme.labelLarge?.copyWith(fontWeight: FontWeight.bold, letterSpacing: 0.5),
            ),
            const SizedBox(height: 12),
            if (loading)
              const Center(child: Padding(padding: EdgeInsets.symmetric(vertical: 16), child: CircularProgressIndicator()))
            else if (products.isEmpty)
              const Padding(padding: EdgeInsets.symmetric(vertical: 8), child: Text('No demand data yet for this city.'))
            else
              for (var i = 0; i < products.length; i++) ...[
                _buildProductRow(context, theme, languageCode, products[i]),
                if (i < products.length - 1) const Divider(height: 1),
              ],
            if (onViewAllProducts != null) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton(onPressed: onViewAllProducts, child: const Text('View all products →')),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
