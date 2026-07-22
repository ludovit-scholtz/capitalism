import '../../core/graphql/graphql_service.dart';
import 'global_exchange_models.dart';

const _citiesQuery = r'''
  query GlobalExchangeCities { cities { id name countryCode currencyCode } }
''';

const _offersQuery = r'''
  query GlobalExchangeOffers($destinationCityId: UUID!) {
    globalExchangeOffers(destinationCityId: $destinationCityId) {
      cityId cityName resourceTypeId resourceName resourceSlug unitSymbol
      localAbundance exchangePricePerUnit estimatedQuality qualityMin qualityMax
      transitCostPerUnit deliveredPricePerUnit distanceKm fuelPriceIndex
    }
  }
''';

const _productListingsQuery = r'''
  query GlobalExchangeProductListings($productTypeId: UUID) {
    globalExchangeProductListings(productTypeId: $productTypeId) {
      orderId productTypeId productName productSlug productIndustry unitSymbol unitName
      basePrice pricePerUnit remainingQuantity sellerCityId sellerCityName
      sellerCompanyId sellerCompanyName createdAtUtc
    }
  }
''';

const _myUnitsAndAccountsQuery = r'''
  query GlobalExchangeMyResources {
    myBankAccounts { id currencyCode balance companyId ownerType }
    myCompanies {
      id name
      buildings { id name units { id unitType } }
    }
  }
''';

const _buyMutation = r'''
  mutation BuyFromExchange($input: BuyFromExchangeInput!) {
    buyFromExchange(input: $input) {
      success errorCode errorMessage resourceName quantityPurchased totalCost newBankBalance currencyCode
    }
  }
''';

/// GraphQL calls for the Global Exchange screen, matching
/// `projects/frontend/src/views/GlobalExchangeView.vue`'s Resources tab
/// contract. The Products tab is read-only here too, matching the web
/// (it has no direct buy flow for product listings either).
class GlobalExchangeService {
  const GlobalExchangeService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<Map<String, String>>> fetchCities() async {
    final result = await _graphQlService.request(_citiesQuery);
    final list = result['cities'] as List<dynamic>? ?? const [];
    return list
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'name': e['name'] as String})
        .toList();
  }

  Future<List<GlobalExchangeOffer>> fetchOffers(String destinationCityId) async {
    final result = await _graphQlService.request(_offersQuery, variables: {'destinationCityId': destinationCityId});
    final list = result['globalExchangeOffers'] as List<dynamic>? ?? const [];
    return list.map((e) => GlobalExchangeOffer.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<GlobalExchangeProductListing>> fetchProductListings() async {
    final result = await _graphQlService.request(_productListingsQuery, variables: {'productTypeId': null});
    final list = result['globalExchangeProductListings'] as List<dynamic>? ?? const [];
    return list.map((e) => GlobalExchangeProductListing.fromJson(e as Map<String, dynamic>)).toList();
  }

  /// Returns `(bankAccounts, targetUnits)` for the buy dialog — bank
  /// accounts are company-owned, target units are STORAGE/PURCHASE units
  /// across the player's buildings, matching the web's buy-modal filters.
  Future<(List<Map<String, String>>, List<ExchangeTargetUnit>)> fetchBuyDialogOptions() async {
    final result = await _graphQlService.request(_myUnitsAndAccountsQuery);
    final accounts = result['myBankAccounts'] as List<dynamic>? ?? const [];
    final bankAccounts = accounts
        .where((a) => (a as Map<String, dynamic>)['ownerType'] == 'COMPANY')
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'currencyCode': e['currencyCode'] as String})
        .toList();

    final companies = result['myCompanies'] as List<dynamic>? ?? const [];
    final targetUnits = <ExchangeTargetUnit>[];
    for (final company in companies) {
      final buildings = (company as Map<String, dynamic>)['buildings'] as List<dynamic>? ?? const [];
      for (final building in buildings) {
        final buildingMap = building as Map<String, dynamic>;
        final units = buildingMap['units'] as List<dynamic>? ?? const [];
        for (final unit in units) {
          final unitMap = unit as Map<String, dynamic>;
          final unitType = unitMap['unitType'] as String;
          if (unitType == 'STORAGE' || unitType == 'PURCHASE') {
            targetUnits.add(
              ExchangeTargetUnit(id: unitMap['id'] as String, buildingName: buildingMap['name'] as String, unitType: unitType),
            );
          }
        }
      }
    }
    return (bankAccounts, targetUnits);
  }

  Future<void> buyFromExchange({
    required String sourceCityId,
    required String resourceTypeId,
    required double quantity,
    required String targetBuildingUnitId,
    required String bankAccountId,
  }) {
    return _graphQlService.request(
      _buyMutation,
      variables: {
        'input': {
          'sourceCityId': sourceCityId,
          'resourceTypeId': resourceTypeId,
          'quantity': quantity,
          'targetBuildingUnitId': targetBuildingUnitId,
          'bankAccountId': bankAccountId,
        },
      },
    );
  }
}
