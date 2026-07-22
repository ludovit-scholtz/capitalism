// Ported from `projects/frontend/src/components/cityTabs/*.vue`. On the
// web these are nested tab components sharing one parent view's fetched
// state (`CityMapView.vue`); here each is its own go_router screen and
// fetches its own data — a reasonable adaptation to this app's routing
// model, not a functional trim.
//
// Deliberately trimmed (documented per-tab below):
// - Economy tab: skips the economic-cycle/weather/power-balance/economic-
//   history dashboards (`EconomyCycleWidget`, `CityPowerPlanningSection`,
//   `HealthIndicatorsPanel`) — five distinct analytics subsystems, shown
//   only as basic city stats here.
// - Buildings tab: the interactive map (`CityMapContent.vue`) is a plain
//   sortable lot list instead — consistent with World Map/Buy Building's
//   list-based lot pickers elsewhere in this app.
// - Market tab: shows the city's resource-abundance list (already fetched
//   for the Cities screen) rather than porting `CityDemandPanel`'s
//   top-selling-products analytics or `CityMediaHousesSection`.

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import '../buildings/buy_building_models.dart';
import '../cities/cities_models.dart';
import 'city_tab_models.dart';
import 'city_tab_service.dart';

abstract class _CityTabScreen extends StatefulWidget {
  const _CityTabScreen({super.key, required this.cityId, this.graphQlService, this.cityTabService});

  final String cityId;
  final GraphQlService? graphQlService;
  final CityTabService? cityTabService;
}

abstract class _CityTabScreenState<T extends _CityTabScreen> extends State<T> {
  late final CityTabService service;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget.graphQlService ?? GraphQlService(auth);
    service = widget.cityTabService ?? CityTabService(graphQlService);
  }

  Widget buildLoading() => const Center(child: CircularProgressIndicator());

  Widget buildError(String message, VoidCallback onRetry) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [Text(message), const SizedBox(height: 12), OutlinedButton(onPressed: onRetry, child: const Text('Try again'))],
      ),
    ),
  );
}

// ── Overview ─────────────────────────────────────────────────────────────

class CityOverviewScreen extends _CityTabScreen {
  const CityOverviewScreen({super.key, required super.cityId, super.graphQlService, super.cityTabService});

  @override
  State<CityOverviewScreen> createState() => _CityOverviewScreenState();
}

class _CityOverviewScreenState extends _CityTabScreenState<CityOverviewScreen> {
  bool _loading = true;
  String? _error;
  City? _city;
  List<CityLot> _lots = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([service.fetchCity(widget.cityId), service.fetchLots(widget.cityId)]);
      if (!mounted) return;
      setState(() {
        _city = results[0] as City?;
        _lots = results[1] as List<CityLot>;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load this city. Please try again.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return buildLoading();
    if (_error != null) return buildError(_error!, _load);
    final city = _city;
    if (city == null) return const Center(child: Text('City not found.'));

    final availableLots = _lots.where((lot) => lot.isAvailable).length;
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text(city.name, style: Theme.of(context).textTheme.headlineSmall),
        Text('${city.countryCode} · ${city.currencyCode}', style: Theme.of(context).textTheme.bodyMedium),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(child: _StatCard(label: 'Population', value: formatPopulation(city.population))),
            const SizedBox(width: 8),
            Expanded(child: _StatCard(label: 'Base salary', value: '${city.baseSalaryPerManhour.toStringAsFixed(1)}/h')),
          ],
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(child: _StatCard(label: 'Available lots', value: '$availableLots')),
            const SizedBox(width: 8),
            Expanded(child: _StatCard(label: 'Total lots', value: '${_lots.length}')),
          ],
        ),
      ],
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [Text(label, style: theme.textTheme.labelSmall), Text(value, style: theme.textTheme.titleMedium)],
        ),
      ),
    );
  }
}

// ── Economy ──────────────────────────────────────────────────────────────

class CityEconomyScreen extends _CityTabScreen {
  const CityEconomyScreen({super.key, required super.cityId, super.graphQlService, super.cityTabService});

  @override
  State<CityEconomyScreen> createState() => _CityEconomyScreenState();
}

class _CityEconomyScreenState extends _CityTabScreenState<CityEconomyScreen> {
  bool _loading = true;
  String? _error;
  City? _city;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final city = await service.fetchCity(widget.cityId);
      if (!mounted) return;
      setState(() {
        _city = city;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load this city. Please try again.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return buildLoading();
    if (_error != null) return buildError(_error!, _load);
    final city = _city;
    if (city == null) return const Center(child: Text('City not found.'));

    final theme = Theme.of(context);
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Economy', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 8),
        Text('Currency: ${city.currencyCode}'),
        Text('Population: ${formatPopulation(city.population)}'),
        Text('Base salary: ${city.baseSalaryPerManhour.toStringAsFixed(1)} ${city.currencyCode}/h'),
        const SizedBox(height: 16),
        Text(
          'Detailed economic cycle, weather, and power-grid dashboards are not yet available on mobile.',
          style: theme.textTheme.bodySmall,
        ),
      ],
    );
  }
}

// ── Buildings ────────────────────────────────────────────────────────────

class CityBuildingsScreen extends _CityTabScreen {
  const CityBuildingsScreen({super.key, required super.cityId, super.graphQlService, super.cityTabService});

  @override
  State<CityBuildingsScreen> createState() => _CityBuildingsScreenState();
}

class _CityBuildingsScreenState extends _CityTabScreenState<CityBuildingsScreen> {
  bool _loading = true;
  String? _error;
  List<CityLot> _lots = const [];
  bool _showAvailableOnly = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final lots = await service.fetchLots(widget.cityId);
      if (!mounted) return;
      setState(() {
        _lots = lots;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load buildings. Please try again.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return buildLoading();
    if (_error != null) return buildError(_error!, _load);

    final lots = _showAvailableOnly ? _lots.where((lot) => lot.isAvailable).toList() : _lots;
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Buildings & Lots', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        SwitchListTile(
          title: const Text('Show available lots only'),
          value: _showAvailableOnly,
          onChanged: (value) => setState(() => _showAvailableOnly = value),
        ),
        for (final lot in lots)
          Card(
            key: ValueKey('city-lot-${lot.id}'),
            margin: const EdgeInsets.only(bottom: 8),
            child: ListTile(
              title: Text(lot.name ?? lot.district ?? 'Lot'),
              subtitle: Text(lot.isAvailable ? 'Available · ${lot.price.toStringAsFixed(0)}' : 'Owned'),
              trailing: !lot.isAvailable && lot.buildingId != null
                  ? IconButton(icon: const FaIcon(AppIcons.arrowRight, size: 16), onPressed: () => context.go('/building/${lot.buildingId}'))
                  : null,
            ),
          ),
      ],
    );
  }
}

// ── Market ───────────────────────────────────────────────────────────────

class CityMarketScreen extends _CityTabScreen {
  const CityMarketScreen({super.key, required super.cityId, super.graphQlService, super.cityTabService});

  @override
  State<CityMarketScreen> createState() => _CityMarketScreenState();
}

class _CityMarketScreenState extends _CityTabScreenState<CityMarketScreen> {
  bool _loading = true;
  String? _error;
  City? _city;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final city = await service.fetchCity(widget.cityId);
      if (!mounted) return;
      setState(() {
        _city = city;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the market. Please try again.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return buildLoading();
    if (_error != null) return buildError(_error!, _load);
    final city = _city;
    if (city == null) return const Center(child: Text('City not found.'));

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Local Resources', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        if (city.resources.isEmpty)
          const Text('No local resources recorded for this city.')
        else
          for (final resource in [...city.resources]..sort((a, b) => b.abundance.compareTo(a.abundance)))
            Card(
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                leading: Text(cityResourceIcon(resource.resourceSlug), style: const TextStyle(fontSize: 20)),
                title: Text(resource.resourceName),
                trailing: Text('${(resource.abundance * 100).round()}%'),
              ),
            ),
      ],
    );
  }
}

// ── Contracts ────────────────────────────────────────────────────────────

class CityContractsScreen extends _CityTabScreen {
  const CityContractsScreen({super.key, required super.cityId, super.graphQlService, super.cityTabService});

  @override
  State<CityContractsScreen> createState() => _CityContractsScreenState();
}

class _CityContractsScreenState extends _CityTabScreenState<CityContractsScreen> {
  bool _loading = true;
  String? _error;
  List<GovernmentContractCard> _contracts = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final contracts = await service.fetchOpenContracts(widget.cityId);
      if (!mounted) return;
      setState(() {
        _contracts = contracts;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load contracts. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _openBidDialog(GovernmentContractCard contract) async {
    final companies = await service.fetchMyCompanies();
    if (!mounted) return;
    if (companies.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('You need a company to bid.')));
      return;
    }
    String companyId = companies.first['id']!;
    final priceController = TextEditingController();
    ContractEligibility? eligibility = await service.fetchEligibility(contractId: contract.id, companyId: companyId);
    if (!mounted) return;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: Text('Bid on ${contract.title}'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String>(
                initialValue: companyId,
                decoration: const InputDecoration(labelText: 'Company'),
                items: [for (final company in companies) DropdownMenuItem(value: company['id'], child: Text(company['name']!))],
                onChanged: (value) async {
                  companyId = value ?? companyId;
                  final updated = await service.fetchEligibility(contractId: contract.id, companyId: companyId);
                  setDialogState(() => eligibility = updated);
                },
              ),
              TextField(
                controller: priceController,
                decoration: const InputDecoration(labelText: 'Bid price per unit'),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
              ),
              if (eligibility != null && !eligibility!.isEligible)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(eligibility!.reasonMessage ?? 'Not eligible', style: TextStyle(color: Theme.of(dialogContext).colorScheme.error)),
                ),
            ],
          ),
          actions: [
            TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
            FilledButton(
              onPressed: (eligibility?.isEligible ?? true) ? () => Navigator.of(dialogContext).pop(true) : null,
              child: const Text('Submit bid'),
            ),
          ],
        ),
      ),
    );
    if (confirmed != true) return;

    try {
      await service.submitBid(
        contractId: contract.id,
        companyId: companyId,
        bidPricePerUnit: double.tryParse(priceController.text) ?? 0,
        estimatedDeliveryTick: contract.deadlineTick,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Bid submitted.')));
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not submit the bid.')));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return buildLoading();
    if (_error != null) return buildError(_error!, _load);

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Government Contracts', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        if (_contracts.isEmpty)
          const Text('No open contracts right now.')
        else
          for (final contract in _contracts)
            Card(
              key: ValueKey('gov-contract-${contract.id}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(contract.title),
                subtitle: Text('${contract.productName} · ${contract.quantityRequired.toStringAsFixed(0)} units · ${contract.bidCount} bids'),
                trailing: FilledButton(onPressed: () => _openBidDialog(contract), child: const Text('Bid')),
              ),
            ),
      ],
    );
  }
}

// ── Competitors ──────────────────────────────────────────────────────────

class CityCompetitorsScreen extends _CityTabScreen {
  const CityCompetitorsScreen({super.key, required super.cityId, super.graphQlService, super.cityTabService});

  @override
  State<CityCompetitorsScreen> createState() => _CityCompetitorsScreenState();
}

class _CityCompetitorsScreenState extends _CityTabScreenState<CityCompetitorsScreen> {
  bool _loading = true;
  String? _error;
  List<CityCompetitor> _competitors = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final competitors = await service.fetchCompetitors(widget.cityId);
      if (!mounted) return;
      setState(() {
        _competitors = competitors;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load competitors. Please try again.';
        _loading = false;
      });
    }
  }

  String _trendIcon(String trend) {
    switch (trend) {
      case 'UP':
        return '▲';
      case 'DOWN':
        return '▼';
      default:
        return '–';
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return buildLoading();
    if (_error != null) return buildError(_error!, _load);

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Competitors', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        if (_competitors.isEmpty)
          const Text('No active companies in this city yet.')
        else
          for (final competitor in _competitors)
            Card(
              key: ValueKey('competitor-${competitor.companyId}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(competitor.companyName),
                subtitle: Text('${competitor.buildingCount} buildings · ${competitor.isNpc ? 'NPC' : 'Player'}'),
                trailing: Text('${competitor.marketSharePercent.toStringAsFixed(1)}% ${_trendIcon(competitor.trend)}'),
              ),
            ),
      ],
    );
  }
}
