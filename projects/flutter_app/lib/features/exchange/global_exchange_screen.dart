// Ported from `projects/frontend/src/views/GlobalExchangeView.vue`, including
// its category/industry filter dropdowns and free-text search (built from
// `resourceTypes`/`productTypes`, matching the web's `RESOURCES_QUERY`/
// `PRODUCTS_QUERY`). Still trimmed: the Products tab is read-only here too,
// matching the web (it has no direct buy flow for product listings —
// that's presumably company-to-company via Contracts).

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import 'global_exchange_models.dart';
import 'global_exchange_service.dart';

class GlobalExchangeScreen extends StatefulWidget {
  const GlobalExchangeScreen({super.key, GraphQlService? graphQlService, GlobalExchangeService? globalExchangeService})
    : _injectedGraphQlService = graphQlService,
      _injectedGlobalExchangeService = globalExchangeService;

  final GraphQlService? _injectedGraphQlService;
  final GlobalExchangeService? _injectedGlobalExchangeService;

  @override
  State<GlobalExchangeScreen> createState() => _GlobalExchangeScreenState();
}

class _GlobalExchangeScreenState extends State<GlobalExchangeScreen> {
  late final GlobalExchangeService _service;

  String _tab = 'resources';
  List<Map<String, String>> _cities = const [];
  String? _destinationCityId;

  bool _offersLoading = false;
  String? _offersError;
  List<GlobalExchangeOffer> _offers = const [];
  Map<String, String> _resourceCategoryById = const {};
  List<String> _resourceCategories = const [];
  String _resourceCategoryFilter = 'ALL';
  String _resourceSearch = '';

  bool _productsLoading = false;
  bool _productsLoaded = false;
  String? _productsError;
  List<GlobalExchangeProductListing> _products = const [];
  List<String> _productIndustries = const [];
  String _productIndustryFilter = 'ALL';
  String _productSearch = '';

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedGlobalExchangeService ?? GlobalExchangeService(graphQlService);
    _loadCities();
    _loadResourceCatalog();
    _loadProductCatalog();
  }

  Future<void> _loadResourceCatalog() async {
    try {
      final catalog = await _service.fetchResourceTypes();
      if (!mounted) return;
      setState(() {
        _resourceCategoryById = {for (final entry in catalog) entry.id: entry.category};
        _resourceCategories = catalog.map((e) => e.category).where((c) => c.isNotEmpty).toSet().toList()..sort();
      });
    } catch (_) {
      // Filter dropdown just stays empty (only "All") on failure — the
      // offers list itself still loads independently.
    }
  }

  Future<void> _loadProductCatalog() async {
    try {
      final catalog = await _service.fetchProductTypes();
      if (!mounted) return;
      setState(() {
        _productIndustries = catalog.map((e) => e.category).where((c) => c.isNotEmpty).toSet().toList()..sort();
      });
    } catch (_) {
      // Filter dropdown just stays empty (only "All") on failure.
    }
  }

  List<GlobalExchangeOffer> get _filteredOffers {
    return _offers.where((offer) {
      if (_resourceCategoryFilter != 'ALL' && (_resourceCategoryById[offer.resourceTypeId] ?? '') != _resourceCategoryFilter) {
        return false;
      }
      if (_resourceSearch.isNotEmpty && !offer.resourceName.toLowerCase().contains(_resourceSearch.toLowerCase())) {
        return false;
      }
      return true;
    }).toList();
  }

  List<GlobalExchangeProductListing> get _filteredProducts {
    return _products.where((listing) {
      if (_productIndustryFilter != 'ALL' && listing.productIndustry != _productIndustryFilter) return false;
      if (_productSearch.isNotEmpty && !listing.productName.toLowerCase().contains(_productSearch.toLowerCase())) {
        return false;
      }
      return true;
    }).toList();
  }

  Future<void> _loadCities() async {
    try {
      final cities = await _service.fetchCities();
      if (!mounted) return;
      setState(() {
        _cities = cities;
        _destinationCityId = cities.isNotEmpty ? cities.first['id'] : null;
      });
      if (_destinationCityId != null) _loadOffers();
    } catch (_) {
      // Handled by the offers/product loaders themselves.
    }
  }

  Future<void> _loadOffers() async {
    if (_destinationCityId == null) return;
    setState(() {
      _offersLoading = true;
      _offersError = null;
    });
    try {
      final offers = await _service.fetchOffers(_destinationCityId!);
      if (!mounted) return;
      setState(() {
        _offers = offers;
        _offersLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _offersError = 'Could not load exchange offers. Please try again.';
        _offersLoading = false;
      });
    }
  }

  Future<void> _loadProducts() async {
    setState(() {
      _productsLoading = true;
      _productsError = null;
    });
    try {
      final products = await _service.fetchProductListings();
      if (!mounted) return;
      setState(() {
        _products = products;
        _productsLoaded = true;
        _productsLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _productsError = 'Could not load product listings. Please try again.';
        _productsLoading = false;
      });
    }
  }

  void _selectTab(String tab) {
    setState(() => _tab = tab);
    if (tab == 'products' && !_productsLoaded && !_productsLoading) _loadProducts();
  }

  Future<void> _openBuyDialog(GlobalExchangeOffer offer) async {
    final (bankAccounts, targetUnits) = await _service.fetchBuyDialogOptions();
    if (!mounted) return;
    if (bankAccounts.isEmpty || targetUnits.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('You need a bank account and a storage/purchase unit to buy.')));
      return;
    }
    String bankAccountId = bankAccounts.first['id']!;
    String targetUnitId = targetUnits.first.id;
    final quantityController = TextEditingController(text: '100');

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: Text('Buy ${offer.resourceName}'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: quantityController,
                decoration: const InputDecoration(labelText: 'Quantity'),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
              ),
              DropdownButtonFormField<String>(
                initialValue: targetUnitId,
                decoration: const InputDecoration(labelText: 'Destination unit'),
                items: [for (final unit in targetUnits) DropdownMenuItem(value: unit.id, child: Text('${unit.buildingName} (${unit.unitType})'))],
                onChanged: (value) => setDialogState(() => targetUnitId = value ?? targetUnitId),
              ),
              DropdownButtonFormField<String>(
                initialValue: bankAccountId,
                decoration: const InputDecoration(labelText: 'Bank account'),
                items: [for (final account in bankAccounts) DropdownMenuItem(value: account['id'], child: Text(account['currencyCode']!))],
                onChanged: (value) => setDialogState(() => bankAccountId = value ?? bankAccountId),
              ),
            ],
          ),
          actions: [
            TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
            FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Buy')),
          ],
        ),
      ),
    );
    if (confirmed != true) return;

    try {
      await _service.buyFromExchange(
        sourceCityId: offer.cityId,
        resourceTypeId: offer.resourceTypeId,
        quantity: double.tryParse(quantityController.text) ?? 0,
        targetBuildingUnitId: targetUnitId,
        bankAccountId: bankAccountId,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Purchase complete.')));
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not complete the purchase.')));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Global Exchange', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(child: ChoiceChip(label: const Text('Resources'), selected: _tab == 'resources', onSelected: (_) => _selectTab('resources'))),
            const SizedBox(width: 8),
            Expanded(child: ChoiceChip(label: const Text('Products'), selected: _tab == 'products', onSelected: (_) => _selectTab('products'))),
          ],
        ),
        const SizedBox(height: 16),
        if (_tab == 'resources') ..._buildResourcesTab() else ..._buildProductsTab(),
      ],
    );
  }

  List<Widget> _buildResourcesTab() {
    final filtered = _filteredOffers;
    return [
      DropdownButtonFormField<String>(
        initialValue: _destinationCityId,
        decoration: const InputDecoration(labelText: 'Destination city'),
        items: [for (final city in _cities) DropdownMenuItem(value: city['id'], child: Text(city['name']!))],
        onChanged: (value) {
          setState(() => _destinationCityId = value);
          _loadOffers();
        },
      ),
      const SizedBox(height: 12),
      TextField(
        key: const Key('resource-search'),
        decoration: const InputDecoration(labelText: 'Search resources', prefixIcon: FaIcon(AppIcons.search, size: 16)),
        onChanged: (value) => setState(() => _resourceSearch = value),
      ),
      const SizedBox(height: 12),
      DropdownButtonFormField<String>(
        key: const Key('resource-category-filter'),
        initialValue: _resourceCategoryFilter,
        decoration: const InputDecoration(labelText: 'Category'),
        items: [
          const DropdownMenuItem(value: 'ALL', child: Text('All categories')),
          for (final category in _resourceCategories) DropdownMenuItem(value: category, child: Text(category)),
        ],
        onChanged: (value) => setState(() => _resourceCategoryFilter = value ?? 'ALL'),
      ),
      const SizedBox(height: 12),
      if (_offersLoading)
        const Center(child: CircularProgressIndicator())
      else if (_offersError != null)
        Column(children: [Text(_offersError!), const SizedBox(height: 8), OutlinedButton(onPressed: _loadOffers, child: const Text('Try again'))])
      else if (filtered.isEmpty)
        Text(_offers.isEmpty ? 'No offers available for this city.' : 'No offers match your filters.')
      else
        for (final offer in filtered)
          Card(
            key: ValueKey('exchange-offer-${offer.cityId}-${offer.resourceTypeId}'),
            margin: const EdgeInsets.only(bottom: 8),
            child: ListTile(
              title: Text('${offer.resourceName} from ${offer.cityName}'),
              subtitle: Text('Delivered: ${offer.deliveredPricePerUnit.toStringAsFixed(2)} · Quality ${(offer.estimatedQuality * 100).round()}%'),
              trailing: FilledButton(onPressed: () => _openBuyDialog(offer), child: const Text('Buy')),
            ),
          ),
    ];
  }

  List<Widget> _buildProductsTab() {
    if (_productsLoading) return const [Center(child: CircularProgressIndicator())];
    if (_productsError != null) {
      return [Text(_productsError!), const SizedBox(height: 8), OutlinedButton(onPressed: _loadProducts, child: const Text('Try again'))];
    }
    final filtered = _filteredProducts;
    return [
      TextField(
        key: const Key('product-search'),
        decoration: const InputDecoration(labelText: 'Search products', prefixIcon: FaIcon(AppIcons.search, size: 16)),
        onChanged: (value) => setState(() => _productSearch = value),
      ),
      const SizedBox(height: 12),
      DropdownButtonFormField<String>(
        key: const Key('product-industry-filter'),
        initialValue: _productIndustryFilter,
        decoration: const InputDecoration(labelText: 'Industry'),
        items: [
          const DropdownMenuItem(value: 'ALL', child: Text('All industries')),
          for (final industry in _productIndustries) DropdownMenuItem(value: industry, child: Text(industry)),
        ],
        onChanged: (value) => setState(() => _productIndustryFilter = value ?? 'ALL'),
      ),
      const SizedBox(height: 12),
      if (filtered.isEmpty)
        Text(_products.isEmpty ? 'No product listings available.' : 'No listings match your filters.')
      else
        for (final listing in filtered)
          Card(
            key: ValueKey('product-listing-${listing.orderId}'),
            margin: const EdgeInsets.only(bottom: 8),
            child: ListTile(
              title: Text(listing.productName),
              subtitle: Text('${listing.sellerCompanyName} · ${listing.sellerCityName}'),
              trailing: Text('${listing.pricePerUnit.toStringAsFixed(2)} × ${listing.remainingQuantity.toStringAsFixed(0)}'),
            ),
          ),
    ];
  }
}
