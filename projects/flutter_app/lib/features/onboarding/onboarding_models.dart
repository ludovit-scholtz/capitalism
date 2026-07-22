// Data models + constants for the onboarding wizard, mirroring
// `projects/frontend/src/views/OnboardingView.vue` and its GraphQL contract.
// GraphQL field names verified against `projects/Api/Types/*.cs`.

List<String> _stringList(Object? raw) {
  if (raw is List) return raw.map((e) => e.toString()).toList();
  if (raw is String) {
    return raw.split(',').map((s) => s.trim()).where((s) => s.isNotEmpty).toList();
  }
  return const [];
}

class OnboardingCityResource {
  const OnboardingCityResource({required this.resourceName, required this.abundance});

  final String resourceName;
  final double abundance;

  factory OnboardingCityResource.fromJson(Map<String, dynamic> json) {
    final resourceType = json['resourceType'] as Map<String, dynamic>?;
    return OnboardingCityResource(
      resourceName: (resourceType?['name'] as String?) ?? 'Unknown',
      abundance: (json['abundance'] as num?)?.toDouble() ?? 0,
    );
  }
}

class OnboardingCity {
  const OnboardingCity({
    required this.id,
    required this.name,
    required this.countryCode,
    required this.currencyCode,
    required this.latitude,
    required this.longitude,
    required this.population,
    required this.resources,
  });

  final String id;
  final String name;
  final String countryCode;
  final String currencyCode;
  final double latitude;
  final double longitude;
  final int population;
  final List<OnboardingCityResource> resources;

  factory OnboardingCity.fromJson(Map<String, dynamic> json) => OnboardingCity(
    id: json['id'] as String,
    name: json['name'] as String,
    countryCode: (json['countryCode'] as String?) ?? '',
    currencyCode: (json['currencyCode'] as String?) ?? 'USD',
    latitude: (json['latitude'] as num?)?.toDouble() ?? 0,
    longitude: (json['longitude'] as num?)?.toDouble() ?? 0,
    population: (json['population'] as num?)?.toInt() ?? 0,
    resources: ((json['resources'] as List<dynamic>?) ?? const [])
        .map((r) => OnboardingCityResource.fromJson(r as Map<String, dynamic>))
        .toList(),
  );
}

class CityLot {
  const CityLot({
    required this.id,
    required this.cityId,
    required this.name,
    required this.district,
    required this.latitude,
    required this.longitude,
    required this.populationIndex,
    required this.price,
    required this.suitableTypes,
    required this.ownerCompanyId,
  });

  final String id;
  final String cityId;
  final String name;
  final String district;
  final double latitude;
  final double longitude;
  final double populationIndex;
  final double price;
  final List<String> suitableTypes;
  final String? ownerCompanyId;

  bool get isOwned => ownerCompanyId != null;

  bool suitableFor(String buildingType) => suitableTypes.contains(buildingType);

  /// Used to mark a lot as taken in local (guest-mode) state after a
  /// simulated purchase, since there's no backend row to re-fetch.
  CityLot copyAsOwned() => CityLot(
    id: id,
    cityId: cityId,
    name: name,
    district: district,
    latitude: latitude,
    longitude: longitude,
    populationIndex: populationIndex,
    price: price,
    suitableTypes: suitableTypes,
    ownerCompanyId: 'guest',
  );

  factory CityLot.fromJson(Map<String, dynamic> json) => CityLot(
    id: json['id'] as String,
    cityId: json['cityId'] as String,
    name: json['name'] as String,
    district: (json['district'] as String?) ?? '',
    latitude: (json['latitude'] as num?)?.toDouble() ?? 0,
    longitude: (json['longitude'] as num?)?.toDouble() ?? 0,
    populationIndex: (json['populationIndex'] as num?)?.toDouble() ?? 0,
    price: (json['price'] as num?)?.toDouble() ?? 0,
    suitableTypes: _stringList(json['suitableTypes']),
    ownerCompanyId: json['ownerCompanyId'] as String?,
  );
}

class ProductRecipeEntry {
  const ProductRecipeEntry({required this.quantity, required this.ingredientName});

  final double quantity;
  final String ingredientName;

  factory ProductRecipeEntry.fromJson(Map<String, dynamic> json) {
    final resource = json['resourceType'] as Map<String, dynamic>?;
    final inputProduct = json['inputProductType'] as Map<String, dynamic>?;
    return ProductRecipeEntry(
      quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
      ingredientName: (resource?['name'] as String?) ?? (inputProduct?['name'] as String?) ?? 'Unknown',
    );
  }
}

class OnboardingProductType {
  const OnboardingProductType({
    required this.id,
    required this.name,
    required this.slug,
    required this.industry,
    required this.basePrice,
    required this.baseCraftTicks,
    required this.description,
    required this.recipes,
  });

  final String id;
  final String name;
  final String slug;
  final String industry;
  final double basePrice;
  final int baseCraftTicks;
  final String description;
  final List<ProductRecipeEntry> recipes;

  factory OnboardingProductType.fromJson(Map<String, dynamic> json) => OnboardingProductType(
    id: json['id'] as String,
    name: json['name'] as String,
    slug: (json['slug'] as String?) ?? '',
    industry: (json['industry'] as String?) ?? '',
    basePrice: (json['basePrice'] as num?)?.toDouble() ?? 0,
    baseCraftTicks: (json['baseCraftTicks'] as num?)?.toInt() ?? 0,
    description: (json['description'] as String?) ?? '',
    recipes: ((json['recipes'] as List<dynamic>?) ?? const [])
        .map((r) => ProductRecipeEntry.fromJson(r as Map<String, dynamic>))
        .toList(),
  );
}

class StarterIndustries {
  const StarterIndustries({required this.industries, required this.proOnlyIndustries});

  final List<String> industries;
  final List<String> proOnlyIndustries;

  factory StarterIndustries.fromJson(Map<String, dynamic> json) => StarterIndustries(
    industries: _stringList(json['industries']),
    proOnlyIndustries: _stringList(json['proOnlyIndustries']),
  );
}

class OnboardingResumeState {
  const OnboardingResumeState({
    required this.onboardingCompletedAtUtc,
    required this.onboardingCurrentStep,
    required this.onboardingIndustry,
    required this.onboardingCityId,
    required this.onboardingCompanyId,
    required this.onboardingFactoryLotId,
    required this.onboardingShopBuildingId,
    required this.onboardingFirstSaleCompletedAtUtc,
  });

  final String? onboardingCompletedAtUtc;
  final String? onboardingCurrentStep;
  final String? onboardingIndustry;
  final String? onboardingCityId;
  final String? onboardingCompanyId;
  final String? onboardingFactoryLotId;
  final String? onboardingShopBuildingId;
  final String? onboardingFirstSaleCompletedAtUtc;

  factory OnboardingResumeState.fromJson(Map<String, dynamic> json) => OnboardingResumeState(
    onboardingCompletedAtUtc: json['onboardingCompletedAtUtc'] as String?,
    onboardingCurrentStep: json['onboardingCurrentStep'] as String?,
    onboardingIndustry: json['onboardingIndustry'] as String?,
    onboardingCityId: json['onboardingCityId'] as String?,
    onboardingCompanyId: json['onboardingCompanyId'] as String?,
    onboardingFactoryLotId: json['onboardingFactoryLotId'] as String?,
    onboardingShopBuildingId: json['onboardingShopBuildingId'] as String?,
    onboardingFirstSaleCompletedAtUtc: json['onboardingFirstSaleCompletedAtUtc'] as String?,
  );
}

class StartOnboardingCompanyResult {
  const StartOnboardingCompanyResult({required this.companyId, required this.companyName, required this.companyCash});

  final String companyId;
  final String companyName;
  final double companyCash;

  factory StartOnboardingCompanyResult.fromJson(Map<String, dynamic> json) {
    final company = json['company'] as Map<String, dynamic>;
    return StartOnboardingCompanyResult(
      companyId: company['id'] as String,
      companyName: company['name'] as String,
      companyCash: (company['cash'] as num).toDouble(),
    );
  }
}

class OnboardingCompletionResult {
  const OnboardingCompletionResult({
    required this.companyName,
    required this.companyCash,
    required this.factoryId,
    required this.salesShopId,
    required this.selectedProductName,
    required this.cityCurrencyCode,
  });

  final String companyName;
  final double companyCash;
  final String factoryId;
  final String salesShopId;
  final String selectedProductName;
  final String cityCurrencyCode;

  factory OnboardingCompletionResult.fromJson(Map<String, dynamic> json) {
    final company = json['company'] as Map<String, dynamic>;
    final factory = json['factory'] as Map<String, dynamic>;
    final salesShop = json['salesShop'] as Map<String, dynamic>;
    final selectedProduct = json['selectedProduct'] as Map<String, dynamic>?;
    return OnboardingCompletionResult(
      companyName: company['name'] as String,
      companyCash: (company['cash'] as num).toDouble(),
      factoryId: factory['id'] as String,
      salesShopId: salesShop['id'] as String,
      selectedProductName: (selectedProduct?['name'] as String?) ?? '',
      cityCurrencyCode: (json['cityCurrencyCode'] as String?) ?? 'USD',
    );
  }
}

/// Fixed IPO plan choices — hardcoded on the web too (`OnboardingView.vue`),
/// mirrored server-side by `ResolveStarterIpoSelection`.
class IpoPlan {
  const IpoPlan({required this.raiseTarget, required this.founderOwnershipRatio, required this.label});

  final double raiseTarget;
  final double founderOwnershipRatio;
  final String label;
}

const List<IpoPlan> onboardingIpoPlans = [
  IpoPlan(raiseTarget: 200000, founderOwnershipRatio: 0.5, label: 'Conservative'),
  IpoPlan(raiseTarget: 400000, founderOwnershipRatio: 0.3333, label: 'Balanced'),
  IpoPlan(raiseTarget: 600000, founderOwnershipRatio: 0.25, label: 'Aggressive'),
];

const double onboardingFounderContribution = 200000;

/// Starter product slugs per industry — must stay in sync with the backend's
/// `StarterOnboardingProductsByIndustry` (`Api/Types/Mutation.cs`), which is
/// the authoritative validation. The frontend uses this same mapping to
/// client-side-filter the `productTypes` query rather than relying on the
/// server to restrict the list, and this port does the same.
const Map<String, List<String>> starterProductSlugsByIndustry = {
  'FURNITURE': ['wooden-chair', 'wooden-table', 'wooden-bed'],
  'FOOD_PROCESSING': ['bread', 'pasta', 'crackers'],
  'HEALTHCARE': ['basic-medicine', 'bandages', 'first-aid-kit'],
  'ELECTRONICS': ['basic-electronics', 'led-screen', 'circuit-board'],
  'CONSTRUCTION': ['residential-block', 'commercial-block', 'industrial-block'],
  'PHARMACEUTICALS': ['aspirin', 'vitamin-capsule', 'antibiotic'],
  'ENERGY': ['coal-briquette', 'heating-oil', 'industrial-fuel'],
  'LOGISTICS': ['shipping-bag', 'storage-sack', 'cargo-pack'],
};
