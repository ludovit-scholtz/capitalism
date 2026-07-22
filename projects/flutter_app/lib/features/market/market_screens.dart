// Ported from `projects/frontend/src/views/MarketIntelligenceView.vue`,
// `MarketDashboardView.vue`, `EnergyMarketView.vue`, `GlobalEventsPanel.vue`,
// and `MarketingAnalyticsView.vue`.
//
// Deliberately trimmed (documented per-screen below): Market Dashboard
// skips the price-history chart panel (list instead, no charting
// dependency); Energy Market fetches all cities' listings in parallel and
// filters client-side instead of the web's per-city N+1 query loop — same
// data, better mobile network behavior; Global Events' admin "Trigger
// Event" button is a non-functional stub even on the web, so it's not
// ported at all; Marketing Analytics shows the raw per-unit rows table
// without `MarketingAnalyticsContent.vue`'s trend/recommendation badge
// styling.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'market_models.dart';
import 'market_service.dart';

class MarketIntelligenceScreen extends StatefulWidget {
  const MarketIntelligenceScreen({super.key, GraphQlService? graphQlService, MarketService? marketService})
    : _injectedGraphQlService = graphQlService,
      _injectedMarketService = marketService;

  final GraphQlService? _injectedGraphQlService;
  final MarketService? _injectedMarketService;

  @override
  State<MarketIntelligenceScreen> createState() => _MarketIntelligenceScreenState();
}

class _MarketIntelligenceScreenState extends State<MarketIntelligenceScreen> {
  late final MarketService _service;

  bool _loading = true;
  String? _error;
  List<Map<String, String>> _cities = const [];
  String? _cityId;
  MarketIntelligence? _intel;

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
      MarketIntelligence? intel;
      if (cityId != null) intel = await _service.fetchMarketIntelligence(cityId);
      if (!mounted) return;
      setState(() {
        _cities = cities;
        _cityId = cityId;
        _intel = intel;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load market intelligence. Please try again.';
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

    final intel = _intel;
    final theme = Theme.of(context);

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Market Intelligence', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          initialValue: _cityId,
          decoration: const InputDecoration(labelText: 'City'),
          items: [for (final city in _cities) DropdownMenuItem(value: city['id'], child: Text(city['name']!))],
          onChanged: (value) {
            setState(() => _cityId = value);
            _load();
          },
        ),
        const SizedBox(height: 16),
        if (intel == null || intel.products.isEmpty)
          const Text('No market data available for this city.')
        else
          for (final product in intel.products)
            Card(
              margin: const EdgeInsets.only(bottom: 8),
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(product.productName, style: theme.textTheme.titleSmall),
                    Text('Weekly volume: ${product.totalWeeklySalesVolume.toStringAsFixed(0)}'),
                    for (final seller in product.sellers.take(5))
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text('#${seller.rank} ${seller.displayName} — ${seller.askingPricePerUnit.toStringAsFixed(2)} (${(seller.marketShare * 100).toStringAsFixed(0)}%)'),
                      ),
                  ],
                ),
              ),
            ),
      ],
    );
  }
}

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

class EnergyMarketScreen extends StatefulWidget {
  const EnergyMarketScreen({super.key, GraphQlService? graphQlService, MarketService? marketService})
    : _injectedGraphQlService = graphQlService,
      _injectedMarketService = marketService;

  final GraphQlService? _injectedGraphQlService;
  final MarketService? _injectedMarketService;

  @override
  State<EnergyMarketScreen> createState() => _EnergyMarketScreenState();
}

class _EnergyMarketScreenState extends State<EnergyMarketScreen> {
  late final MarketService _service;

  bool _loading = true;
  String? _error;
  List<Map<String, String>> _cities = const [];
  String? _cityFilter;
  List<EnergyListing> _listings = const [];
  List<Map<String, String>> _myPlants = const [];

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
      final listingLists = await Future.wait(cities.map((c) => _service.fetchEnergyMarket(c['id']!)));
      final myPlants = await _service.fetchMyPowerPlants();
      if (!mounted) return;
      setState(() {
        _cities = cities;
        _listings = listingLists.expand((l) => l).toList();
        _myPlants = myPlants;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the energy market. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _openListDialog() async {
    if (_myPlants.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('You need a power plant to list energy for sale.')));
      return;
    }
    String plantId = _myPlants.first['id']!;
    final priceController = TextEditingController(text: '0.1');
    final capacityController = TextEditingController(text: '100');
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('List surplus energy'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String>(
                initialValue: plantId,
                decoration: const InputDecoration(labelText: 'Power plant'),
                items: [for (final plant in _myPlants) DropdownMenuItem(value: plant['id'], child: Text(plant['name']!))],
                onChanged: (value) => setDialogState(() => plantId = value ?? plantId),
              ),
              TextField(controller: priceController, decoration: const InputDecoration(labelText: 'Price per kWh'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
              TextField(controller: capacityController, decoration: const InputDecoration(labelText: 'Capacity (kW)'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
            ],
          ),
          actions: [
            TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
            FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('List')),
          ],
        ),
      ),
    );
    if (confirmed != true) return;

    try {
      await _service.listEnergyForSale(
        buildingId: plantId,
        pricePerKwhLocal: double.tryParse(priceController.text) ?? 0,
        capacityKw: double.tryParse(capacityController.text) ?? 0,
      );
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not list energy for sale.')));
      }
    }
  }

  Future<void> _cancelListing(EnergyListing listing) async {
    try {
      await _service.cancelEnergyListing(listing.listingId);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not cancel this listing.')));
      }
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

    final myPlantBuildingIds = _myPlants.map((p) => p['id']).toSet();
    final listings = _cityFilter == null ? _listings : _listings.where((l) => l.cityId == _cityFilter).toList();
    final theme = Theme.of(context);

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Row(
          children: [
            Expanded(child: Text('Energy Market', style: theme.textTheme.headlineSmall)),
            FilledButton(onPressed: _openListDialog, child: const Text('List Surplus')),
          ],
        ),
        const SizedBox(height: 12),
        DropdownButtonFormField<String?>(
          initialValue: _cityFilter,
          decoration: const InputDecoration(labelText: 'City'),
          items: [
            const DropdownMenuItem(value: null, child: Text('All cities')),
            for (final city in _cities) DropdownMenuItem(value: city['id'], child: Text(city['name']!)),
          ],
          onChanged: (value) => setState(() => _cityFilter = value),
        ),
        const SizedBox(height: 16),
        if (listings.isEmpty)
          const Text('No energy listings available.')
        else
          for (final listing in listings)
            Card(
              key: ValueKey('energy-${listing.listingId}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text('${listing.buildingName} (${listing.plantType ?? 'PLANT'})'),
                subtitle: Text('${listing.availableKw.toStringAsFixed(0)} kW available at ${listing.pricePerKwhLocal.toStringAsFixed(3)}/kWh'),
                trailing: myPlantBuildingIds.contains(listing.buildingId)
                    ? TextButton(onPressed: () => _cancelListing(listing), child: const Text('Cancel'))
                    : const Chip(label: Text('For sale')),
              ),
            ),
      ],
    );
  }
}

class GlobalEventsScreen extends StatefulWidget {
  const GlobalEventsScreen({super.key, GraphQlService? graphQlService, MarketService? marketService})
    : _injectedGraphQlService = graphQlService,
      _injectedMarketService = marketService;

  final GraphQlService? _injectedGraphQlService;
  final MarketService? _injectedMarketService;

  @override
  State<GlobalEventsScreen> createState() => _GlobalEventsScreenState();
}

class _GlobalEventsScreenState extends State<GlobalEventsScreen> {
  late final MarketService _service;

  String _tab = 'active';
  bool _loading = true;
  String? _error;
  List<GlobalEvent> _active = const [];
  List<GlobalEvent> _history = const [];

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
      final results = await Future.wait([_service.fetchActiveGlobalEvents(), _service.fetchGlobalEventHistory()]);
      if (!mounted) return;
      setState(() {
        _active = results[0];
        _history = results[1];
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load global events. Please try again.';
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

    final events = _tab == 'active' ? _active : _history;
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Global Events', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(child: ChoiceChip(label: const Text('Active'), selected: _tab == 'active', onSelected: (_) => setState(() => _tab = 'active'))),
            const SizedBox(width: 8),
            Expanded(child: ChoiceChip(label: const Text('History'), selected: _tab == 'history', onSelected: (_) => setState(() => _tab = 'history'))),
          ],
        ),
        const SizedBox(height: 16),
        if (events.isEmpty)
          Text(_tab == 'active' ? 'No active events right now.' : 'No past events.')
        else
          for (final event in events)
            Card(
              key: ValueKey('event-${event.id}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(event.title),
                subtitle: Text(event.description ?? event.eventType),
                trailing: Chip(label: Text(event.severity)),
              ),
            ),
      ],
    );
  }
}

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
