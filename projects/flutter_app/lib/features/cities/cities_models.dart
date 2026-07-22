// Data models for the Cities and World Map screens, mirroring
// `projects/frontend/src/views/CitiesView.vue` / `WorldMapView.vue`.
// GraphQL field names verified against `Api/Types/Query.World.cs`
// (`GetCities` / `City`/`CityResource` entities) and
// `Api/Types/Query.MultiCityExpansion.cs` (`getCities` /
// `CityExpansionOverview`, note the distinct GraphQL name from the plain
// `cities` query above — the web calls this one for the expansion map).

class CityResourceAbundance {
  const CityResourceAbundance({required this.abundance, required this.resourceName, required this.resourceSlug});

  final double abundance;
  final String resourceName;
  final String resourceSlug;

  factory CityResourceAbundance.fromJson(Map<String, dynamic> json) {
    final resourceType = (json['resourceType'] as Map<String, dynamic>?) ?? const {};
    return CityResourceAbundance(
      abundance: (json['abundance'] as num?)?.toDouble() ?? 0,
      resourceName: (resourceType['name'] as String?) ?? '',
      resourceSlug: (resourceType['slug'] as String?) ?? '',
    );
  }
}

class City {
  const City({
    required this.id,
    required this.name,
    required this.countryCode,
    required this.population,
    required this.currencyCode,
    required this.baseSalaryPerManhour,
    required this.resources,
  });

  final String id;
  final String name;
  final String countryCode;
  final int population;
  final String currencyCode;
  final double baseSalaryPerManhour;
  final List<CityResourceAbundance> resources;

  /// Mirrors `topResources`: sorted by abundance descending, top 4.
  List<CityResourceAbundance> get topResources {
    final sorted = [...resources]..sort((a, b) => b.abundance.compareTo(a.abundance));
    return sorted.take(4).toList();
  }

  factory City.fromJson(Map<String, dynamic> json) => City(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    countryCode: (json['countryCode'] as String?) ?? '',
    population: (json['population'] as num?)?.toInt() ?? 0,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    baseSalaryPerManhour: (json['baseSalaryPerManhour'] as num?)?.toDouble() ?? 0,
    resources: ((json['resources'] as List<dynamic>?) ?? const [])
        .map((e) => CityResourceAbundance.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

/// Mirrors `RESOURCE_ICONS_BY_SLUG` in `CitiesView.vue`.
const Map<String, String> cityResourceIcons = {
  'wood': '🪵',
  'iron-ore': '⛓️',
  'coal': '🪨',
  'gold': '🥇',
  'chemical-minerals': '🧪',
  'cotton': '🧵',
  'grain': '🌾',
  'silicon': '🔹',
};

String cityResourceIcon(String slug) => cityResourceIcons[slug] ?? '📦';

/// Formats a population count like the web's `formatPopulation`.
String formatPopulation(int population) {
  if (population >= 1000000) return '${(population / 1000000).toStringAsFixed(1)}M';
  if (population >= 1000) return '${(population / 1000).toStringAsFixed(0)}K';
  return '$population';
}

class ExpansionCity {
  const ExpansionCity({
    required this.id,
    required this.name,
    required this.countryCode,
    required this.currencyCode,
    required this.population,
    required this.isUnlocked,
    required this.availableLandPlots,
    required this.activeCompanyCount,
    required this.topResourceName,
    required this.requiredNetWorth,
    required this.currentNetWorth,
    required this.progressPercent,
  });

  final String id;
  final String name;
  final String countryCode;
  final String currencyCode;
  final int population;
  final bool isUnlocked;
  final int availableLandPlots;
  final int activeCompanyCount;
  final String? topResourceName;
  final double requiredNetWorth;
  final double currentNetWorth;
  final int progressPercent;

  factory ExpansionCity.fromJson(Map<String, dynamic> json) => ExpansionCity(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    countryCode: (json['countryCode'] as String?) ?? '',
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    population: (json['population'] as num?)?.toInt() ?? 0,
    isUnlocked: json['isUnlocked'] as bool? ?? true,
    availableLandPlots: (json['availableLandPlots'] as num?)?.toInt() ?? 0,
    activeCompanyCount: (json['activeCompanyCount'] as num?)?.toInt() ?? 0,
    topResourceName: json['topResourceName'] as String?,
    requiredNetWorth: (json['requiredNetWorth'] as num?)?.toDouble() ?? 0,
    currentNetWorth: (json['currentNetWorth'] as num?)?.toDouble() ?? 0,
    progressPercent: (json['progressPercent'] as num?)?.toInt() ?? 100,
  );
}
