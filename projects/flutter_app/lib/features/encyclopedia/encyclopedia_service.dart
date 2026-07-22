import '../../core/graphql/graphql_service.dart';
import 'encyclopedia_models.dart';

const _resourcesPageQuery = r'''
  query EncyclopediaResources($page: Int!) {
    encyclopediaResources(page: $page) {
      page totalPages totalCount
      items {
        id kind name slug category industry description imageUrl
        isPerishable isProOnly isUnlockedForCurrentPlayer basePrice
        weightPerUnit baseCraftTicks outputQuantity energyConsumptionMwh
        basicLaborHours unitName unitSymbol
      }
    }
  }
''';

const _entryFields = r'''
  id kind name slug category industry description imageUrl
  isPerishable isProOnly isUnlockedForCurrentPlayer basePrice
  weightPerUnit baseCraftTicks outputQuantity energyConsumptionMwh
  basicLaborHours unitName unitSymbol
''';

const String _resourceDetailQuery =
    '''
  query EncyclopediaResourceDetail(\$slug: String!) {
    encyclopediaResourceDetail(slug: \$slug) {
      entry { $_entryFields }
      producedByRecipes {
        id recipeName buildingType outputQuantity
        output { $_entryFields }
        inputs { kind name slug category industry imageUrl quantity unitName unitSymbol isPerishable isProOnly isUnlockedForCurrentPlayer }
      }
      usedInRecipes {
        id recipeName buildingType outputQuantity
        output { $_entryFields }
        inputs { kind name slug category industry imageUrl quantity unitName unitSymbol isPerishable isProOnly isUnlockedForCurrentPlayer }
      }
    }
  }
''';

/// GraphQL calls for the Manufacturing Encyclopedia catalog and Resource
/// Detail screens, matching `Api/Types/Query.Encyclopedia.cs`'s
/// `encyclopediaResources`/`encyclopediaResourceDetail` queries. Both are
/// public — no authentication required.
class EncyclopediaService {
  const EncyclopediaService(this._graphQlService);

  final GraphQlService _graphQlService;

  /// Mirrors the web's "load every page on mount" behavior for the catalog
  /// view — the server paginates but the client always wants the full list
  /// for local search/filtering.
  Future<List<EncyclopediaEntry>> fetchAllEntries() async {
    final entries = <EncyclopediaEntry>[];
    var page = 1;
    while (true) {
      final result = await _graphQlService.request(_resourcesPageQuery, variables: {'page': page});
      final data = EncyclopediaResourcesPage.fromJson(result['encyclopediaResources'] as Map<String, dynamic>);
      entries.addAll(data.items);
      if (page >= data.totalPages || data.items.isEmpty) break;
      page++;
    }
    return entries;
  }

  Future<EncyclopediaResourceDetail?> fetchResourceDetail(String slug) async {
    final result = await _graphQlService.request(_resourceDetailQuery, variables: {'slug': slug});
    final data = result['encyclopediaResourceDetail'] as Map<String, dynamic>?;
    if (data == null || data['entry'] == null) return null;
    return EncyclopediaResourceDetail.fromJson(data);
  }
}
