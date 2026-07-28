// Ported from `projects/frontend/src/views/MarketIntelligenceView.vue`,
// `EnergyMarketView.vue`, `GlobalEventsPanel.vue`, and
// `MarketingAnalyticsView.vue`.
//
// Market Dashboard (`MarketDashboardView.vue`) lives in its own
// `market_dashboard_screen.dart` — large enough (plus its price-history
// panel) to warrant the same dedicated-file treatment as other split
// screens.
//
// Deliberately trimmed (documented per-screen below): Energy Market
// fetches all cities' listings in parallel and filters client-side instead
// of the web's per-city N+1 query loop — same data, better mobile network
// behavior; Global Events' admin "Trigger Event" button is a
// non-functional stub even on the web, so it's not ported at all;
// Marketing Analytics shows the raw per-unit rows table without
// `MarketingAnalyticsContent.vue`'s trend/recommendation badge styling.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'market_models.dart';
import 'market_service.dart';

export 'market_dashboard_screen.dart' show MarketDashboardScreen;
export 'marketing_analytics_screen.dart' show MarketingAnalyticsScreen;

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

