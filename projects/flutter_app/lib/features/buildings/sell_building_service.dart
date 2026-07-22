import '../../core/graphql/graphql_service.dart';
import 'sell_building_models.dart';

const _myCompaniesQuery = r'''
  query SellBuildingMyCompanies {
    myCompanies {
      id name
      buildings {
        id name type level isForSale askingPrice listedAtUtc isCollateralized foreclosureTicksRemaining
        marketValuation { landValue structureValue unitsValue totalValue minimumSalePrice currencyCode }
      }
    }
  }
''';

const _setForSaleMutation = r'''
  mutation SetBuildingForSale($input: SetBuildingForSaleInput!) {
    setBuildingForSale(input: $input) { id isForSale askingPrice listedAtUtc }
  }
''';

const _destroyMutation = r'''
  mutation DestroyBuilding($input: DestroyBuildingInput!) {
    destroyBuilding(input: $input) { buildingId buildingName refundAmount currencyCode }
  }
''';

/// GraphQL calls for the Sell Building screen, matching
/// `projects/frontend/src/views/SellBuildingView.vue`. No dedicated
/// per-building query exists server-side — the web loads all
/// `myCompanies.buildings` and finds the target client-side; this service
/// does the same.
class SellBuildingService {
  const SellBuildingService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<SellableBuilding?> fetchBuilding(String buildingId) async {
    final result = await _graphQlService.request(_myCompaniesQuery);
    final companies = result['myCompanies'] as List<dynamic>? ?? const [];
    for (final company in companies) {
      final buildings = (company as Map<String, dynamic>)['buildings'] as List<dynamic>? ?? const [];
      for (final building in buildings) {
        if ((building as Map<String, dynamic>)['id'] == buildingId) {
          return SellableBuilding.fromJson(building);
        }
      }
    }
    return null;
  }

  Future<void> setForSale({required String buildingId, required bool isForSale, double? askingPrice}) {
    return _graphQlService.request(
      _setForSaleMutation,
      variables: {
        'input': {'buildingId': buildingId, 'isForSale': isForSale, 'askingPrice': askingPrice},
      },
    );
  }

  Future<void> destroyBuilding(String buildingId) {
    return _graphQlService.request(
      _destroyMutation,
      variables: {
        'input': {'buildingId': buildingId},
      },
    );
  }
}
