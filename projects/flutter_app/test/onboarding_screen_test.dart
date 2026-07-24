import 'dart:convert';

import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/auth/biatec_oidc_service.dart';
import 'package:capitalism_app/core/auth/web_authenticator.dart';
import 'package:capitalism_app/core/graphql/graphql_service.dart';
import 'package:capitalism_app/features/onboarding/onboarding_models.dart';
import 'package:capitalism_app/features/onboarding/onboarding_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import 'support/fake_id_token.dart';
import 'support/fake_onboarding_service.dart';
import 'support/fake_web_authenticator.dart';
import 'support/in_memory_token_storage.dart';

const _bratislava = OnboardingCity(
  id: 'city-1',
  name: 'Bratislava',
  countryCode: 'SK',
  currencyCode: 'EUR',
  latitude: 48.1486,
  longitude: 17.1077,
  population: 475000,
  resources: [],
);

const _furnitureProducts = [
  OnboardingProductType(
    id: 'p1',
    name: 'Wooden Chair',
    slug: 'wooden-chair',
    industry: 'FURNITURE',
    basePrice: 45,
    baseCraftTicks: 10,
    description: 'A simple wooden chair.',
    recipes: [],
  ),
  OnboardingProductType(
    id: 'p2',
    name: 'Wooden Table',
    slug: 'wooden-table',
    industry: 'FURNITURE',
    basePrice: 90,
    baseCraftTicks: 15,
    description: 'A simple wooden table.',
    recipes: [],
  ),
];

const _lots = [
  CityLot(
    id: 'lot-factory-1',
    cityId: 'city-1',
    name: 'Industrial Plot A',
    district: 'Industrial',
    latitude: 48.15,
    longitude: 17.11,
    populationIndex: 0.5,
    price: 5000,
    suitableTypes: ['FACTORY'],
    ownerCompanyId: null,
  ),
  CityLot(
    id: 'lot-shop-1',
    cityId: 'city-1',
    name: 'Commercial Plot B',
    district: 'Commercial',
    latitude: 48.16,
    longitude: 17.12,
    populationIndex: 0.8,
    price: 3000,
    suitableTypes: ['SALES_SHOP'],
    ownerCompanyId: null,
  ),
];

// The last id_token minted by `_successfulCallbackFor`, served back by
// `_oidcTokenClient()` to stand in for the real PKCE code-exchange response.
String? _lastMintedIdToken;

String _successfulCallbackFor(Uri authorizeUrl) {
  final state = authorizeUrl.queryParameters['state']!;
  final nonce = authorizeUrl.queryParameters['nonce']!;
  _lastMintedIdToken = buildFakeIdToken({
    'nonce': nonce,
    'iss': 'https://google.biatec.io',
    'aud': 'capitalism-pkce',
    'exp': DateTime.now().add(const Duration(hours: 1)).millisecondsSinceEpoch ~/ 1000,
  });
  return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&code=test-oidc-code';
}

http.Client _oidcTokenClient() => MockClient((request) async {
  return http.Response(
    jsonEncode({'idToken': _lastMintedIdToken}),
    200,
    headers: {'content-type': 'application/json'},
  );
});

Future<void> _pumpOnboarding(
  WidgetTester tester, {
  required AuthState auth,
  required FakeOnboardingService service,
  WebAuthenticator? webAuthenticator,
}) async {
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/',
        builder: (context, state) => Scaffold(
          body: OnboardingScreen(
            onboardingService: service,
            oidcService: BiatecOidcService(
              authenticator: webAuthenticator ?? FakeWebAuthenticator(_successfulCallbackFor),
              httpClient: _oidcTokenClient(),
            ),
          ),
        ),
      ),
      GoRoute(
        path: '/dashboard',
        builder: (context, state) => const Scaffold(body: Center(child: Text('Dashboard Reached'))),
      ),
    ],
  );

  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('OnboardingScreen — authenticated', () {
    testWidgets('happy path walks all steps and calls both mutations with the exact selected args', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeOnboardingService(
        cities: const [_bratislava],
        starterIndustries: const StarterIndustries(industries: ['FURNITURE'], proOnlyIndustries: []),
        productsByIndustry: const {'FURNITURE': _furnitureProducts},
        lotsByCity: const {'city-1': _lots},
      );

      await _pumpOnboarding(tester, auth: auth, service: service);

      await tester.tap(find.byKey(const Key('city-city-1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('industry-FURNITURE')));
      await tester.pumpAndSettle();
      expect(service.calls, contains('fetchProducts:FURNITURE'));

      await tester.tap(find.byKey(const Key('product-p1')));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Conservative'));
      await tester.pumpAndSettle();
      expect(service.calls, contains('fetchCityLots:city-1'));

      await tester.tap(find.byKey(const Key('lot-lot-factory-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Purchase Factory'));
      await tester.pumpAndSettle();

      expect(service.calls, contains('startOnboardingCompany:FURNITURE:city-1:lot-factory-1:200000.0'));
      expect(find.text('Purchase your sales shop lot'), findsOneWidget);

      await tester.tap(find.byKey(const Key('lot-lot-shop-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Purchase Shop'));
      await tester.pumpAndSettle();

      expect(service.calls, contains('finishOnboarding:p1:lot-shop-1'));
      expect(find.text('Your empire has launched!'), findsOneWidget);
      expect(find.text('Mark milestone complete'), findsOneWidget);
    });

    testWidgets('blocks selecting a Pro-only industry with a friendly error and does not load its products', (
      tester,
    ) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeOnboardingService(
        cities: const [_bratislava],
        starterIndustries: const StarterIndustries(industries: ['FURNITURE', 'ELECTRONICS'], proOnlyIndustries: ['ELECTRONICS']),
      );

      await _pumpOnboarding(tester, auth: auth, service: service);
      await tester.tap(find.byKey(const Key('city-city-1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('industry-ELECTRONICS')));
      await tester.pumpAndSettle();

      expect(find.text('This requires a Pro subscription.'), findsOneWidget);
      expect(find.byKey(const Key('industry-ELECTRONICS')), findsOneWidget); // still on step 2
      expect(service.calls, isNot(contains('fetchProducts:ELECTRONICS')));
    });

    testWidgets('LOT_ALREADY_OWNED on factory purchase clears the selection and reloads lots', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeOnboardingService(
        cities: const [_bratislava],
        starterIndustries: const StarterIndustries(industries: ['FURNITURE'], proOnlyIndustries: []),
        productsByIndustry: const {'FURNITURE': _furnitureProducts},
        lotsByCity: const {'city-1': _lots},
        startOnboardingCompanyError: GraphQlException('taken', 'LOT_ALREADY_OWNED'),
      );

      await _pumpOnboarding(tester, auth: auth, service: service);
      await tester.tap(find.byKey(const Key('city-city-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('industry-FURNITURE')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('product-p1')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Conservative'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('lot-lot-factory-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Purchase Factory'));
      await tester.pumpAndSettle();

      expect(find.text('That lot was just taken by someone else. Please choose another.'), findsOneWidget);
      expect(find.text('Purchase your factory lot'), findsOneWidget); // still on step 5
      final fetchCityLotsCalls = service.calls.where((c) => c.startsWith('fetchCityLots')).length;
      expect(fetchCityLotsCalls, greaterThanOrEqualTo(2)); // once for the IPO step, once again after the conflict
    });

    testWidgets('resumes directly into the shop step when onboardingCurrentStep is SHOP_SELECTION', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeOnboardingService(
        cities: const [_bratislava],
        starterIndustries: const StarterIndustries(industries: ['FURNITURE'], proOnlyIndustries: []),
        lotsByCity: const {'city-1': _lots},
        resumeState: const OnboardingResumeState(
          onboardingCompletedAtUtc: null,
          onboardingCurrentStep: 'SHOP_SELECTION',
          onboardingIndustry: 'FURNITURE',
          onboardingCityId: 'city-1',
          onboardingCompanyId: 'company-1',
          onboardingFactoryLotId: 'lot-factory-1',
          onboardingShopBuildingId: null,
          onboardingFirstSaleCompletedAtUtc: null,
        ),
      );

      await _pumpOnboarding(tester, auth: auth, service: service);

      expect(service.calls, contains('fetchResumeState'));
      expect(service.calls, contains('fetchCityLots:city-1'));
      expect(find.text('Purchase your sales shop lot'), findsOneWidget);
      expect(find.byKey(const Key('lot-lot-shop-1')), findsOneWidget);
    });

    testWidgets('redirects to /dashboard when onboarding and the first sale are both already complete', (
      tester,
    ) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeOnboardingService(
        resumeState: const OnboardingResumeState(
          onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
          onboardingCurrentStep: null,
          onboardingIndustry: null,
          onboardingCityId: null,
          onboardingCompanyId: null,
          onboardingFactoryLotId: null,
          onboardingShopBuildingId: 'shop-1',
          onboardingFirstSaleCompletedAtUtc: '2026-01-02T00:00:00Z',
        ),
      );

      await _pumpOnboarding(tester, auth: auth, service: service);

      expect(find.text('Dashboard Reached'), findsOneWidget);
    });

    testWidgets('marking the milestone complete calls the mutation and updates the UI', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeOnboardingService(
        resumeState: const OnboardingResumeState(
          onboardingCompletedAtUtc: '2026-01-01T00:00:00Z',
          onboardingCurrentStep: null,
          onboardingIndustry: null,
          onboardingCityId: null,
          onboardingCompanyId: null,
          onboardingFactoryLotId: null,
          onboardingShopBuildingId: 'shop-1',
          onboardingFirstSaleCompletedAtUtc: null,
        ),
      );

      await _pumpOnboarding(tester, auth: auth, service: service);

      expect(find.text('Mark milestone complete'), findsOneWidget);
      await tester.tap(find.text('Mark milestone complete'));
      await tester.pumpAndSettle();

      expect(service.calls, contains('completeFirstSaleMilestone'));
      expect(find.text('Go to Dashboard'), findsOneWidget);
      expect(find.text('Mark milestone complete'), findsNothing);
    });
  });

  group('OnboardingScreen — guest', () {
    testWidgets('makes no purchase mutation calls until Save Progress, which migrates via both mutations', (
      tester,
    ) async {
      final auth = AuthState(storage: InMemoryTokenStorage()); // never authenticated
      final service = FakeOnboardingService(
        cities: const [_bratislava],
        starterIndustries: const StarterIndustries(industries: ['FURNITURE'], proOnlyIndustries: []),
        productsByIndustry: const {'FURNITURE': _furnitureProducts},
        lotsByCity: const {'city-1': _lots},
      );
      final authenticator = FakeWebAuthenticator(_successfulCallbackFor);

      await _pumpOnboarding(tester, auth: auth, service: service, webAuthenticator: authenticator);

      expect(service.calls, isNot(contains('fetchResumeState'))); // guests have no resumable server state

      await tester.tap(find.byKey(const Key('city-city-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('industry-FURNITURE')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('product-p1')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Conservative'));
      await tester.pumpAndSettle();

      expect(service.calls.any((c) => c.startsWith('startOnboardingCompany')), isFalse);

      await tester.tap(find.byKey(const Key('lot-lot-factory-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Purchase Factory'));
      await tester.pumpAndSettle();

      // The guest "purchase" is simulated locally — still no mutation call.
      expect(service.calls.any((c) => c.startsWith('startOnboardingCompany')), isFalse);
      expect(find.text('Purchase your sales shop lot'), findsOneWidget);

      await tester.tap(find.byKey(const Key('lot-lot-shop-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Purchase Shop'));
      await tester.pumpAndSettle();

      expect(service.calls.any((c) => c.startsWith('finishOnboarding')), isFalse);
      expect(find.text('Save Progress'), findsOneWidget);

      await tester.tap(find.text('Save Progress'));
      await tester.pumpAndSettle();

      expect(service.calls, contains('startOnboardingCompany:FURNITURE:city-1:lot-factory-1:200000.0'));
      expect(service.calls, contains('finishOnboarding:p1:lot-shop-1'));
      expect(auth.isAuthenticated, isTrue);
    });
  });
}
