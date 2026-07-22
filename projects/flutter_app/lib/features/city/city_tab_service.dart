import '../../core/graphql/graphql_service.dart';
import '../buildings/buy_building_models.dart';
import '../cities/cities_models.dart';
import 'city_tab_models.dart';

const _cityQuery = r'''
  query CityTabDetail($id: UUID!) {
    city(id: $id) {
      id name countryCode population currencyCode baseSalaryPerManhour
      resources { abundance resourceType { name slug } }
    }
  }
''';

const _lotsQuery = r'''
  query CityTabLots($cityId: UUID!) {
    cityLots(cityId: $cityId) {
      id cityId name description district latitude longitude populationIndex basePrice price suitableTypes
      ownerCompanyId buildingId
    }
  }
''';

const _competitorsQuery = r'''
  query CityCompetitors($cityId: UUID!, $lastNTicks: Int!) {
    cityCompetitors(cityId: $cityId, lastNTicks: $lastNTicks) {
      companyId companyName isNpc npcCompanyId archetype buildingCount
      estimatedRevenueLastTicks marketSharePercent trend
      marketShareByCategory { category sharePercent }
    }
  }
''';

const _contractsQuery = r'''
  query CityGovernmentContracts($cityId: UUID!) {
    gameState { currentTick }
    cityGovernmentContracts(cityId: $cityId, status: "OPEN") {
      id cityId cityName currencyCode title description productTypeId productName quantityRequired minimumQuality budgetCap
      deadlineTick status winnerCompanyId winnerCompanyName createdAtTick bidCount awardedBidPricePerUnit
      fulfilledQuantity fulfillmentPercent
    }
  }
''';

const _contractDetailQuery = r'''
  query ContractDetail($contractId: UUID!, $companyId: UUID) {
    contractDetail(contractId: $contractId, companyId: $companyId) {
      competingBidCount
      eligibility { isEligible reasonCode reasonMessage currentQualityLevel }
      contract { id }
    }
  }
''';

const _submitBidMutation = r'''
  mutation SubmitContractBid($input: SubmitContractBidInput!) {
    submitContractBid(input: $input) { id }
  }
''';

const _myCompaniesQuery = r'''
  query CityTabMyCompanies { me { companies { id name } } }
''';

/// GraphQL calls for the six City tab screens, matching
/// `projects/frontend/src/components/cityTabs/*.vue`. Each tab is a
/// standalone route/screen in this app (rather than a nested tab of one
/// shared parent view, unlike the web) and fetches its own data.
class CityTabService {
  const CityTabService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<City?> fetchCity(String cityId) async {
    final result = await _graphQlService.request(_cityQuery, variables: {'id': cityId});
    final data = result['city'] as Map<String, dynamic>?;
    return data == null ? null : City.fromJson(data);
  }

  Future<List<CityLot>> fetchLots(String cityId) async {
    final result = await _graphQlService.request(_lotsQuery, variables: {'cityId': cityId});
    final list = result['cityLots'] as List<dynamic>? ?? const [];
    return list.map((e) => CityLot.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<CityCompetitor>> fetchCompetitors(String cityId, {int lastNTicks = 10}) async {
    final result = await _graphQlService.request(_competitorsQuery, variables: {'cityId': cityId, 'lastNTicks': lastNTicks});
    final list = result['cityCompetitors'] as List<dynamic>? ?? const [];
    return list.map((e) => CityCompetitor.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<GovernmentContractCard>> fetchOpenContracts(String cityId) async {
    final result = await _graphQlService.request(_contractsQuery, variables: {'cityId': cityId});
    final list = result['cityGovernmentContracts'] as List<dynamic>? ?? const [];
    return list.map((e) => GovernmentContractCard.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<ContractEligibility?> fetchEligibility({required String contractId, required String companyId}) async {
    final result = await _graphQlService.request(
      _contractDetailQuery,
      variables: {'contractId': contractId, 'companyId': companyId},
    );
    final detail = result['contractDetail'] as Map<String, dynamic>?;
    final eligibility = detail?['eligibility'] as Map<String, dynamic>?;
    return eligibility == null ? null : ContractEligibility.fromJson(eligibility);
  }

  Future<List<Map<String, String>>> fetchMyCompanies() async {
    final result = await _graphQlService.request(_myCompaniesQuery);
    final companies = (result['me'] as Map<String, dynamic>?)?['companies'] as List<dynamic>? ?? const [];
    return companies
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'name': e['name'] as String})
        .toList();
  }

  Future<void> submitBid({
    required String contractId,
    required String companyId,
    required double bidPricePerUnit,
    required int estimatedDeliveryTick,
  }) {
    return _graphQlService.request(
      _submitBidMutation,
      variables: {
        'input': {
          'contractId': contractId,
          'companyId': companyId,
          'bidPricePerUnit': bidPricePerUnit,
          'estimatedDeliveryTick': estimatedDeliveryTick,
        },
      },
    );
  }
}
