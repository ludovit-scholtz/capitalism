import 'package:capitalism_app/features/exchange/global_exchange_models.dart';
import 'package:capitalism_app/features/exchange/global_exchange_service.dart';

class FakeGlobalExchangeService implements GlobalExchangeService {
  FakeGlobalExchangeService({
    this.cities = const [],
    this.offersByCity = const {},
    this.products = const [],
    this.bankAccounts = const [],
    this.targetUnits = const [],
    this.resourceTypes = const [],
    this.productTypes = const [],
    this.buyError,
  });

  final List<Map<String, String>> cities;
  final Map<String, List<GlobalExchangeOffer>> offersByCity;
  final List<GlobalExchangeProductListing> products;
  final List<Map<String, String>> bankAccounts;
  final List<ExchangeTargetUnit> targetUnits;
  final List<ExchangeCatalogEntry> resourceTypes;
  final List<ExchangeCatalogEntry> productTypes;
  final Object? buyError;

  final List<String> calls = [];
  Map<String, dynamic>? lastBuyArgs;

  @override
  Future<List<Map<String, String>>> fetchCities() async {
    calls.add('fetchCities');
    return cities;
  }

  @override
  Future<List<GlobalExchangeOffer>> fetchOffers(String destinationCityId) async {
    calls.add('fetchOffers');
    return offersByCity[destinationCityId] ?? const [];
  }

  @override
  Future<List<GlobalExchangeProductListing>> fetchProductListings() async {
    calls.add('fetchProductListings');
    return products;
  }

  @override
  Future<List<ExchangeCatalogEntry>> fetchResourceTypes() async {
    calls.add('fetchResourceTypes');
    return resourceTypes;
  }

  @override
  Future<List<ExchangeCatalogEntry>> fetchProductTypes() async {
    calls.add('fetchProductTypes');
    return productTypes;
  }

  @override
  Future<(List<Map<String, String>>, List<ExchangeTargetUnit>)> fetchBuyDialogOptions() async {
    calls.add('fetchBuyDialogOptions');
    return (bankAccounts, targetUnits);
  }

  @override
  Future<void> buyFromExchange({
    required String sourceCityId,
    required String resourceTypeId,
    required double quantity,
    required String targetBuildingUnitId,
    required String bankAccountId,
  }) async {
    calls.add('buyFromExchange');
    if (buyError != null) throw buyError!;
    lastBuyArgs = {
      'sourceCityId': sourceCityId,
      'resourceTypeId': resourceTypeId,
      'quantity': quantity,
      'targetBuildingUnitId': targetBuildingUnitId,
      'bankAccountId': bankAccountId,
    };
  }
}
