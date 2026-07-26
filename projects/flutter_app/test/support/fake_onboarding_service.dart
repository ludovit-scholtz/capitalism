import 'package:capitalism_app/core/graphql/graphql_service.dart';
import 'package:capitalism_app/features/onboarding/onboarding_fx.dart';
import 'package:capitalism_app/features/onboarding/onboarding_models.dart';
import 'package:capitalism_app/features/onboarding/onboarding_service.dart';

/// Full in-memory fake of [OnboardingService] (`implements`, not a mock —
/// `OnboardingService` isn't abstract, so this satisfies its public API
/// without touching GraphQL at all). Records every call in [calls] so tests
/// can assert on args (e.g. the exact `factoryLotId` sent to
/// `startOnboardingCompany`) and on call counts (e.g. "no calls at all"
/// during guest steps).
class FakeOnboardingService implements OnboardingService {
  FakeOnboardingService({
    this.cities = const [],
    this.starterIndustries = const StarterIndustries(industries: [], proOnlyIndustries: []),
    this.productsByIndustry = const {},
    this.lotsByCity = const {},
    this.fxRates = OnboardingFxRates.empty,
    this.resumeState,
    this.startOnboardingCompanyResult,
    this.startOnboardingCompanyError,
    this.finishOnboardingResult,
    this.finishOnboardingError,
    this.completeFirstSaleMilestoneError,
    this.firstSaleMissionStatuses = const [],
  });

  final List<OnboardingCity> cities;
  final StarterIndustries starterIndustries;
  final Map<String, List<OnboardingProductType>> productsByIndustry;
  final Map<String, List<CityLot>> lotsByCity;
  final OnboardingFxRates fxRates;
  final OnboardingResumeState? resumeState;
  final StartOnboardingCompanyResult? startOnboardingCompanyResult;
  final GraphQlException? startOnboardingCompanyError;
  final OnboardingCompletionResult? finishOnboardingResult;
  final GraphQlException? finishOnboardingError;
  final GraphQlException? completeFirstSaleMilestoneError;

  /// Successive results returned by [fetchFirstSaleMission] calls, in order
  /// (the last entry repeats once exhausted) — lets a test simulate a
  /// mission progressing across ticks, e.g. AWAITING_FIRST_SALE then
  /// FIRST_SALE_RECORDED.
  final List<FirstSaleMissionStatus> firstSaleMissionStatuses;

  int _fetchFirstSaleMissionCallCount = 0;

  final List<String> calls = [];

  @override
  Future<List<OnboardingCity>> fetchCities() async {
    calls.add('fetchCities');
    return cities;
  }

  @override
  Future<StarterIndustries> fetchStarterIndustries() async {
    calls.add('fetchStarterIndustries');
    return starterIndustries;
  }

  @override
  Future<List<CityLot>> fetchCityLots(String cityId) async {
    calls.add('fetchCityLots:$cityId');
    return lotsByCity[cityId] ?? const [];
  }

  @override
  Future<List<OnboardingProductType>> fetchProducts(String industry) async {
    calls.add('fetchProducts:$industry');
    return productsByIndustry[industry] ?? const [];
  }

  @override
  Future<OnboardingFxRates> fetchFxRates() async {
    calls.add('fetchFxRates');
    return fxRates;
  }

  @override
  Future<OnboardingResumeState?> fetchResumeState() async {
    calls.add('fetchResumeState');
    return resumeState;
  }

  @override
  Future<StartOnboardingCompanyResult> startOnboardingCompany({
    required String industry,
    required String cityId,
    required String companyName,
    required String factoryLotId,
    required double ipoRaiseTarget,
  }) async {
    calls.add('startOnboardingCompany:$industry:$cityId:$factoryLotId:$ipoRaiseTarget');
    if (startOnboardingCompanyError != null) throw startOnboardingCompanyError!;
    return startOnboardingCompanyResult ??
        StartOnboardingCompanyResult(companyId: 'company-1', companyName: companyName, companyCash: 400000);
  }

  @override
  Future<OnboardingCompletionResult> finishOnboarding({required String productTypeId, required String shopLotId}) async {
    calls.add('finishOnboarding:$productTypeId:$shopLotId');
    if (finishOnboardingError != null) throw finishOnboardingError!;
    return finishOnboardingResult ??
        const OnboardingCompletionResult(
          companyName: 'Test Co',
          companyCash: 350000,
          factoryId: 'factory-1',
          salesShopId: 'shop-1',
          selectedProductName: 'Wooden Chair',
          cityCurrencyCode: 'USD',
        );
  }

  @override
  Future<FirstSaleMissionStatus> fetchFirstSaleMission() async {
    calls.add('fetchFirstSaleMission');
    if (firstSaleMissionStatuses.isEmpty) {
      return const FirstSaleMissionStatus(
        phase: 'NO_SHOP',
        shopBuildingId: null,
        shopName: null,
        blockers: [],
        firstSaleRevenue: null,
        firstSaleProductName: null,
        firstSaleTick: null,
        firstSaleQuantity: null,
        firstSalePricePerUnit: null,
      );
    }
    final index = _fetchFirstSaleMissionCallCount < firstSaleMissionStatuses.length
        ? _fetchFirstSaleMissionCallCount
        : firstSaleMissionStatuses.length - 1;
    _fetchFirstSaleMissionCallCount++;
    return firstSaleMissionStatuses[index];
  }

  @override
  Future<void> completeFirstSaleMilestone() async {
    calls.add('completeFirstSaleMilestone');
    if (completeFirstSaleMilestoneError != null) throw completeFirstSaleMilestoneError!;
  }
}
