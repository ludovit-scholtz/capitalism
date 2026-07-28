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

const _myBuildingLocationsQuery = r'''
  query BuyBuildingMyBuildingLocations {
    myCompanies { id buildings { id name type cityId latitude longitude } }
  }
''';

const _purchaseLotMutation = r'''
  mutation PurchaseLot($input: PurchaseLotInput!) {
    purchaseLot(input: $input) { building { id name type level } }
  }
''';

/// GraphQL calls for the Buy Building screen, matching
/// `projects/frontend/src/views/BuyBuildingView.vue`'s core contract, plus
/// the POWER_PLANT-subtype picker ported from `CityLotDetailPanel.vue` (the
/// dedicated Buy Building screen itself never grew one on the web). The
/// BANK-specific follow-up (`setBankRates`/`initiateBaseDeposit`) reuses
/// `BankingService` from `buy_building_screen.dart` rather than duplicating
/// those mutations here.
class BuyBuildingService {
  const BuyBuildingService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<BuyBuildingCity>> fetchCities() async {
    final result = await _graphQlService.request(_citiesQuery);
    final list = result['cities'] as List<dynamic>? ?? const [];
    return list.map((e) => BuyBuildingCity.fromJson(e as Map<String, dynamic>)).toList();
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

  /// Coordinates of every building the given company already owns — used to
  /// render "distance to existing buildings" on the lot map. Mirrors web's
  /// `cityPlayerBuildings` (`myCompanies { buildings { ... latitude longitude } }`).
  Future<List<OwnedBuildingLocation>> fetchMyBuildingLocations(String companyId) async {
    final result = await _graphQlService.request(_myBuildingLocationsQuery);
    final companies = (result['myCompanies'] as List<dynamic>?) ?? const [];
    for (final company in companies) {
      final map = company as Map<String, dynamic>;
      if (map['id'] == companyId) {
        final buildings = (map['buildings'] as List<dynamic>?) ?? const [];
        return buildings.map((b) => OwnedBuildingLocation.fromJson(b as Map<String, dynamic>)).toList();
      }
    }
    return const [];
  }

  /// Returns the id of the newly built building — used by the screen to
  /// chain the BANK-specific follow-up mutations (`initiateBaseDeposit` /
  /// `setBankRates`) after a successful purchase.
  Future<String> purchaseLot({
    required String companyId,
    required String lotId,
    required String buildingType,
    String? buildingName,
    String? mediaType,
    String? powerPlantType,
  }) async {
    final result = await _graphQlService.request(
      _purchaseLotMutation,
      variables: {
        'input': {
          'companyId': companyId,
          'lotId': lotId,
          'buildingType': buildingType,
          'buildingName': buildingName,
          'mediaType': buildingType == 'MEDIA_HOUSE' ? mediaType : null,
          'powerPlantType': buildingType == 'POWER_PLANT' ? powerPlantType : null,
        },
      },
    );
    return (result['purchaseLot'] as Map<String, dynamic>)['building']['id'] as String;
  }
}
