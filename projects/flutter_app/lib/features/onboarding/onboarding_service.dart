import '../../core/graphql/graphql_service.dart';
import 'onboarding_models.dart';

const _citiesQuery = r'''
  query OnboardingCities {
    cities {
      id name countryCode currencyCode latitude longitude population
      resources { resourceType { name } abundance }
    }
  }
''';

const _starterIndustriesQuery = r'''
  query StarterIndustries {
    starterIndustries { industries proOnlyIndustries }
  }
''';

const _cityLotsQuery = r'''
  query CityLots($cityId: UUID!) {
    cityLots(cityId: $cityId) {
      id cityId name district latitude longitude populationIndex price suitableTypes ownerCompanyId
    }
  }
''';

const _productsQuery = r'''
  query Products($industry: String) {
    productTypes(industry: $industry) {
      id name slug industry basePrice baseCraftTicks description
      recipes { quantity resourceType { name } inputProductType { name } }
    }
  }
''';

const _resumeStateQuery = r'''
  query OnboardingResumeState {
    me {
      onboardingCompletedAtUtc
      onboardingCurrentStep
      onboardingIndustry
      onboardingCityId
      onboardingCompanyId
      onboardingFactoryLotId
      onboardingShopBuildingId
      onboardingFirstSaleCompletedAtUtc
    }
  }
''';

const _startOnboardingCompanyMutation = r'''
  mutation StartOnboardingCompany($input: StartOnboardingCompanyInput!) {
    startOnboardingCompany(input: $input) {
      company { id name cash }
    }
  }
''';

const _finishOnboardingMutation = r'''
  mutation FinishOnboarding($input: FinishOnboardingInput!) {
    finishOnboarding(input: $input) {
      company { id name cash }
      factory { id name type }
      salesShop { id name type }
      selectedProduct { name industry basePrice }
      cityCurrencyCode
    }
  }
''';

const _completeFirstSaleMilestoneMutation = r'''
  mutation CompleteFirstSaleMilestone {
    completeFirstSaleMilestone { onboardingFirstSaleCompletedAtUtc }
  }
''';

/// GraphQL calls for the onboarding wizard, matching the exact operation
/// names/args/return shapes `OnboardingView.vue` uses (see
/// `projects/Api/Types/Mutation.Onboarding*.cs` for the authoritative
/// contract) — not the legacy one-shot `completeOnboarding` mutation, which
/// still exists server-side but isn't used by the current web wizard.
class OnboardingService {
  const OnboardingService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<OnboardingCity>> fetchCities() async {
    final data = await _graphQlService.request(_citiesQuery);
    return ((data['cities'] as List<dynamic>?) ?? const [])
        .map((c) => OnboardingCity.fromJson(c as Map<String, dynamic>))
        .toList();
  }

  Future<StarterIndustries> fetchStarterIndustries() async {
    final data = await _graphQlService.request(_starterIndustriesQuery);
    return StarterIndustries.fromJson(data['starterIndustries'] as Map<String, dynamic>);
  }

  Future<List<CityLot>> fetchCityLots(String cityId) async {
    final data = await _graphQlService.request(_cityLotsQuery, variables: {'cityId': cityId});
    return ((data['cityLots'] as List<dynamic>?) ?? const [])
        .map((l) => CityLot.fromJson(l as Map<String, dynamic>))
        .toList();
  }

  Future<List<OnboardingProductType>> fetchProducts(String industry) async {
    final data = await _graphQlService.request(_productsQuery, variables: {'industry': industry});
    return ((data['productTypes'] as List<dynamic>?) ?? const [])
        .map((p) => OnboardingProductType.fromJson(p as Map<String, dynamic>))
        .toList();
  }

  /// Returns null for a guest (unauthenticated) caller — there is no
  /// resumable server state until a player exists.
  Future<OnboardingResumeState?> fetchResumeState() async {
    final data = await _graphQlService.request(_resumeStateQuery);
    final me = data['me'] as Map<String, dynamic>?;
    if (me == null) return null;
    return OnboardingResumeState.fromJson(me);
  }

  Future<StartOnboardingCompanyResult> startOnboardingCompany({
    required String industry,
    required String cityId,
    required String companyName,
    required String factoryLotId,
    required double ipoRaiseTarget,
  }) async {
    final data = await _graphQlService.request(
      _startOnboardingCompanyMutation,
      variables: {
        'input': {
          'industry': industry,
          'cityId': cityId,
          'companyName': companyName,
          'factoryLotId': factoryLotId,
          'ipoRaiseTarget': ipoRaiseTarget,
        },
      },
    );
    return StartOnboardingCompanyResult.fromJson(data['startOnboardingCompany'] as Map<String, dynamic>);
  }

  Future<OnboardingCompletionResult> finishOnboarding({required String productTypeId, required String shopLotId}) async {
    final data = await _graphQlService.request(
      _finishOnboardingMutation,
      variables: {
        'input': {'productTypeId': productTypeId, 'shopLotId': shopLotId},
      },
    );
    return OnboardingCompletionResult.fromJson(data['finishOnboarding'] as Map<String, dynamic>);
  }

  Future<void> completeFirstSaleMilestone() async {
    await _graphQlService.request(_completeFirstSaleMilestoneMutation);
  }
}
