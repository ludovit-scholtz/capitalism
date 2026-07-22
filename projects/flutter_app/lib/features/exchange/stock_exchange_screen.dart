// Ported from `projects/frontend/src/views/StockExchangeView.vue`.
//
// Deliberately trimmed (documented, not oversights): the web's inline
// per-row expandable trade panel, dividend proposal/voting
// (`proposeDividend`/`voteDividendProposal`), company merger
// (`mergeCompany`), CEO replacement (`replaceCEO`), and the hostile
// takeover dialog are not ported — buying/selling shares lives on the
// Stock Trading screen (`/stock/trade/:companyId`) instead of inline here,
// a reasonable adaptation to mobile's narrower layout rather than a lost
// feature.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'stock_models.dart';
import 'stock_service.dart';

class StockExchangeScreen extends StatefulWidget {
  const StockExchangeScreen({super.key, GraphQlService? graphQlService, StockService? stockService})
    : _injectedGraphQlService = graphQlService,
      _injectedStockService = stockService;

  final GraphQlService? _injectedGraphQlService;
  final StockService? _injectedStockService;

  @override
  State<StockExchangeScreen> createState() => _StockExchangeScreenState();
}

class _StockExchangeScreenState extends State<StockExchangeScreen> {
  late final StockService _service;

  bool _loading = true;
  String? _error;
  List<StockListing> _listings = const [];

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedStockService ?? StockService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final listings = await _service.fetchListings();
      if (!mounted) return;
      setState(() {
        _listings = listings;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the stock exchange. Please try again.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _load, child: const Text('Try again')),
            ],
          ),
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Stock Exchange', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 16),
          if (_listings.isEmpty)
            const Text('No companies are publicly traded yet.')
          else
            for (final listing in _listings)
              Card(
                key: ValueKey('stock-listing-${listing.companyId}'),
                margin: const EdgeInsets.only(bottom: 8),
                child: ListTile(
                  title: Text('${listing.companyName} (${listing.stockSymbol})'),
                  subtitle: Text([if (listing.primaryCityName != null) listing.primaryCityName!, if (listing.primaryIndustry != null) listing.primaryIndustry!].join(' · ')),
                  trailing: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text(listing.sharePrice.toStringAsFixed(2)),
                      Text(
                        '${listing.dailyChangePercent >= 0 ? '+' : ''}${listing.dailyChangePercent.toStringAsFixed(1)}%',
                        style: TextStyle(color: listing.dailyChangePercent >= 0 ? Colors.green : Colors.red),
                      ),
                    ],
                  ),
                  onTap: () => context.go('/stock/trade/${listing.companyId}'),
                ),
              ),
        ],
      ),
    );
  }
}
