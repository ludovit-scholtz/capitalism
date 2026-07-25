import 'package:capitalism_app/features/dashboard/dashboard_models.dart';
import 'package:capitalism_app/features/dashboard/dashboard_service.dart';

/// Full in-memory fake of [DashboardService] (`implements`, not an HTTP
/// mock — see `test/support/fake_onboarding_service.dart` for why this
/// pattern is preferred once a service has more than 2-3 operations).
class FakeDashboardService implements DashboardService {
  FakeDashboardService({
    this.onboardingCompleted = true,
    this.data = const DashboardData(companies: [], currentTick: 100, taxRate: 15, pendingActions: []),
    this.fetchDataError,
    this.removeDestroyedBuildingError,
  });

  final bool? onboardingCompleted;
  final DashboardData data;
  final Object? fetchDataError;
  final Object? removeDestroyedBuildingError;

  final List<String> calls = [];
  int fetchDashboardDataCallCount = 0;
  String? lastRemovedBuildingId;

  @override
  Future<bool?> fetchOnboardingCompleted() async {
    calls.add('fetchOnboardingCompleted');
    return onboardingCompleted;
  }

  @override
  Future<DashboardData> fetchDashboardData() async {
    calls.add('fetchDashboardData');
    fetchDashboardDataCallCount++;
    if (fetchDataError != null) throw fetchDataError!;
    return data;
  }

  @override
  Future<void> removeDestroyedBuilding(String buildingId) async {
    calls.add('removeDestroyedBuilding');
    lastRemovedBuildingId = buildingId;
    if (removeDestroyedBuildingError != null) throw removeDestroyedBuildingError!;
  }
}
