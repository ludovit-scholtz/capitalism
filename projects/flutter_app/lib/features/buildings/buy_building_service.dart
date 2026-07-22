import '../../core/graphql/graphql_service.dart';
import 'buy_building_models.dart';

const _citiesQuery = r'''
  query BuyBuildingCities { cities { id name countryCode currencyCode population } }
''';

const _lotsQuery = r'''
  query BuyBuildingLots($cityId: UUID!) {
    cityLots(cityId: $cityId) {
      id cityId name description district latitude longitude populationIndex basePrice price suitableTypes
      ownerCompanyId buildingId
    }
  }
''';

const _myCompaniesQuery = r'''
  query BuyBuildingMyCompanies { me { companies { id name } } }
''';

const _purchaseLotMutation = r'''
  mutation PurchaseLot($input: PurchaseLotInput!) {
    purchaseLot(input: $input) { building { id name type level } }
  }
''';

/// GraphQL calls for the Buy Building screen, matching
/// `projects/frontend/src/views/BuyBuildingView.vue`'s core contract.
/// Trimmed (documented in the screen): no bank-specific follow-up
/// mutations (`setBankRates`/`initiateBaseDeposit`), no power-plant-type or
/// media-type sub-selection.
class BuyBuildingService {
  const BuyBuildingService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<Map<String, String>>> fetchCities() async {
    final result = await _graphQlService.request(_citiesQuery);
    final list = result['cities'] as List<dynamic>? ?? const [];
    return list
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'name': e['name'] as String})
        .toList();
  }

  Future<List<CityLot>> fetchLots(String cityId) async {
    final result = await _graphQlService.request(_lotsQuery, variables: {'cityId': cityId});
    final list = result['cityLots'] as List<dynamic>? ?? const [];
    return list.map((e) => CityLot.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<Map<String, String>>> fetchMyCompanies() async {
    final result = await _graphQlService.request(_myCompaniesQuery);
    final companies = (result['me'] as Map<String, dynamic>?)?['companies'] as List<dynamic>? ?? const [];
    return companies
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'name': e['name'] as String})
        .toList();
  }

  Future<void> purchaseLot({
    required String companyId,
    required String lotId,
    required String buildingType,
    String? buildingName,
  }) {
    return _graphQlService.request(
      _purchaseLotMutation,
      variables: {
        'input': {
          'companyId': companyId,
          'lotId': lotId,
          'buildingType': buildingType,
          'buildingName': buildingName,
        },
      },
    );
  }
}
