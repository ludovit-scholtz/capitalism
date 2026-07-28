// Ported from `projects/frontend/src/components/cityTabs/*.vue`. On the
// web these are nested tab components sharing one parent view's fetched
// state (`CityMapView.vue`); here each is its own go_router screen and
// fetches its own data — a reasonable adaptation to this app's routing
// model, not a functional trim. The shared loading/error/service bootstrap
// lives in `city_tab_base.dart`; `CityEconomyScreen` (economic-cycle,
// weather, power-grid, and health dashboards) lives in its own
// `city_economy_screen.dart` and is re-exported below to keep this file
// under the 500-line budget.
//
// Deliberately trimmed (documented per-tab below):
// - Buildings tab: renders a real interactive map (`CapitalismMapView`,
//   mirroring `CityMapContent.vue`'s Leaflet setup) alongside the existing
//   sortable lot list (kept as a thumb-friendly selector, same pattern as
//   World Map/Buy Building). Marker coloring is a simplified two-state
//   available/owned (web additionally distinguishes "yours" vs "NPC-owned"
//   via `playerCompanyIds`/`npcCompanyIds`, which this app's `CityLot`
//   model doesn't carry) — a scoped simplification, not an oversight.
// - Market tab: shows the city's resource-abundance list (already fetched
//   for the Cities screen) rather than porting `CityDemandPanel`'s
//   top-selling-products analytics or `CityMediaHousesSection`.

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart' show TileProvider;
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/theme/app_icons.dart';
import '../../core/widgets/capitalism_map_view.dart';
import '../buildings/buy_building_models.dart';
import '../cities/cities_models.dart';
import 'city_tab_base.dart';
import 'city_tab_models.dart';

export 'city_economy_screen.dart' show CityEconomyScreen;
export 'city_market_screen.dart' show CityMarketScreen;

// ── Overview ─────────────────────────────────────────────────────────────

class CityOverviewScreen extends CityTabScreen {
  const CityOverviewScreen({super.key, required super.cityId, super.graphQlService, super.cityTabService});

  @override
  State<CityOverviewScreen> createState() => _CityOverviewScreenState();
}

class _CityOverviewScreenState extends CityTabScreenState<CityOverviewScreen> {
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
    final languageCode = context.watch<LocaleState>().languageCode;
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text(city.name, style: Theme.of(context).textTheme.headlineSmall),
        Text('${city.countryCode} · ${city.currencyCode}', style: Theme.of(context).textTheme.bodyMedium),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(child: _StatCard(label: 'Population', value: formatPopulation(city.population, languageCode))),
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

// ── Buildings ────────────────────────────────────────────────────────────

class CityBuildingsScreen extends CityTabScreen {
  const CityBuildingsScreen({
    super.key,
    required super.cityId,
    super.graphQlService,
    super.cityTabService,
    this.tileProvider,
  });

  /// Injectable so widget tests never hit real OSM tile servers — see
  /// `test/support/fake_tile_provider.dart`.
  final TileProvider? tileProvider;

  @override
  State<CityBuildingsScreen> createState() => _CityBuildingsScreenState();
}

class _CityBuildingsScreenState extends CityTabScreenState<CityBuildingsScreen> {
  bool _loading = true;
  String? _error;
  List<CityLot> _lots = const [];
  bool _showAvailableOnly = false;
  String? _selectedLotId;

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
    CityLot? selected;
    for (final lot in lots) {
      if (lot.id == _selectedLotId) {
        selected = lot;
        break;
      }
    }

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
        if (lots.isNotEmpty) ...[
          ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: SizedBox(
              height: 280,
              child: CapitalismMapView(
                tileProvider: widget.tileProvider,
                flyToTarget: selected != null ? LatLng(selected.latitude, selected.longitude) : null,
                markers: [
                  for (final lot in lots)
                    CapitalismMapMarker(
                      id: lot.id,
                      position: LatLng(lot.latitude, lot.longitude),
                      color: lot.id == _selectedLotId
                          ? CapitalismMapColors.selected
                          : (lot.isAvailable ? CapitalismMapColors.available : CapitalismMapColors.ownedByOther),
                      size: lot.id == _selectedLotId ? 20 : 14,
                      tooltip: lot.name ?? lot.district,
                      onTap: () => setState(() => _selectedLotId = lot.id),
                    ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
        ],
        for (final lot in lots)
          Card(
            key: ValueKey('city-lot-${lot.id}'),
            color: lot.id == _selectedLotId ? Theme.of(context).colorScheme.primaryContainer : null,
            margin: const EdgeInsets.only(bottom: 8),
            child: ListTile(
              title: Text(lot.name ?? lot.district ?? 'Lot'),
              subtitle: Text(lot.isAvailable ? 'Available · ${lot.price.toStringAsFixed(0)}' : 'Owned'),
              onTap: () => setState(() => _selectedLotId = lot.id),
              trailing: !lot.isAvailable && lot.buildingId != null
                  ? IconButton(icon: const FaIcon(AppIcons.arrowRight, size: 16), onPressed: () => context.go('/building/${lot.buildingId}'))
                  : null,
            ),
          ),
      ],
    );
  }
}

// ── Contracts ────────────────────────────────────────────────────────────

class CityContractsScreen extends CityTabScreen {
  const CityContractsScreen({super.key, required super.cityId, super.graphQlService, super.cityTabService});

  @override
  State<CityContractsScreen> createState() => _CityContractsScreenState();
}

class _CityContractsScreenState extends CityTabScreenState<CityContractsScreen> {
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

class CityCompetitorsScreen extends CityTabScreen {
  const CityCompetitorsScreen({super.key, required super.cityId, super.graphQlService, super.cityTabService});

  @override
  State<CityCompetitorsScreen> createState() => _CityCompetitorsScreenState();
}

class _CityCompetitorsScreenState extends CityTabScreenState<CityCompetitorsScreen> {
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
