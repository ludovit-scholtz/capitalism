// Ported from `projects/frontend/src/views/MarketDashboardView.vue` —
// factored out of `market_screens.dart` to keep that file under the
// 500-line budget. Now includes the price-history panel
// (`MarketPriceHistoryPanel.vue` port, `_PriceHistoryPanel` below), which
// was previously trimmed in favor of the plain product list.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import 'market_models.dart';
import 'market_service.dart';

class MarketDashboardScreen extends StatefulWidget {
  const MarketDashboardScreen({super.key, GraphQlService? graphQlService, MarketService? marketService})
    : _injectedGraphQlService = graphQlService,
      _injectedMarketService = marketService;

  final GraphQlService? _injectedGraphQlService;
  final MarketService? _injectedMarketService;

  @override
  State<MarketDashboardScreen> createState() => _MarketDashboardScreenState();
}

class _MarketDashboardScreenState extends State<MarketDashboardScreen> {
  late final MarketService _service;

  bool _loading = true;
  String? _error;
  List<Map<String, String>> _cities = const [];
  String? _cityId;
  MarketOverview? _overview;
  String? _selectedProductId;
  List<CompetitorQuality> _competitors = const [];

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedMarketService ?? MarketService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final cities = await _service.fetchCities();
      if (!mounted) return;
      final cityId = _cityId ?? (cities.isNotEmpty ? cities.first['id'] : null);
      MarketOverview? overview;
      if (cityId != null) overview = await _service.fetchMarketOverview(cityId);
      if (!mounted) return;
      setState(() {
        _cities = cities;
        _cityId = cityId;
        _overview = overview;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the market dashboard. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _selectProduct(MarketOverviewProduct product) async {
    if (_selectedProductId == product.productTypeId) {
      setState(() {
        _selectedProductId = null;
        _competitors = const [];
      });
      return;
    }
    setState(() => _selectedProductId = product.productTypeId);
    try {
      final competitors = await _service.fetchCompetitorIntelligence(cityId: _cityId!, productTypeId: product.productTypeId);
      if (mounted) setState(() => _competitors = competitors);
    } catch (_) {
      // Keep the row selected without competitor detail on failure.
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [Text(_error!), const SizedBox(height: 12), OutlinedButton(onPressed: _load, child: const Text('Try again'))],
          ),
        ),
      );
    }

    final overview = _overview;
    final theme = Theme.of(context);

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Market Dashboard', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          initialValue: _cityId,
          decoration: const InputDecoration(labelText: 'City'),
          items: [for (final city in _cities) DropdownMenuItem(value: city['id'], child: Text(city['name']!))],
          onChanged: (value) {
            setState(() {
              _cityId = value;
              _selectedProductId = null;
            });
            _load();
          },
        ),
        const SizedBox(height: 16),
        if (overview == null || overview.products.isEmpty)
          const Text('No market activity recorded for this city yet.')
        else
          for (final product in overview.products)
            Card(
              key: ValueKey('market-product-${product.productTypeId}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: Column(
                children: [
                  ListTile(
                    title: Text(product.productName),
                    subtitle: Text('Sold ${product.totalQuantitySold.toStringAsFixed(0)} of ${product.totalDemand.toStringAsFixed(0)} demanded (${(product.satisfactionRate * 100).toStringAsFixed(0)}%)'),
                    trailing: Text(product.averageClearingPrice.toStringAsFixed(2)),
                    onTap: () => _selectProduct(product),
                  ),
                  if (_selectedProductId == product.productTypeId)
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          _PriceHistoryPanel(marketService: _service, cityId: _cityId!, product: product),
                          const SizedBox(height: 12),
                          Text('Competitor intelligence', style: theme.textTheme.labelLarge),
                          const SizedBox(height: 4),
                          if (_competitors.isEmpty)
                            const Text('No competitor data.')
                          else
                            for (final competitor in _competitors)
                              Text(
                                '${competitor.companyName}${competitor.isOwnCompany ? ' (you)' : ''} — quality ${(competitor.qualityLevel * 100).toStringAsFixed(0)}%, ${competitor.pricePremiumPct >= 0 ? '+' : ''}${competitor.pricePremiumPct.toStringAsFixed(1)}% price',
                              ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
      ],
    );
  }
}

/// Ports `projects/frontend/src/components/market/MarketPriceHistoryPanel.vue`
/// — the web renders a table (not an actual chart, despite the file name),
/// so this does the same rather than pulling in a charting dependency.
class _PriceHistoryPanel extends StatefulWidget {
  const _PriceHistoryPanel({required this.marketService, required this.cityId, required this.product});

  final MarketService marketService;
  final String cityId;
  final MarketOverviewProduct product;

  @override
  State<_PriceHistoryPanel> createState() => _PriceHistoryPanelState();
}

class _PriceHistoryPanelState extends State<_PriceHistoryPanel> {
  bool _loading = true;
  String? _error;
  List<MarketPriceHistoryPoint> _history = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didUpdateWidget(covariant _PriceHistoryPanel oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.product.productTypeId != widget.product.productTypeId || oldWidget.cityId != widget.cityId) {
      _load();
    }
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final history = await widget.marketService.fetchPriceHistory(cityId: widget.cityId, productTypeId: widget.product.productTypeId);
      if (!mounted) return;
      setState(() {
        _history = history;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load price history.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Price history', style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            if (_loading)
              const Center(child: Padding(padding: EdgeInsets.symmetric(vertical: 12), child: CircularProgressIndicator()))
            else if (_error != null)
              Text(_error!, style: TextStyle(color: theme.colorScheme.error))
            else if (_history.isEmpty)
              const Text('No price history recorded yet.')
            else
              for (final point in _history.reversed)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    children: [
                      SizedBox(width: 56, child: Text('#${point.tick}', style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant))),
                      Expanded(
                        child: Text(
                          AppNumberFormat.money(point.clearingPrice, languageCode: languageCode),
                          style: theme.textTheme.bodySmall?.copyWith(fontWeight: FontWeight.w600),
                        ),
                      ),
                      Expanded(child: Text('Sold ${point.totalVolume.round()}', style: theme.textTheme.bodySmall)),
                      Text('${point.sellerCount} sellers', style: theme.textTheme.bodySmall),
                    ],
                  ),
                ),
          ],
        ),
      ),
    );
  }
}
