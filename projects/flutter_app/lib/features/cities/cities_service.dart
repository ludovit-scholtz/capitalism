import '../../core/graphql/graphql_service.dart';
import 'cities_models.dart';

const _citiesQuery = r'''
  query CitiesOverview {
    cities {
      id name countryCode population currencyCode baseSalaryPerManhour
      resources { abundance resourceType { name slug } }
    }
  }
''';

const _expansionCitiesQuery = r'''
  query WorldMapCities {
    getCities {
      id name countryCode currencyCode latitude longitude population isUnlocked
      availableLandPlots activeCompanyCount topResourceName
      requiredNetWorth currentNetWorth progressPercent
    }
  }
''';

const _myFirstCompanyQuery = r'''
  query MyFirstCompany { me { companies { id } } }
''';

/// GraphQL calls for the Cities directory and World Map (expansion)
/// screens, matching `CitiesView.vue` / `WorldMapView.vue`.
class CitiesService {
  const CitiesService(this._graphQlService);

  final GraphQlService _graphQlService;

  /// Mirrors the web's client-side `sort((a, b) => b.population - a.population)`.
  Future<List<City>> fetchCities() async {
    final result = await _graphQlService.request(_citiesQuery);
    final list = result['cities'] as List<dynamic>? ?? const [];
    final cities = list.map((e) => City.fromJson(e as Map<String, dynamic>)).toList();
    cities.sort((a, b) => b.population.compareTo(a.population));
    return cities;
  }

  /// `getCities` — the expansion-aware overview with unlock/progress data,
  /// distinct from the plain `cities` query above (verified against
  /// `Api/Types/Query.MultiCityExpansion.cs`'s `[GraphQLName("getCities")]`).
  Future<List<ExpansionCity>> fetchExpansionCities() async {
    final result = await _graphQlService.request(_expansionCitiesQuery);
    final list = result['getCities'] as List<dynamic>? ?? const [];
    return list.map((e) => ExpansionCity.fromJson(e as Map<String, dynamic>)).toList();
  }

  /// Used for "Start expanding" when no company is known yet.
  Future<String?> fetchMyFirstCompanyId() async {
    final result = await _graphQlService.request(_myFirstCompanyQuery);
    final companies = (result['me'] as Map<String, dynamic>?)?['companies'] as List<dynamic>?;
    if (companies == null || companies.isEmpty) return null;
    return (companies.first as Map<String, dynamic>)['id'] as String?;
  }
}
