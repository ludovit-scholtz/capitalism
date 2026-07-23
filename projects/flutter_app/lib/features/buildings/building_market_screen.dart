// Ported from `projects/frontend/src/views/BuildingMarketView.vue`.
// Trimmed: no negotiation-note field on the Make Offer dialog (the mutation
// supports one, but it's optional and secondary to price); no
// optimistic-concurrency retry-with-refresh UX on `OFFER_VERSION_CONFLICT`
// beyond a plain error snackbar — the underlying mutation call is still the
// real, version-checked one, just without the web's auto-refresh-and-retry
// affordance.

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/context/account_context_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import 'building_market_models.dart';
import 'building_market_service.dart';

class BuildingMarketScreen extends StatefulWidget {
  const BuildingMarketScreen({super.key, GraphQlService? graphQlService, BuildingMarketService? buildingMarketService})
    : _injectedGraphQlService = graphQlService,
      _injectedBuildingMarketService = buildingMarketService;

  final GraphQlService? _injectedGraphQlService;
  final BuildingMarketService? _injectedBuildingMarketService;

  @override
  State<BuildingMarketScreen> createState() => _BuildingMarketScreenState();
}

class _BuildingMarketScreenState extends State<BuildingMarketScreen> {
  late final BuildingMarketService _service;
  late final bool _isAuthenticated;

  String _tab = 'market';

  bool _marketLoading = true;
  String? _marketError;
  List<MarketBuilding> _market = const [];
  List<Map<String, String>> _cities = const [];
  String? _cityFilter;

  bool _listingsLoading = false;
  bool _listingsLoaded = false;
  String? _listingsError;
  List<MyBuildingListing> _myListings = const [];
  final Set<String> _actionLoadingIds = {};

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    _isAuthenticated = auth.isAuthenticated;
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedBuildingMarketService ?? BuildingMarketService(graphQlService);
    _loadMarket();
    _service.fetchCities().then((cities) {
      if (mounted) setState(() => _cities = cities);
    }).catchError((_) {});
  }

  Future<void> _loadMarket() async {
    setState(() {
      _marketLoading = true;
      _marketError = null;
    });
    try {
      final market = await _service.fetchMarket(cityId: _cityFilter);
      if (!mounted) return;
      setState(() {
        _market = market;
        _marketLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _marketError = 'Could not load the building market. Please try again.';
        _marketLoading = false;
      });
    }
  }

  Future<void> _loadListings() async {
    setState(() {
      _listingsLoading = true;
      _listingsError = null;
    });
    try {
      final listings = await _service.fetchMyListings();
      if (!mounted) return;
      setState(() {
        _myListings = listings;
        _listingsLoaded = true;
        _listingsLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _listingsError = 'Could not load your listings. Please try again.';
        _listingsLoading = false;
      });
    }
  }

  void _selectTab(String tab) {
    setState(() => _tab = tab);
    if (tab == 'listings' && !_listingsLoaded && !_listingsLoading) {
      _loadListings();
    }
  }

  Future<void> _openMakeOfferDialog(MarketBuilding building) async {
    final companies = await _service.fetchMyCompanies();
    if (!mounted) return;
    if (companies.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('You need a company to make an offer.')));
      return;
    }
    final activeCompanyId = context.read<AccountContextState>().activeCompanyId;
    String buyerCompanyId = companies.any((c) => c['id'] == activeCompanyId) ? activeCompanyId! : companies.first['id']!;
    final priceController = TextEditingController(text: building.askingPrice?.toStringAsFixed(0) ?? '');
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: Text('Make an offer on ${building.name}'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String>(
                initialValue: buyerCompanyId,
                decoration: const InputDecoration(labelText: 'Buyer company'),
                items: [for (final company in companies) DropdownMenuItem(value: company['id'], child: Text(company['name']!))],
                onChanged: (value) => setDialogState(() => buyerCompanyId = value ?? buyerCompanyId),
              ),
              TextField(
                controller: priceController,
                decoration: const InputDecoration(labelText: 'Offered price'),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
              ),
            ],
          ),
          actions: [
            TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
            FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Send offer')),
          ],
        ),
      ),
    );
    if (confirmed != true) return;

    try {
      await _service.makeOffer(
        buildingId: building.id,
        buyerCompanyId: buyerCompanyId,
        offeredPrice: double.tryParse(priceController.text) ?? 0,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Offer sent.')));
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not send the offer.')));
      }
    }
  }

  Future<void> _respondToOffer(BuildingOffer offer, bool accept) async {
    setState(() => _actionLoadingIds.add(offer.id));
    try {
      if (accept) {
        await _service.acceptOffer(offerId: offer.id, offerVersion: offer.offerVersion);
      } else {
        await _service.rejectOffer(offerId: offer.id, offerVersion: offer.offerVersion);
      }
      await _loadListings();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Action failed. Please try again.')));
      }
    } finally {
      if (mounted) setState(() => _actionLoadingIds.remove(offer.id));
    }
  }

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Building Market', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(child: ChoiceChip(label: const Text('Market'), selected: _tab == 'market', onSelected: (_) => _selectTab('market'))),
            const SizedBox(width: 8),
            Expanded(
              child: ChoiceChip(
                label: const Text('My Listings'),
                selected: _tab == 'listings',
                onSelected: _isAuthenticated ? (_) => _selectTab('listings') : null,
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        if (_tab == 'market') ..._buildMarketTab() else ..._buildListingsTab(),
      ],
    );
  }

  List<Widget> _buildMarketTab() {
    if (_marketLoading) {
      return const [Padding(padding: EdgeInsets.symmetric(vertical: 48), child: Center(child: CircularProgressIndicator()))];
    }
    if (_marketError != null) {
      return [
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 24),
          child: Column(
            children: [Text(_marketError!), const SizedBox(height: 12), OutlinedButton(onPressed: _loadMarket, child: const Text('Try again'))],
          ),
        ),
      ];
    }
    return [
      DropdownButtonFormField<String?>(
        initialValue: _cityFilter,
        decoration: const InputDecoration(labelText: 'City'),
        items: [
          const DropdownMenuItem(value: null, child: Text('Any city')),
          for (final city in _cities) DropdownMenuItem(value: city['id'], child: Text(city['name']!)),
        ],
        onChanged: (value) {
          setState(() => _cityFilter = value);
          _loadMarket();
        },
      ),
      const SizedBox(height: 12),
      if (_market.isEmpty)
        const Text('No buildings for sale right now.')
      else
        for (final building in _market)
          Card(
            key: ValueKey('market-building-${building.id}'),
            margin: const EdgeInsets.only(bottom: 8),
            child: ListTile(
              title: Text(building.name),
              subtitle: Text('${building.type} · Level ${building.level} · ${building.city.name}'),
              trailing: Text(building.askingPrice != null ? '${building.askingPrice!.toStringAsFixed(0)} ${building.city.currencyCode}' : '—'),
              onTap: () => _openMakeOfferDialog(building),
            ),
          ),
    ];
  }

  List<Widget> _buildListingsTab() {
    if (!_isAuthenticated) {
      return const [Text('Sign in to manage your building listings.')];
    }
    if (_listingsLoading) {
      return const [Padding(padding: EdgeInsets.symmetric(vertical: 48), child: Center(child: CircularProgressIndicator()))];
    }
    if (_listingsError != null) {
      return [
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 24),
          child: Column(
            children: [Text(_listingsError!), const SizedBox(height: 12), OutlinedButton(onPressed: _loadListings, child: const Text('Try again'))],
          ),
        ),
      ];
    }
    if (_myListings.isEmpty) {
      return const [Text('You have no buildings listed for sale.')];
    }
    return [
      for (final listing in _myListings)
        Card(
          key: ValueKey('my-listing-${listing.building.id}'),
          margin: const EdgeInsets.only(bottom: 12),
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(listing.building.name, style: Theme.of(context).textTheme.titleSmall),
                Text('Asking: ${listing.building.askingPrice?.toStringAsFixed(0) ?? '—'} ${listing.building.city.currencyCode}'),
                const SizedBox(height: 8),
                if (listing.offers.isEmpty)
                  const Text('No offers yet.')
                else
                  for (final offer in listing.offers.where((o) => o.status == 'PENDING'))
                    Padding(
                      padding: const EdgeInsets.only(bottom: 4),
                      child: Row(
                        children: [
                          Expanded(child: Text('${offer.buyerCompanyName ?? offer.buyerDisplayName ?? 'Buyer'}: ${offer.offeredPrice.toStringAsFixed(0)}')),
                          IconButton(
                            icon: const FaIcon(AppIcons.check, size: 16),
                            onPressed: _actionLoadingIds.contains(offer.id) ? null : () => _respondToOffer(offer, true),
                          ),
                          IconButton(
                            icon: const FaIcon(AppIcons.close, size: 16),
                            onPressed: _actionLoadingIds.contains(offer.id) ? null : () => _respondToOffer(offer, false),
                          ),
                        ],
                      ),
                    ),
              ],
            ),
          ),
        ),
    ];
  }
}
