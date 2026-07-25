import '../../core/graphql/graphql_service.dart';
import 'building_detail_models.dart';

const _myCompaniesQuery = r'''
  query BuildingDetailMyCompanies {
    myCompanies {
      id
      buildings {
        id name type level powerStatus occupancyPercent isForSale
        units { id unitType level resourceTypeId productTypeId minPrice gridX gridY }
        pendingConfiguration { id appliesAtTick totalTicksRequired blockReason }
      }
    }
  }
''';

const _catalogQuery = r'''
  query BuildingDetailCatalog {
    resourceTypes { id name }
    productTypes { id name }
  }
''';

const _scheduleUpgradeMutation = r'''
  mutation ScheduleUnitUpgrade($input: ScheduleUnitUpgradeInput!) {
    scheduleUnitUpgrade(input: $input) { id level }
  }
''';

const _updatePublicSalesPriceMutation = r'''
  mutation UpdatePublicSalesPrice($input: UpdatePublicSalesPriceInput!) {
    updatePublicSalesPrice(input: $input) { id minPrice }
  }
''';

/// GraphQL calls for the (heavily trimmed) Building Detail screen. There is
/// no dedicated per-building query server-side — the web itself loads all
/// `myCompanies.buildings` and finds the target client-side; this service
/// does the same.
class BuildingDetailService {
  const BuildingDetailService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<BuildingDetail?> fetchBuilding(String buildingId) async {
    final result = await _graphQlService.request(_myCompaniesQuery);
    final companies = result['myCompanies'] as List<dynamic>? ?? const [];
    for (final company in companies) {
      final buildings = (company as Map<String, dynamic>)['buildings'] as List<dynamic>? ?? const [];
      for (final building in buildings) {
        if ((building as Map<String, dynamic>)['id'] == buildingId) {
          return BuildingDetail.fromJson(building);
        }
      }
    }
    return null;
  }

  /// Returns `(resourceTypeNamesById, productTypeNamesById)`.
  Future<(Map<String, String>, Map<String, String>)> fetchCatalogNames() async {
    final result = await _graphQlService.request(_catalogQuery);
    final resources = result['resourceTypes'] as List<dynamic>? ?? const [];
    final products = result['productTypes'] as List<dynamic>? ?? const [];
    final resourceNames = {for (final r in resources) (r as Map<String, dynamic>)['id'] as String: r['name'] as String};
    final productNames = {for (final p in products) (p as Map<String, dynamic>)['id'] as String: p['name'] as String};
    return (resourceNames, productNames);
  }

  Future<void> scheduleUnitUpgrade(String unitId) {
    return _graphQlService.request(
      _scheduleUpgradeMutation,
      variables: {
        'input': {'unitId': unitId},
      },
    );
  }

  Future<void> updatePublicSalesPrice({required String unitId, required double newMinPrice}) {
    return _graphQlService.request(
      _updatePublicSalesPriceMutation,
      variables: {
        'input': {'unitId': unitId, 'newMinPrice': newMinPrice},
      },
    );
  }
}
