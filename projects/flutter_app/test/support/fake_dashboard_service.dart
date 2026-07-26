import 'package:capitalism_app/features/buildings/building_analytics_models.dart';
import 'package:capitalism_app/features/buildings/building_panel_models.dart';
import 'package:capitalism_app/features/company/company_models.dart';
import 'package:capitalism_app/features/dashboard/dashboard_models.dart';
import 'package:capitalism_app/features/dashboard/dashboard_service.dart';

/// Full in-memory fake of [DashboardService] (`implements`, not an HTTP
/// mock — see `test/support/fake_onboarding_service.dart` for why this
/// pattern is preferred once a service has more than 2-3 operations).
class FakeDashboardService implements DashboardService {
  FakeDashboardService({
    this.onboardingCompleted = true,
    this.data = const DashboardData(companies: [], currentTick: 100, taxRate: 15, pendingActions: []),
    this.ledgersByCompanyId = const {},
    this.newCompanyPrerequisites = const AdditionalCompanyPrerequisites(
      companyCount: 1,
      underMaxCap: true,
      hasExistingCompany: true,
      companyAgeRequirementMet: false,
      ticksUntilAgeRequirementMet: 100,
      profitabilityRequirementMet: false,
      balanceRequirementMet: false,
      allRequirementsMet: false,
    ),
    this.newCompanyCities = const [],
    this.startAdditionalCompanyResult,
    this.startAdditionalCompanyError,
    this.fetchDataError,
    this.removeDestroyedBuildingError,
    this.financialsByBuildingId = const {},
    this.unitStatusesByBuildingId = const {},
    this.powerBalanceByCityId = const {},
    this.buildingFinancialsError,
    this.buildingUnitStatusesError,
    this.cityPowerBalanceError,
    this.proSubscriptionEndsAtUtc,
  });

  final bool? onboardingCompleted;
  final DashboardData data;
  final Map<String, CompanyLedger> ledgersByCompanyId;
  final AdditionalCompanyPrerequisites newCompanyPrerequisites;
  final List<NewCompanyCity> newCompanyCities;
  final NewCompanyResult? startAdditionalCompanyResult;
  final Object? startAdditionalCompanyError;
  final Object? fetchDataError;
  final Object? removeDestroyedBuildingError;
  final Map<String, BuildingFinancialTimeline> financialsByBuildingId;
  final Map<String, List<BuildingUnitOperationalStatus>> unitStatusesByBuildingId;
  final Map<String, CityPowerBalance> powerBalanceByCityId;
  final Object? buildingFinancialsError;
  final Object? buildingUnitStatusesError;
  final Object? cityPowerBalanceError;
  final String? proSubscriptionEndsAtUtc;

  final List<String> calls = [];
  int fetchDashboardDataCallCount = 0;
  int fetchCompanyOverviewLedgerCallCount = 0;
  int fetchCityPowerBalanceCallCount = 0;
  Map<String, dynamic>? lastStartAdditionalCompanyArgs;
  String? lastRemovedBuildingId;

  @override
  Future<bool?> fetchOnboardingCompleted() async {
    calls.add('fetchOnboardingCompleted');
    return onboardingCompleted;
  }

  @override
  Future<String?> fetchProSubscriptionEndsAtUtc() async {
    calls.add('fetchProSubscriptionEndsAtUtc');
    return proSubscriptionEndsAtUtc;
  }

  @override
  Future<DashboardData> fetchDashboardData() async {
    calls.add('fetchDashboardData');
    fetchDashboardDataCallCount++;
    if (fetchDataError != null) throw fetchDataError!;
    return data;
  }

  @override
  Future<CompanyLedger> fetchCompanyOverviewLedger(String companyId) async {
    calls.add('fetchCompanyOverviewLedger');
    fetchCompanyOverviewLedgerCallCount++;
    return ledgersByCompanyId[companyId] ??
        const CompanyLedger(
          companyName: '',
          gameYear: 1,
          currentCash: 0,
          primaryCurrencyCode: 'USD',
          totalRevenue: 0,
          totalPurchasingCosts: 0,
          totalShippingCosts: 0,
          totalLaborCosts: 0,
          totalEnergyCosts: 0,
          totalMarketingCosts: 0,
          totalTaxPaid: 0,
          totalOtherCosts: 0,
          netIncome: 0,
          totalAssets: 0,
        );
  }

  @override
  Future<(AdditionalCompanyPrerequisites, List<NewCompanyCity>)> fetchAdditionalCompanyPrerequisites() async {
    calls.add('fetchAdditionalCompanyPrerequisites');
    return (newCompanyPrerequisites, newCompanyCities);
  }

  @override
  Future<NewCompanyResult> startAdditionalCompany({
    required String companyName,
    required String cityId,
    required double ipoRaiseTarget,
  }) async {
    calls.add('startAdditionalCompany');
    lastStartAdditionalCompanyArgs = {'companyName': companyName, 'cityId': cityId, 'ipoRaiseTarget': ipoRaiseTarget};
    if (startAdditionalCompanyError != null) throw startAdditionalCompanyError!;
    return startAdditionalCompanyResult ?? const NewCompanyResult(id: 'new-company-1', name: 'New Co');
  }

  @override
  Future<BuildingFinancialTimeline?> fetchBuildingFinancials(String buildingId) async {
    calls.add('fetchBuildingFinancials:$buildingId');
    if (buildingFinancialsError != null) throw buildingFinancialsError!;
    return financialsByBuildingId[buildingId];
  }

  @override
  Future<List<BuildingUnitOperationalStatus>> fetchBuildingUnitStatuses(String buildingId) async {
    calls.add('fetchBuildingUnitStatuses:$buildingId');
    if (buildingUnitStatusesError != null) throw buildingUnitStatusesError!;
    return unitStatusesByBuildingId[buildingId] ?? const [];
  }

  @override
  Future<CityPowerBalance?> fetchCityPowerBalance(String cityId) async {
    calls.add('fetchCityPowerBalance:$cityId');
    fetchCityPowerBalanceCallCount++;
    if (cityPowerBalanceError != null) throw cityPowerBalanceError!;
    return powerBalanceByCityId[cityId];
  }

  @override
  Future<void> removeDestroyedBuilding(String buildingId) async {
    calls.add('removeDestroyedBuilding');
    lastRemovedBuildingId = buildingId;
    if (removeDestroyedBuildingError != null) throw removeDestroyedBuildingError!;
  }
}
