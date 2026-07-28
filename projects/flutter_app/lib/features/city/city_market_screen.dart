// Ported from `projects/frontend/src/components/cityTabs/CityMarketTab.vue`
// — factored out of `city_tab_screens.dart` to keep that file under the
// 500-line budget (mechanical move, not a behavior change). The web tab
// renders only `CityDemandPanel` + `CityMediaHousesSection`; this screen
// additionally keeps a "Local Resources" abundance list above them, a
// Flutter-only section that predates this port and is kept as a superset
// rather than removed.

import 'dart:async';

import 'package:flutter/material.dart';

import '../buildings/building_panel_models.dart';
import '../buildings/building_panel_service.dart';
import '../cities/cities_models.dart';
import 'city_demand_panel.dart';
import 'city_market_models.dart';
import 'city_market_service.dart';
import 'city_media_houses_section.dart';
import 'city_tab_base.dart';

class CityMarketScreen extends CityTabScreen {
  const CityMarketScreen({
    super.key,
    required super.cityId,
    super.graphQlService,
    super.cityTabService,
    this.marketService,
    this.buildingPanelService,
  });

  final CityMarketService? marketService;
  final BuildingPanelService? buildingPanelService;

  @override
  State<CityMarketScreen> createState() => _CityMarketScreenState();
}

class _CityMarketScreenState extends CityTabScreenState<CityMarketScreen> {
  late final CityMarketService _marketService;
  late final BuildingPanelService _buildingPanelService;

  bool _loading = true;
  String? _error;
  City? _city;
  CityDemandSummary? _demandSummary;
  bool _demandLoading = true;
  List<CityMediaHouse> _mediaHouses = const [];
  bool _mediaHousesLoading = true;

  @override
  void initState() {
    super.initState();
    _marketService = widget.marketService ?? CityMarketService(graphQlService);
    _buildingPanelService = widget.buildingPanelService ?? BuildingPanelService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _demandLoading = true;
      _mediaHousesLoading = true;
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
      return;
    }

    // The demand summary and media-houses panels are supplementary — a
    // failure loading either shouldn't block the core resources list from
    // showing, matching the web's independently-loaded `CityDemandPanel`
    // and `CityMediaHousesSection` components.
    unawaited(
      _marketService
          .fetchDemandSummary(widget.cityId)
          .then((summary) {
            if (!mounted) return;
            setState(() {
              _demandSummary = summary;
              _demandLoading = false;
            });
          })
          .catchError((_) {
            if (!mounted) return;
            setState(() => _demandLoading = false);
          }),
    );
    unawaited(
      _buildingPanelService
          .fetchCityMediaHouses(widget.cityId)
          .then((houses) {
            if (!mounted) return;
            setState(() {
              _mediaHouses = houses;
              _mediaHousesLoading = false;
            });
          })
          .catchError((_) {
            if (!mounted) return;
            setState(() => _mediaHousesLoading = false);
          }),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return buildLoading();
    if (_error != null) return buildError(_error!, _load);
    final city = _city;
    if (city == null) return const Center(child: Text('City not found.'));

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
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
          const SizedBox(height: 24),
          CityDemandPanel(summary: _demandSummary, loading: _demandLoading),
          const SizedBox(height: 24),
          CityMediaHousesSection(mediaHouses: _mediaHouses, loading: _mediaHousesLoading),
        ],
      ),
    );
  }
}
