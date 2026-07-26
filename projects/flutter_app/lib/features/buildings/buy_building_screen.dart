// Ported from `projects/frontend/src/views/BuyBuildingView.vue` +
// `BuyBuildingSteps.vue`.
//
// The lot step now renders a real interactive map (`CapitalismMapView`) with
// lot markers, the player's existing-building markers, and a
// distance-to-existing-buildings list (`buy_building_distance.dart`, real
// haversine, matching web's `computeDistanceKm`/`nearestBuildingsForLot`) —
// the plain sortable list is kept alongside it as a precise, thumb-friendly
// alternative to tapping small map pins on a phone (mobile-UX improvement,
// not a web-parity requirement).
//
// Deliberately trimmed from the web (documented, not oversights):
// - No BANK-specific follow-up (`setBankRates`/`initiateBaseDeposit`), no
//   POWER_PLANT subtype or MEDIA_HOUSE channel-type selection — those
//   inputs are accepted by the mutation but ignored for other building
//   types, and this first pass only covers the common path.

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart' show TileProvider;
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import '../../core/widgets/capitalism_map_view.dart';
import 'buy_building_distance.dart';
import 'buy_building_models.dart';
import 'buy_building_service.dart';

class BuyBuildingScreen extends StatefulWidget {
  const BuyBuildingScreen({
    super.key,
    required this.companyId,
    GraphQlService? graphQlService,
    BuyBuildingService? buyBuildingService,
    this.tileProvider,
  }) : _injectedGraphQlService = graphQlService,
       _injectedBuyBuildingService = buyBuildingService;

  final String companyId;
  final GraphQlService? _injectedGraphQlService;
  final BuyBuildingService? _injectedBuyBuildingService;

  /// Injectable so widget tests never hit real OSM tile servers — see
  /// `test/support/fake_tile_provider.dart`.
  final TileProvider? tileProvider;

  @override
  State<BuyBuildingScreen> createState() => _BuyBuildingScreenState();
}

class _BuyBuildingScreenState extends State<BuyBuildingScreen> {
  late final BuyBuildingService _service;

  bool _loading = true;
  String? _error;
  List<Map<String, String>> _cities = const [];

  int _step = 0;
  String? _cityId;
  String? _buildingType;
  CityLot? _selectedLot;
  final _nameController = TextEditingController();

  bool _lotsLoading = false;
  List<CityLot> _lots = const [];
  List<OwnedBuildingLocation> _myBuildingLocations = const [];
  bool _purchasing = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedBuyBuildingService ?? BuyBuildingService(graphQlService);
    _load();
  }

  @override
  void dispose() {
    _nameController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final cities = await _service.fetchCities();
      if (!mounted) return;
      setState(() {
        _cities = cities;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load cities. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _loadLots() async {
    if (_cityId == null) return;
    setState(() => _lotsLoading = true);
    try {
      final lots = await _service.fetchLots(_cityId!);
      if (!mounted) return;
      lots.sort((a, b) => a.price.compareTo(b.price));
      setState(() {
        _lots = lots;
        _lotsLoading = false;
      });
    } catch (_) {
      if (mounted) setState(() => _lotsLoading = false);
    }
    try {
      final buildings = await _service.fetchMyBuildingLocations(widget.companyId);
      if (mounted) setState(() => _myBuildingLocations = buildings);
    } catch (_) {
      // Best-effort — missing "nearest existing buildings" data shouldn't
      // block lot selection itself.
    }
  }

  Future<void> _purchase() async {
    final lot = _selectedLot;
    if (lot == null || _buildingType == null) return;
    setState(() => _purchasing = true);
    try {
      await _service.purchaseLot(
        companyId: widget.companyId,
        lotId: lot.id,
        buildingType: _buildingType!,
        buildingName: _nameController.text.trim().isEmpty ? null : _nameController.text.trim(),
      );
      if (mounted) context.go('/dashboard');
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not purchase this lot.')));
      }
    } finally {
      if (mounted) setState(() => _purchasing = false);
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

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Buy a Building', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 16),
        if (_step == 0) ..._buildCityStep(),
        if (_step == 1) ..._buildTypeStep(),
        if (_step == 2) ..._buildLotStep(),
        if (_step == 3) ..._buildConfirmStep(),
      ],
    );
  }

  List<Widget> _buildCityStep() {
    return [
      Text('1. Choose a city', style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 8),
      for (final city in _cities)
        ListTile(
          key: ValueKey('city-${city['id']}'),
          selected: _cityId == city['id'],
          leading: FaIcon(_cityId == city['id'] ? AppIcons.radioChecked : AppIcons.radioUnchecked, size: 18),
          title: Text(city['name']!),
          onTap: () => setState(() => _cityId = city['id']),
        ),
      const SizedBox(height: 12),
      FilledButton(
        onPressed: _cityId == null
            ? null
            : () {
                setState(() => _step = 1);
              },
        child: const Text('Next'),
      ),
    ];
  }

  List<Widget> _buildTypeStep() {
    return [
      Text('2. Choose a building type', style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 8),
      for (final type in buildingTypes)
        ListTile(
          key: ValueKey('type-$type'),
          selected: _buildingType == type,
          leading: FaIcon(_buildingType == type ? AppIcons.radioChecked : AppIcons.radioUnchecked, size: 18),
          title: Text(type),
          onTap: () => setState(() => _buildingType = type),
        ),
      const SizedBox(height: 12),
      Row(
        children: [
          OutlinedButton(onPressed: () => setState(() => _step = 0), child: const Text('Back')),
          const SizedBox(width: 8),
          FilledButton(
            onPressed: _buildingType == null
                ? null
                : () {
                    setState(() => _step = 2);
                    _loadLots();
                  },
            child: const Text('Next'),
          ),
        ],
      ),
    ];
  }

  List<Widget> _buildLotStep() {
    final suitableLots = _lots.where((lot) => lot.isAvailable && lot.suitableTypes.contains(_buildingType)).toList();
    final cityBuildings = _myBuildingLocations.where((b) => b.cityId == _cityId).toList();
    final selectedLot = _selectedLot;
    final nearest = selectedLot != null
        ? nearestBuildingsForLot<OwnedBuildingLocation>(
            lotLat: selectedLot.latitude,
            lotLng: selectedLot.longitude,
            buildings: cityBuildings,
            latOf: (b) => b.latitude,
            lngOf: (b) => b.longitude,
          )
        : const <NearestBuilding<OwnedBuildingLocation>>[];

    return [
      Text('3. Choose a lot', style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 8),
      if (_lotsLoading)
        const Center(child: CircularProgressIndicator())
      else if (suitableLots.isEmpty)
        const Text('No available lots of this type in this city.')
      else ...[
        ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: SizedBox(
            height: 280,
            child: CapitalismMapView(
              tileProvider: widget.tileProvider,
              flyToTarget: selectedLot != null ? LatLng(selectedLot.latitude, selectedLot.longitude) : null,
              markers: [
                for (final lot in suitableLots)
                  CapitalismMapMarker(
                    id: 'lot-${lot.id}',
                    position: LatLng(lot.latitude, lot.longitude),
                    color: selectedLot?.id == lot.id ? CapitalismMapColors.selected : CapitalismMapColors.available,
                    size: selectedLot?.id == lot.id ? 20 : 14,
                    tooltip: lot.name ?? lot.district,
                    onTap: () => setState(() => _selectedLot = lot),
                  ),
                for (final building in cityBuildings)
                  CapitalismMapMarker(
                    id: 'building-${building.id}',
                    position: LatLng(building.latitude, building.longitude),
                    color: CapitalismMapColors.ownedByOther,
                    size: 18,
                    tooltip: building.name,
                  ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        for (final lot in suitableLots)
          Card(
            key: ValueKey('lot-${lot.id}'),
            color: selectedLot?.id == lot.id ? Theme.of(context).colorScheme.primaryContainer : null,
            child: ListTile(
              selected: selectedLot?.id == lot.id,
              leading: FaIcon(selectedLot?.id == lot.id ? AppIcons.radioChecked : AppIcons.radioUnchecked, size: 18),
              title: Text(lot.name ?? lot.district ?? 'Lot'),
              subtitle: Text(lot.price.toStringAsFixed(0)),
              onTap: () => setState(() => _selectedLot = lot),
            ),
          ),
        if (selectedLot != null && nearest.isNotEmpty) ...[
          const SizedBox(height: 12),
          Text('Nearest existing buildings', style: Theme.of(context).textTheme.labelMedium),
          for (final entry in nearest)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text('${entry.building.name} (${entry.building.type}) · ${formatDistanceKm(entry.distanceKm)}'),
            ),
        ],
      ],
      const SizedBox(height: 12),
      Row(
        children: [
          OutlinedButton(onPressed: () => setState(() => _step = 1), child: const Text('Back')),
          const SizedBox(width: 8),
          FilledButton(
            onPressed: _selectedLot == null ? null : () => setState(() => _step = 3),
            child: const Text('Next'),
          ),
        ],
      ),
    ];
  }

  List<Widget> _buildConfirmStep() {
    return [
      Text('4. Confirm purchase', style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 8),
      Text('Lot: ${_selectedLot?.name ?? _selectedLot?.district ?? _selectedLot?.id}'),
      Text('Price: ${_selectedLot?.price.toStringAsFixed(0)}'),
      Text('Type: $_buildingType'),
      const SizedBox(height: 12),
      TextField(controller: _nameController, decoration: const InputDecoration(labelText: 'Building name (optional)')),
      const SizedBox(height: 12),
      Row(
        children: [
          OutlinedButton(onPressed: () => setState(() => _step = 2), child: const Text('Back')),
          const SizedBox(width: 8),
          FilledButton(
            onPressed: _purchasing ? null : _purchase,
            child: Text(_purchasing ? 'Purchasing…' : 'Purchase'),
          ),
        ],
      ),
    ];
  }
}
