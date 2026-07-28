// Ported from `projects/frontend/src/views/MarketingAnalyticsView.vue` —
// factored out of `market_screens.dart` to keep that file under the
// 500-line budget (mechanical move, not a behavior change). Trimmed: shows
// the raw per-unit rows table without `MarketingAnalyticsContent.vue`'s
// trend/recommendation badge styling.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'market_models.dart';
import 'market_service.dart';

class MarketingAnalyticsScreen extends StatefulWidget {
  const MarketingAnalyticsScreen({super.key, GraphQlService? graphQlService, MarketService? marketService})
    : _injectedGraphQlService = graphQlService,
      _injectedMarketService = marketService;

  final GraphQlService? _injectedGraphQlService;
  final MarketService? _injectedMarketService;

  @override
  State<MarketingAnalyticsScreen> createState() => _MarketingAnalyticsScreenState();
}

class _MarketingAnalyticsScreenState extends State<MarketingAnalyticsScreen> {
  late final MarketService _service;

  bool _loading = true;
  String? _error;
  List<Map<String, String>> _companies = const [];
  String? _companyId;
  CampaignAnalytics? _analytics;

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
      final companies = await _service.fetchMyCompanies();
      if (!mounted) return;
      final companyId = _companyId ?? (companies.isNotEmpty ? companies.first['id'] : null);
      CampaignAnalytics? analytics;
      if (companyId != null) analytics = await _service.fetchCampaignAnalytics(companyId);
      if (!mounted) return;
      setState(() {
        _companies = companies;
        _companyId = companyId;
        _analytics = analytics;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load marketing analytics. Please try again.';
        _loading = false;
      });
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

    final analytics = _analytics;
    final theme = Theme.of(context);

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Marketing Analytics', style: theme.textTheme.headlineSmall),
        if (_companies.length > 1) ...[
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            initialValue: _companyId,
            decoration: const InputDecoration(labelText: 'Company'),
            items: [for (final company in _companies) DropdownMenuItem(value: company['id'], child: Text(company['name']!))],
            onChanged: (value) {
              setState(() => _companyId = value);
              _load();
            },
          ),
        ],
        const SizedBox(height: 16),
        if (analytics == null)
          const Text('No campaign data available.')
        else ...[
          Card(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Revenue: ${analytics.totalRevenue.toStringAsFixed(0)} · Marketing spend: ${analytics.totalMarketingSpend.toStringAsFixed(0)}'),
                  if (analytics.bestPerformingProduct != null) Text('Top product: ${analytics.bestPerformingProduct}'),
                  if (analytics.globalRecommendation != null) Text(analytics.globalRecommendation!),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          for (final row in analytics.rows)
            Card(
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text('${row.productName} · ${row.buildingName}'),
                subtitle: Text('${row.cityName} · awareness ${(row.brandAwareness * 100).toStringAsFixed(0)}% · quality ${(row.brandQuality * 100).toStringAsFixed(0)}%'),
                trailing: Text(row.revenueLastTicks.toStringAsFixed(0)),
              ),
            ),
        ],
      ],
    );
  }
}
