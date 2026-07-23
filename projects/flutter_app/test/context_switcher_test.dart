import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/context/account_context_models.dart';
import 'package:capitalism_app/core/context/account_context_state.dart';
import 'package:capitalism_app/core/widgets/context_switcher.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_account_context_service.dart';
import 'support/in_memory_selected_city_storage.dart';
import 'support/in_memory_token_storage.dart';

const _cityA = ContextCityOption(id: 'city-a', name: 'Metropolis', countryCode: 'US');
const _cityB = ContextCityOption(id: 'city-b', name: 'Riverside', countryCode: 'US');
const _company = ContextCompanyOption(id: 'company-1', name: 'Acme Corp', cash: 5000, buildingCityIds: ['city-a']);

Future<AccountContextState> _pump(WidgetTester tester, {required FakeAccountContextService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 1600));
  addTearDown(() => tester.binding.setSurfaceSize(null));

  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  final accountContextState = AccountContextState(storage: InMemorySelectedCityStorage());

  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<AccountContextState>.value(value: accountContextState),
      ],
      child: MaterialApp(
        home: Scaffold(appBar: AppBar(title: ContextSwitcher(accountContextService: service))),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return accountContextState;
}

void main() {
  group('ContextSwitcher', () {
    testWidgets('shows the active account name once loaded', (tester) async {
      final service = FakeAccountContextService(
        activeAccount: const ActiveAccountInfo(
          playerId: 'player-1',
          displayName: 'Ada',
          availableCash: 0,
          activeAccountType: 'PERSON',
          activeCompanyId: null,
        ),
      );

      await _pump(tester, service: service);

      expect(find.text('Ada'), findsOneWidget);
    });

    testWidgets('shows the selected city alongside the account name', (tester) async {
      final service = FakeAccountContextService(companies: const [_company], cities: const [_cityA]);

      await _pump(tester, service: service);

      expect(find.textContaining('Metropolis'), findsOneWidget);
    });

    testWidgets('tapping the trigger opens the switcher sheet with cities and accounts', (tester) async {
      final service = FakeAccountContextService(companies: const [_company], cities: const [_cityA, _cityB]);

      await _pump(tester, service: service);
      await tester.tap(find.byKey(const Key('context-switcher-trigger')));
      await tester.pumpAndSettle();

      expect(find.text('Switch context'), findsOneWidget);
      expect(find.byKey(const Key('context-city-city-a')), findsOneWidget);
      expect(find.byKey(const Key('context-city-city-b')), findsOneWidget);
      expect(find.byKey(const Key('context-account-person')), findsOneWidget);
      expect(find.byKey(const Key('context-company-company-1')), findsOneWidget);
    });

    testWidgets('selecting a company switches the active account and closes the sheet', (tester) async {
      final service = FakeAccountContextService(companies: const [_company], cities: const [_cityA]);
      final state = await _pump(tester, service: service);

      await tester.tap(find.byKey(const Key('context-switcher-trigger')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('context-company-company-1')));
      await tester.pumpAndSettle();

      expect(service.lastSwitchedCompanyId, 'company-1');
      expect(state.isCompanyMode, isTrue);
      expect(find.text('Switch context'), findsNothing);
      expect(find.text('Acme Corp'), findsOneWidget);
    });

    testWidgets('selecting a city updates the trigger without a network switch call', (tester) async {
      final service = FakeAccountContextService(companies: const [_company], cities: const [_cityA, _cityB]);
      await _pump(tester, service: service);

      await tester.tap(find.byKey(const Key('context-switcher-trigger')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('context-city-city-b')));
      await tester.pumpAndSettle();

      expect(find.textContaining('Riverside'), findsOneWidget);
      expect(service.calls, isNot(contains('switchToCompany')));
      expect(service.calls, isNot(contains('switchToPerson')));
    });

    testWidgets('switching back to Person from a company works', (tester) async {
      final service = FakeAccountContextService(
        activeAccount: const ActiveAccountInfo(
          playerId: 'player-1',
          displayName: 'Ada',
          availableCash: 0,
          activeAccountType: 'COMPANY',
          activeCompanyId: 'company-1',
        ),
        companies: const [_company],
        cities: const [_cityA],
      );

      await _pump(tester, service: service);
      expect(find.text('Acme Corp'), findsOneWidget);

      await tester.tap(find.byKey(const Key('context-switcher-trigger')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('context-account-person')));
      await tester.pumpAndSettle();

      expect(service.switchedToPerson, isTrue);
      expect(find.text('Ada'), findsOneWidget);
    });
  });
}
