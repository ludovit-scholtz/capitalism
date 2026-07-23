import 'package:capitalism_app/core/context/account_context_models.dart';
import 'package:capitalism_app/core/context/account_context_service.dart';

class FakeAccountContextService implements AccountContextService {
  FakeAccountContextService({
    ActiveAccountInfo activeAccount = const ActiveAccountInfo(
      playerId: 'player-1',
      displayName: 'Test Player',
      availableCash: 0,
      activeAccountType: 'PERSON',
      activeCompanyId: null,
    ),
    this.companies = const [],
    this.cities = const [],
    this.error,
  }) : _activeAccount = activeAccount;

  ActiveAccountInfo _activeAccount;
  final List<ContextCompanyOption> companies;
  final List<ContextCityOption> cities;
  final Object? error;

  final List<String> calls = [];
  String? lastSwitchedCompanyId;
  bool switchedToPerson = false;

  @override
  Future<ActiveAccountInfo> fetchActiveAccount() async {
    calls.add('fetchActiveAccount');
    if (error != null) throw error!;
    return _activeAccount;
  }

  @override
  Future<List<ContextCompanyOption>> fetchMyCompanies() async {
    calls.add('fetchMyCompanies');
    if (error != null) throw error!;
    return companies;
  }

  @override
  Future<List<ContextCityOption>> fetchCities() async {
    calls.add('fetchCities');
    if (error != null) throw error!;
    return cities;
  }

  @override
  Future<void> switchToPerson() async {
    calls.add('switchToPerson');
    if (error != null) throw error!;
    switchedToPerson = true;
    _activeAccount = ActiveAccountInfo(
      playerId: _activeAccount.playerId,
      displayName: _activeAccount.displayName,
      availableCash: _activeAccount.availableCash,
      activeAccountType: 'PERSON',
      activeCompanyId: null,
    );
  }

  @override
  Future<void> switchToCompany(String companyId) async {
    calls.add('switchToCompany');
    if (error != null) throw error!;
    lastSwitchedCompanyId = companyId;
    _activeAccount = ActiveAccountInfo(
      playerId: _activeAccount.playerId,
      displayName: _activeAccount.displayName,
      availableCash: _activeAccount.availableCash,
      activeAccountType: 'COMPANY',
      activeCompanyId: companyId,
    );
  }
}
