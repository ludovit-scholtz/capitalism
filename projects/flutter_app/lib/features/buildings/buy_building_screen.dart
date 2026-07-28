// Ported from `projects/frontend/src/views/BuyBuildingView.vue` +
// `BuyBuildingSteps.vue`. The POWER_PLANT subtype picker has no equivalent
// in either of those files — `BuyBuildingSteps.vue` never grew one — so
// it's ported instead from `CityLotDetailPanel.vue` (the map-based "buy from
// a lot" flow, which does support it), reusing the same option list/labels.
//
// The lot step now renders a real interactive map (`CapitalismMapView`) with
// lot markers, the player's existing-building markers, and a
// distance-to-existing-buildings list (`buy_building_distance.dart`, real
// haversine, matching web's `computeDistanceKm`/`nearestBuildingsForLot`) —
// the plain sortable list is kept alongside it as a precise, thumb-friendly
// alternative to tapping small map pins on a phone (mobile-UX improvement,
// not a web-parity requirement).

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart' show TileProvider;
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/i18n/locale_state.dart';
import '../../core/theme/app_icons.dart';
import '../../core/utils/app_number_format.dart';
import '../../core/widgets/capitalism_map_view.dart';
import '../banking/banking_models.dart';
import '../banking/banking_service.dart';
import 'buy_building_distance.dart';
import 'buy_building_models.dart';
import 'buy_building_service.dart';

class BuyBuildingScreen extends StatefulWidget {
  const BuyBuildingScreen({
    super.key,
    required this.companyId,
    GraphQlService? graphQlService,
    BuyBuildingService? buyBuildingService,
    BankingService? bankingService,
    this.tileProvider,
  }) : _injectedGraphQlService = graphQlService,
       _injectedBuyBuildingService = buyBuildingService,
       _injectedBankingService = bankingService;

  final String companyId;
  final GraphQlService? _injectedGraphQlService;
  final BuyBuildingService? _injectedBuyBuildingService;

  /// Used for the BANK-specific follow-up mutations after purchase
  /// (`initiateBaseDeposit`/`setBankRates`) — `BuyBuildingService` doesn't
  /// duplicate those, `BankingService` already owns them.
  final BankingService? _injectedBankingService;

  /// Injectable so widget tests never hit real OSM tile servers — see
  /// `test/support/fake_tile_provider.dart`.
  final TileProvider? tileProvider;

  @override
  State<BuyBuildingScreen> createState() => _BuyBuildingScreenState();
}

class _BuyBuildingScreenState extends State<BuyBuildingScreen> {
  late final BuyBuildingService _service;
  late final BankingService _bankingService;

  bool _loading = true;
  String? _error;
  List<BuyBuildingCity> _cities = const [];

  int _step = 0;
  String? _cityId;
  String? _buildingType;
  CityLot? _selectedLot;
  final _nameController = TextEditingController();

  String? _selectedMediaType;
  String? _selectedPowerPlantType;
  final _depositRateController = TextEditingController(text: '3');
  final _lendingRateController = TextEditingController(text: '8');
  List<PlayerBankAccount> _companyBankAccounts = const [];

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
    _bankingService = widget._injectedBankingService ?? BankingService(graphQlService);
    _load();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _depositRateController.dispose();
    _lendingRateController.dispose();
    super.dispose();
  }

  BuyBuildingCity? get _selectedCity {
    final cityId = _cityId;
    if (cityId == null) return null;
    for (final city in _cities) {
      if (city.id == cityId) return city;
    }
    return null;
  }

  /// Company bank balance in the selected city's currency, summed across
  /// every company-owned account in that currency — mirrors
  /// `companyBankBalanceInCityCurrency` in `BuyBuildingSteps.vue`.
  double get _companyBankBalanceInCityCurrency {
    final currencyCode = _selectedCity?.currencyCode.toUpperCase();
    if (currencyCode == null) return 0;
    return _companyBankAccounts
        .where((a) => a.ownerType == 'COMPANY' && a.companyId == widget.companyId && a.currencyCode.toUpperCase() == currencyCode)
        .fold<double>(0, (sum, a) => sum + a.balance);
  }

  bool get _companyHasBankCapital {
    final currencyCode = _selectedCity?.currencyCode;
    if (currencyCode == null) return false;
    return _companyBankBalanceInCityCurrency >= bankBaseCapitalRequired(currencyCode);
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
    try {
      final accounts = await _bankingService.fetchMyBankAccounts();
      if (mounted) setState(() => _companyBankAccounts = accounts);
    } catch (_) {
      // Best-effort — the BANK capital check simply shows "insufficient"
      // until this succeeds; it never blocks non-BANK purchases.
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
    if (_buildingType == 'BANK' && !_companyHasBankCapital) return;
    setState(() => _purchasing = true);
    try {
      final buildingId = await _service.purchaseLot(
        companyId: widget.companyId,
        lotId: lot.id,
        buildingType: _buildingType!,
        buildingName: _nameController.text.trim().isEmpty ? null : _nameController.text.trim(),
        mediaType: _selectedMediaType,
        powerPlantType: _selectedPowerPlantType,
      );

      if (_buildingType == 'BANK') {
        // Best-effort, matching web's swallowed-error behavior — a failure
        // here shouldn't undo the purchase itself; the owner can still
        // configure rates/deposit later from Bank Management.
        try {
          await _bankingService.initiateBaseDeposit(buildingId);
        } catch (_) {
          // Non-fatal.
        }
        try {
          await _bankingService.setBankRates(
            bankBuildingId: buildingId,
            depositRate: double.tryParse(_depositRateController.text) ?? 3,
            lendingRate: double.tryParse(_lendingRateController.text) ?? 8,
          );
        } catch (_) {
          // Non-fatal.
        }
      }

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
          key: ValueKey('city-${city.id}'),
          selected: _cityId == city.id,
          leading: FaIcon(_cityId == city.id ? AppIcons.radioChecked : AppIcons.radioUnchecked, size: 18),
          title: Text(city.name),
          onTap: () => setState(() => _cityId = city.id),
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

  bool get _typeStepComplete {
    if (_buildingType == 'MEDIA_HOUSE') return _selectedMediaType != null;
    if (_buildingType == 'POWER_PLANT') return _selectedPowerPlantType != null;
    return true;
  }

  List<Widget> _buildTypeStep() {
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;
    return [
      Text('2. Choose a building type', style: theme.textTheme.titleMedium),
      const SizedBox(height: 8),
      for (final type in buildingTypes)
        ListTile(
          key: ValueKey('type-$type'),
          selected: _buildingType == type,
          leading: FaIcon(_buildingType == type ? AppIcons.radioChecked : AppIcons.radioUnchecked, size: 18),
          title: Text(type),
          onTap: () => setState(() {
            _buildingType = type;
            if (type != 'MEDIA_HOUSE') _selectedMediaType = null;
            if (type != 'POWER_PLANT') _selectedPowerPlantType = null;
          }),
        ),
      if (_buildingType == 'MEDIA_HOUSE') ..._buildMediaTypeSelector(theme),
      if (_buildingType == 'POWER_PLANT') ..._buildPowerPlantTypeSelector(theme),
      if (_buildingType == 'BANK') ..._buildBankSetupSection(theme, languageCode),
      const SizedBox(height: 12),
      Row(
        children: [
          OutlinedButton(onPressed: () => setState(() => _step = 0), child: const Text('Back')),
          const SizedBox(width: 8),
          FilledButton(
            onPressed: _buildingType == null || !_typeStepComplete
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

  List<Widget> _buildMediaTypeSelector(ThemeData theme) {
    return [
      const SizedBox(height: 12),
      Text('Media channel type', style: theme.textTheme.labelLarge),
      const SizedBox(height: 4),
      Wrap(
        spacing: 8,
        runSpacing: 8,
        children: [
          for (final option in mediaHouseChannelTypes)
            ChoiceChip(
              key: ValueKey('media-type-${option.code}'),
              label: Text('${option.icon} ${option.label} (${option.multiplierLabel})'),
              selected: _selectedMediaType == option.code,
              onSelected: (_) => setState(() => _selectedMediaType = option.code),
            ),
        ],
      ),
      if (_selectedMediaType == null)
        Padding(
          padding: const EdgeInsets.only(top: 4),
          child: Text('Select a channel type to continue.', style: theme.textTheme.bodySmall),
        ),
    ];
  }

  List<Widget> _buildPowerPlantTypeSelector(ThemeData theme) {
    return [
      const SizedBox(height: 12),
      Text('Power plant subtype', style: theme.textTheme.labelLarge),
      const SizedBox(height: 4),
      for (final option in powerPlantTypeOptions)
        Card(
          key: ValueKey('power-plant-type-${option.code}'),
          color: _selectedPowerPlantType == option.code ? theme.colorScheme.primaryContainer : null,
          child: ListTile(
            selected: _selectedPowerPlantType == option.code,
            leading: FaIcon(
              _selectedPowerPlantType == option.code ? AppIcons.radioChecked : AppIcons.radioUnchecked,
              size: 18,
            ),
            title: Text('${option.label} · ${option.outputMw} MW'),
            subtitle: Text('${option.description} ${option.isRenewable ? '(renewable)' : '(fuel-based)'}'),
            onTap: () => setState(() => _selectedPowerPlantType = option.code),
          ),
        ),
      if (_selectedPowerPlantType == null)
        Padding(
          padding: const EdgeInsets.only(top: 4),
          child: Text('Select a subtype to continue.', style: theme.textTheme.bodySmall),
        ),
    ];
  }

  List<Widget> _buildBankSetupSection(ThemeData theme, String languageCode) {
    final currencyCode = _selectedCity?.currencyCode ?? 'EUR';
    final required = bankBaseCapitalRequired(currencyCode);
    final hasCapital = _companyHasBankCapital;
    return [
      const SizedBox(height: 16),
      Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('🏦 Bank setup', style: theme.textTheme.titleSmall),
              const SizedBox(height: 8),
              const Text(
                'A new bank needs base capital before it can start operating, plus '
                'deposit/lending rates for its first customers.',
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Icon(
                    hasCapital ? Icons.check_circle : Icons.warning_amber,
                    color: hasCapital ? Colors.green : theme.colorScheme.error,
                    size: 18,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Base capital required: ${AppNumberFormat.money(required, currencyCode: currencyCode, languageCode: languageCode)}'
                      '${hasCapital ? ' — sufficient' : ' — insufficient (have ${AppNumberFormat.money(_companyBankBalanceInCityCurrency, currencyCode: currencyCode, languageCode: languageCode)})'}',
                      key: const Key('bank-capital-check'),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      key: const Key('bank-deposit-rate'),
                      controller: _depositRateController,
                      decoration: const InputDecoration(labelText: 'Deposit rate (%)'),
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextField(
                      key: const Key('bank-lending-rate'),
                      controller: _lendingRateController,
                      decoration: const InputDecoration(labelText: 'Lending rate (%)'),
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
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
    final theme = Theme.of(context);
    final isBank = _buildingType == 'BANK';
    final bankBlocked = isBank && !_companyHasBankCapital;
    return [
      Text('4. Confirm purchase', style: theme.textTheme.titleMedium),
      const SizedBox(height: 8),
      Text('Lot: ${_selectedLot?.name ?? _selectedLot?.district ?? _selectedLot?.id}'),
      Text('Price: ${_selectedLot?.price.toStringAsFixed(0)}'),
      Text('Type: $_buildingType'),
      if (_selectedMediaType != null) Text('Channel type: $_selectedMediaType'),
      if (_selectedPowerPlantType != null) Text('Subtype: $_selectedPowerPlantType'),
      const SizedBox(height: 12),
      TextField(controller: _nameController, decoration: const InputDecoration(labelText: 'Building name (optional)')),
      if (bankBlocked) ...[
        const SizedBox(height: 12),
        Text(
          'This company does not have enough bank capital in this city\'s currency yet — '
          'go back and check the bank setup requirement.',
          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error),
        ),
      ],
      const SizedBox(height: 12),
      Row(
        children: [
          OutlinedButton(onPressed: () => setState(() => _step = 2), child: const Text('Back')),
          const SizedBox(width: 8),
          FilledButton(
            onPressed: _purchasing || bankBlocked ? null : _purchase,
            child: Text(_purchasing ? 'Purchasing…' : 'Purchase'),
          ),
        ],
      ),
    ];
  }
}
