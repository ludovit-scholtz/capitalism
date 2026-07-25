// GraphQL calls for the Global Exchange sourcing/vendor-selector comparison
// surfaces for PURCHASE units (ROADMAP 136). Field names/argument shapes
// verified against `useBuildingDetail.ts`'s `loadGlobalExchangeOffers`/
// `loadProcurementPreview`/`loadSourcingCandidates`.

import '../../core/graphql/graphql_service.dart';
import 'building_sourcing_models.dart';

const _globalExchangeOffersQuery = r'''
  query GlobalExchangeOffers($destinationCityId: UUID!, $resourceTypeId: UUID) {
    globalExchangeOffers(destinationCityId: $destinationCityId, resourceTypeId: $resourceTypeId) {
      cityId cityName resourceName unitSymbol
      exchangePricePerUnit estimatedQuality transitCostPerUnit deliveredPricePerUnit distanceKm
    }
  }
''';

const _procurementPreviewQuery = r'''
  query ProcurementPreview($unitId: UUID!) {
    procurementPreview(buildingUnitId: $unitId) {
      sourceType sourceCityName sourceVendorName
      exchangePricePerUnit transitCostPerUnit deliveredPricePerUnit estimatedQuality
      canExecute blockReason blockMessage
    }
  }
''';

const _sourcingCandidatesQuery = r'''
  query SourcingCandidates($unitId: UUID!) {
    sourcingCandidates(buildingUnitId: $unitId) {
      sourceType sourceCityName sourceVendorName
      exchangePricePerUnit transitCostPerUnit deliveredPricePerUnit estimatedQuality
      distanceKm isEligible blockReason blockMessage isRecommended rank
    }
  }
''';

class BuildingSourcingService {
  const BuildingSourcingService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<GlobalExchangeOffer>> fetchGlobalExchangeOffers({required String destinationCityId, String? resourceTypeId}) async {
    final result = await _graphQlService.request(
      _globalExchangeOffersQuery,
      variables: {'destinationCityId': destinationCityId, 'resourceTypeId': resourceTypeId},
    );
    final list = result['globalExchangeOffers'] as List<dynamic>? ?? const [];
    return list.map((e) => GlobalExchangeOffer.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<ProcurementPreview?> fetchProcurementPreview(String unitId) async {
    final result = await _graphQlService.request(_procurementPreviewQuery, variables: {'unitId': unitId});
    final data = result['procurementPreview'] as Map<String, dynamic>?;
    return data == null ? null : ProcurementPreview.fromJson(data);
  }

  Future<List<SourcingCandidate>> fetchSourcingCandidates(String unitId) async {
    final result = await _graphQlService.request(_sourcingCandidatesQuery, variables: {'unitId': unitId});
    final list = result['sourcingCandidates'] as List<dynamic>? ?? const [];
    return list.map((e) => SourcingCandidate.fromJson(e as Map<String, dynamic>)).toList();
  }
}
