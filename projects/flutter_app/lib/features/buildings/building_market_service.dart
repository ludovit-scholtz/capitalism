import '../../core/graphql/graphql_service.dart';
import 'building_market_models.dart';

const _buildingFields = r'''
  id name type isForSale askingPrice level isCollateralized foreclosureTicksRemaining
  city { id name currencyCode countryCode }
  company { id name player { displayName } }
''';

const String _marketQuery =
    '''
  query BuildingMarket(\$cityId: UUID, \$buildingType: String, \$maxPrice: Decimal) {
    buildingMarket(cityId: \$cityId, buildingType: \$buildingType, maxPrice: \$maxPrice) {
      pendingOfferCount
      building { $_buildingFields }
    }
  }
''';

const String _myListingsQuery =
    '''
  query MyBuildingListings {
    myBuildingListings {
      building { $_buildingFields }
      offers { id offerVersion offeredPrice status negotiationNote createdAtUtc resolvedAtUtc buyerPlayer { displayName } buyerCompany { id name } }
    }
  }
''';

const _citiesQuery = r'''
  query BuildingMarketCities { cities { id name } }
''';

const _myCompaniesQuery = r'''
  query BuildingMarketMyCompanies { me { companies { id name } } }
''';

const _makeOfferMutation = r'''
  mutation MakeOffer($input: MakeOfferOnBuildingInput!) {
    makeOfferOnBuilding(input: $input) { id offeredPrice status }
  }
''';

const _acceptOfferMutation = r'''
  mutation AcceptOffer($input: AcceptBuildingOfferInput!) {
    acceptBuildingOffer(input: $input) { building { id name companyId isForSale } offer { id status } }
  }
''';

const _rejectOfferMutation = r'''
  mutation RejectOffer($input: RejectBuildingOfferInput!) {
    rejectBuildingOffer(input: $input) { id status }
  }
''';

/// GraphQL calls for the Building Market screen, matching
/// `projects/frontend/src/views/BuildingMarketView.vue`'s exact contract.
class BuildingMarketService {
  const BuildingMarketService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<MarketBuilding>> fetchMarket({String? cityId, String? buildingType, double? maxPrice}) async {
    final result = await _graphQlService.request(
      _marketQuery,
      variables: {'cityId': cityId, 'buildingType': buildingType, 'maxPrice': maxPrice},
    );
    final list = result['buildingMarket'] as List<dynamic>? ?? const [];
    return list
        .map((e) => MarketBuilding.fromJson(((e as Map<String, dynamic>)['building']) as Map<String, dynamic>))
        .toList();
  }

  Future<List<MyBuildingListing>> fetchMyListings() async {
    final result = await _graphQlService.request(_myListingsQuery);
    final list = result['myBuildingListings'] as List<dynamic>? ?? const [];
    return list.map((e) => MyBuildingListing.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<Map<String, String>>> fetchCities() async {
    final result = await _graphQlService.request(_citiesQuery);
    final list = result['cities'] as List<dynamic>? ?? const [];
    return list
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'name': e['name'] as String})
        .toList();
  }

  Future<List<Map<String, String>>> fetchMyCompanies() async {
    final result = await _graphQlService.request(_myCompaniesQuery);
    final companies = (result['me'] as Map<String, dynamic>?)?['companies'] as List<dynamic>? ?? const [];
    return companies
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'name': e['name'] as String})
        .toList();
  }

  Future<void> makeOffer({required String buildingId, required String buyerCompanyId, required double offeredPrice}) {
    return _graphQlService.request(
      _makeOfferMutation,
      variables: {
        'input': {'buildingId': buildingId, 'buyerCompanyId': buyerCompanyId, 'offeredPrice': offeredPrice},
      },
    );
  }

  Future<void> acceptOffer({required String offerId, required int offerVersion}) {
    return _graphQlService.request(
      _acceptOfferMutation,
      variables: {
        'input': {'offerId': offerId, 'offerVersion': offerVersion},
      },
    );
  }

  Future<void> rejectOffer({required String offerId, required int offerVersion}) {
    return _graphQlService.request(
      _rejectOfferMutation,
      variables: {
        'input': {'offerId': offerId, 'offerVersion': offerVersion},
      },
    );
  }
}
