import 'package:capitalism_app/core/context/account_context_models.dart';
import 'package:capitalism_app/core/context/account_context_state.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_account_context_service.dart';
import 'support/in_memory_selected_city_storage.dart';

const _cityA = ContextCityOption(id: 'city-a', name: 'Metropolis', countryCode: 'US');
const _cityB = ContextCityOption(id: 'city-b', name: 'Riverside', countryCode: 'US');

const _companyOne = ContextCompanyOption(id: 'company-1', name: 'Acme Corp', cash: 5000, buildingCityIds: ['city-a', 'city-a', 'city-b']);
const _companyTwo = ContextCompanyOption(id: 'company-2', name: 'Beta LLC', cash: 2000, buildingCityIds: ['city-b']);

void main() {
  group('AccountContextState', () {
    test('restoreSelectedCity loads the persisted city without a network call', () async {
      final storage = InMemorySelectedCityStorage();
      await storage.write('city-a');
      final state = AccountContextState(storage: storage);

      await state.restoreSelectedCity();

      expect(state.selectedCityId, 'city-a');
    });

    test('refresh populates active account, companies, and cities', () async {
      final state = AccountContextState(storage: InMemorySelectedCityStorage());
      final service = FakeAccountContextService(
        companies: const [_companyOne, _companyTwo],
        cities: const [_cityA, _cityB],
      );

      await state.refresh(service);

      expect(state.activeAccount, isNotNull);
      expect(state.companies, [_companyOne, _companyTwo]);
      expect(state.cities, [_cityA, _cityB]);
      expect(state.loading, isFalse);
      expect(state.error, isNull);
    });

    test('refresh auto-selects the city with the most buildings when none is selected', () async {
      final state = AccountContextState(storage: InMemorySelectedCityStorage());
      final service = FakeAccountContextService(companies: const [_companyOne], cities: const [_cityA, _cityB]);

      await state.refresh(service);

      // company-1 has 2 buildings in city-a and 1 in city-b, so city-a wins.
      expect(state.selectedCityId, 'city-a');
    });

    test('refresh keeps a manually-selected city that is still relevant', () async {
      final storage = InMemorySelectedCityStorage();
      await storage.write('city-b');
      final state = AccountContextState(storage: storage);
      await state.restoreSelectedCity();
      final service = FakeAccountContextService(companies: const [_companyOne], cities: const [_cityA, _cityB]);

      await state.refresh(service);

      expect(state.selectedCityId, 'city-b');
    });

    test('switching to a company re-runs the auto-select using only that company\'s buildings', () async {
      final storage = InMemorySelectedCityStorage();
      await storage.write('city-a');
      final state = AccountContextState(storage: storage);
      await state.restoreSelectedCity();
      final service = FakeAccountContextService(
        activeAccount: const ActiveAccountInfo(
          playerId: 'player-1',
          displayName: 'Player',
          availableCash: 0,
          activeAccountType: 'PERSON',
          activeCompanyId: null,
        ),
        companies: const [_companyOne, _companyTwo],
        cities: const [_cityA, _cityB],
      );
      await state.refresh(service);
      expect(state.selectedCityId, 'city-a');

      final ok = await state.switchToCompany(service, 'company-2');

      // company-2 only has buildings in city-b, so switching to it should
      // pull the selected city along — the same "shouldAutoSwitchCity"
      // behavior as the web when the stored city has zero relevance.
      expect(ok, isTrue);
      expect(state.isCompanyMode, isTrue);
      expect(state.activeCompanyId, 'company-2');
      expect(state.selectedCityId, 'city-b');
    });

    test('switchToPerson updates the active account and returns true on success', () async {
      final state = AccountContextState(storage: InMemorySelectedCityStorage());
      final service = FakeAccountContextService(
        activeAccount: const ActiveAccountInfo(
          playerId: 'player-1',
          displayName: 'Player',
          availableCash: 0,
          activeAccountType: 'COMPANY',
          activeCompanyId: 'company-1',
        ),
        companies: const [_companyOne],
        cities: const [_cityA],
      );
      await state.refresh(service);
      expect(state.isCompanyMode, isTrue);

      final ok = await state.switchToPerson(service);

      expect(ok, isTrue);
      expect(state.isCompanyMode, isFalse);
      expect(service.switchedToPerson, isTrue);
    });

    test('switchToCompany returns false and preserves prior state on failure', () async {
      final state = AccountContextState(storage: InMemorySelectedCityStorage());
      final service = FakeAccountContextService(companies: const [_companyOne], cities: const [_cityA], error: Exception('down'));

      final ok = await state.switchToCompany(service, 'company-1');

      expect(ok, isFalse);
    });

    test('selectCity persists the choice', () async {
      final storage = InMemorySelectedCityStorage();
      final state = AccountContextState(storage: storage);

      await state.selectCity('city-b');

      expect(state.selectedCityId, 'city-b');
      expect(await storage.read(), 'city-b');
    });

    test('refresh sets an error message on failure without throwing', () async {
      final state = AccountContextState(storage: InMemorySelectedCityStorage());
      final service = FakeAccountContextService(error: Exception('down'));

      await state.refresh(service);

      expect(state.error, isNotNull);
      expect(state.loading, isFalse);
    });

    test('refresh still populates cities and companies when only the active-account fetch fails', () async {
      // Regression test: a single Future.wait used to discard successful
      // city/company results whenever the account fetch failed, blanking
      // out the whole switcher instead of degrading gracefully.
      final state = AccountContextState(storage: InMemorySelectedCityStorage());
      final service = FakeAccountContextService(
        companies: const [_companyOne, _companyTwo],
        cities: const [_cityA, _cityB],
        activeAccountError: Exception('account query timed out'),
      );

      await state.refresh(service);

      expect(state.companies, [_companyOne, _companyTwo]);
      expect(state.cities, [_cityA, _cityB]);
      expect(state.error, isNotNull);
      expect(state.loading, isFalse);
    });

    test('clearAccount resets server-derived state but keeps the selected city', () async {
      final storage = InMemorySelectedCityStorage();
      await storage.write('city-a');
      final state = AccountContextState(storage: storage);
      await state.restoreSelectedCity();
      final service = FakeAccountContextService(companies: const [_companyOne], cities: const [_cityA]);
      await state.refresh(service);

      state.clearAccount();

      expect(state.activeAccount, isNull);
      expect(state.companies, isEmpty);
      expect(state.selectedCityId, 'city-a');
    });
  });
}
