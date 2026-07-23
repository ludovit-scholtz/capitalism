import 'package:flutter/foundation.dart';

import 'account_context_models.dart';
import 'account_context_service.dart';
import 'selected_city_storage.dart';

/// Holds the player's current "acting as" context — person vs. a specific
/// company, plus a persisted "current city" — for the app-wide
/// [ContextSwitcher] control. Mirrors `stores/auth.ts`'s
/// `activeAccountType`/`activeCompanyId`/`selectedCityId` plus
/// `lib/cityContext.ts`'s auto-select heuristic, but as a state container:
/// it does not own a [AccountContextService] itself (screens/widgets build
/// one from their own `AuthState`, same convention as every other feature
/// in this app), instead taking the service as a parameter on each call.
class AccountContextState extends ChangeNotifier {
  AccountContextState({SelectedCityStorage? storage}) : _storage = storage ?? SharedPreferencesSelectedCityStorage();

  final SelectedCityStorage _storage;

  bool loading = false;
  String? error;
  ActiveAccountInfo? activeAccount;
  List<ContextCompanyOption> companies = const [];
  List<ContextCityOption> cities = const [];
  String? selectedCityId;
  bool _citySelectionRestored = false;

  String? get activeCompanyId => activeAccount?.activeCompanyId;
  bool get isCompanyMode => activeAccount?.activeAccountType == 'COMPANY';

  /// The active company's record, or `null` in person mode / before load.
  ContextCompanyOption? get activeCompany {
    final companyId = activeCompanyId;
    if (companyId == null) return null;
    for (final company in companies) {
      if (company.id == companyId) return company;
    }
    return null;
  }

  String get displayName {
    if (isCompanyMode) return activeCompany?.name ?? activeAccount?.displayName ?? 'Company';
    return activeAccount?.displayName ?? 'Person';
  }

  String? get selectedCityName {
    final cityId = selectedCityId;
    if (cityId == null) return null;
    for (final city in cities) {
      if (city.id == cityId) return city.name;
    }
    return null;
  }

  /// Loads the persisted city choice — cheap and offline, safe to call
  /// before the player is authenticated (mirrors reading `localStorage` on
  /// the web before `fetchMe()` resolves).
  Future<void> restoreSelectedCity() async {
    if (_citySelectionRestored) return;
    _citySelectionRestored = true;
    selectedCityId = await _storage.read();
    notifyListeners();
  }

  /// Refetches the active account, owned companies, and city list, then
  /// re-runs the city auto-select heuristic if the stored city no longer
  /// makes sense for the (possibly just-switched) active context.
  Future<void> refresh(AccountContextService service) async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      final results = await Future.wait([service.fetchActiveAccount(), service.fetchMyCompanies(), service.fetchCities()]);
      activeAccount = results[0] as ActiveAccountInfo;
      companies = results[1] as List<ContextCompanyOption>;
      cities = results[2] as List<ContextCityOption>;
      await _autoSelectCityIfNeeded();
      loading = false;
    } catch (_) {
      error = 'Could not load your account context.';
      loading = false;
    }
    notifyListeners();
  }

  /// Mirrors `shouldAutoSwitchCity`/`selectMainCity`: picks the city with
  /// the most buildings for the currently-active context (the active
  /// company, or the union of all owned companies in person mode) whenever
  /// no city is selected yet, or the selected one has zero relevance to the
  /// context that's now active (e.g. right after switching companies).
  Future<void> _autoSelectCityIfNeeded() async {
    if (cities.isEmpty) return;

    final relevantCompanies = isCompanyMode ? companies.where((c) => c.id == activeCompanyId) : companies;
    final cityCounts = <String, int>{};
    for (final company in relevantCompanies) {
      for (final cityId in company.buildingCityIds) {
        cityCounts[cityId] = (cityCounts[cityId] ?? 0) + 1;
      }
    }

    final currentIsRelevant = selectedCityId != null && (cityCounts[selectedCityId] ?? 0) > 0;
    if (currentIsRelevant) return;

    if (cityCounts.isEmpty) {
      // No buildings anywhere yet — leave any existing manual selection
      // alone, or fall back to the first known city so the switcher never
      // shows a blank city.
      selectedCityId ??= cities.first.id;
      return;
    }

    final mainCityId = cityCounts.entries.reduce((a, b) => b.value > a.value ? b : a).key;
    selectedCityId = mainCityId;
    await _storage.write(mainCityId);
  }

  Future<void> selectCity(String cityId) async {
    selectedCityId = cityId;
    await _storage.write(cityId);
    notifyListeners();
  }

  Future<bool> switchToPerson(AccountContextService service) async {
    try {
      await service.switchToPerson();
      await refresh(service);
      return true;
    } catch (_) {
      return false;
    }
  }

  Future<bool> switchToCompany(AccountContextService service, String companyId) async {
    try {
      await service.switchToCompany(companyId);
      await refresh(service);
      return true;
    } catch (_) {
      return false;
    }
  }

  /// Called on logout — clears server-derived state but keeps the persisted
  /// city choice (a device/browser-level preference, not account-specific,
  /// matching the web's `localStorage` city persisting across sign-outs).
  void clearAccount() {
    activeAccount = null;
    companies = const [];
    error = null;
    notifyListeners();
  }
}
