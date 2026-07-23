import '../graphql/graphql_service.dart';
import 'account_context_models.dart';

const _activeAccountQuery = r'''
  query ActiveAccountContext {
    personAccount { playerId displayName availableCash activeAccountType activeCompanyId }
  }
''';

const _myCompaniesWithCitiesQuery = r'''
  query ContextSwitcherCompanies { myCompanies { id name cash buildings { cityId } } }
''';

const _citiesQuery = r'''
  query ContextSwitcherCities { cities { id name countryCode } }
''';

const _switchAccountContextMutation = r'''
  mutation SwitchAccountContext($input: SwitchAccountContextInput!) {
    switchAccountContext(input: $input) { activeAccountType activeCompanyId activeAccountName }
  }
''';

/// GraphQL calls backing the persistent context switcher, matching
/// `stores/auth.ts`'s `switchAccountContext`/`fetchMe` and
/// `ContextSwitcherPanel.vue`'s data needs.
class AccountContextService {
  const AccountContextService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<ActiveAccountInfo> fetchActiveAccount() async {
    final result = await _graphQlService.request(_activeAccountQuery);
    return ActiveAccountInfo.fromJson(result['personAccount'] as Map<String, dynamic>);
  }

  Future<List<ContextCompanyOption>> fetchMyCompanies() async {
    final result = await _graphQlService.request(_myCompaniesWithCitiesQuery);
    final list = result['myCompanies'] as List<dynamic>? ?? const [];
    return list.map((e) => ContextCompanyOption.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<ContextCityOption>> fetchCities() async {
    final result = await _graphQlService.request(_citiesQuery);
    final list = result['cities'] as List<dynamic>? ?? const [];
    return list.map((e) => ContextCityOption.fromJson(e as Map<String, dynamic>)).toList();
  }

  /// Switches to the player's personal account.
  Future<void> switchToPerson() {
    return _graphQlService.request(
      _switchAccountContextMutation,
      variables: {
        'input': {'accountType': 'PERSON', 'companyId': null},
      },
    );
  }

  /// Switches to acting as [companyId]. The backend auto-claims CEO control
  /// if the caller has majority ownership but isn't yet the CEO on record
  /// (`ClaimCompanyControlIfEligibleAsync`), matching `replaceCEO`'s takeover path.
  Future<void> switchToCompany(String companyId) {
    return _graphQlService.request(
      _switchAccountContextMutation,
      variables: {
        'input': {'accountType': 'COMPANY', 'companyId': companyId},
      },
    );
  }
}
