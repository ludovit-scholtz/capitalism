import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/economy/contracts_models.dart';
import 'package:capitalism_app/features/economy/contracts_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_contracts_service.dart';
import 'support/in_memory_token_storage.dart';

const _pending = SupplyContract(
  id: 'contract-1',
  sellerCompanyId: 'seller-1',
  sellerCompanyName: 'Acme Mining',
  buyerCompanyId: 'buyer-1',
  buyerCompanyName: 'Acme Foundry',
  resourceTypeName: 'Iron Ore',
  productTypeName: null,
  quantityPerTick: 100,
  pricePerUnit: 5,
  remainingTicks: 100,
  penaltyRatePercent: 10,
  currencyCode: 'EUR',
  status: 'PENDING',
  totalDeliveredQuantity: 0,
  totalUndeliveredQuantity: 0,
  totalPenaltyAmount: 0,
  penaltyCount: 0,
);

const _active = SupplyContract(
  id: 'contract-2',
  sellerCompanyId: 'seller-1',
  sellerCompanyName: 'Acme Mining',
  buyerCompanyId: 'buyer-2',
  buyerCompanyName: 'Acme Steelworks',
  resourceTypeName: 'Coal',
  productTypeName: null,
  quantityPerTick: 50,
  pricePerUnit: 3,
  remainingTicks: 40,
  penaltyRatePercent: 5,
  currencyCode: 'EUR',
  status: 'ACTIVE',
  totalDeliveredQuantity: 500,
  totalUndeliveredQuantity: 10,
  totalPenaltyAmount: 0,
  penaltyCount: 0,
);

const _history = SupplyContract(
  id: 'contract-3',
  sellerCompanyId: 'seller-1',
  sellerCompanyName: 'Acme Mining',
  buyerCompanyId: 'buyer-3',
  buyerCompanyName: 'Acme Alloys',
  resourceTypeName: null,
  productTypeName: 'Steel Beams',
  quantityPerTick: 20,
  pricePerUnit: 15,
  remainingTicks: 0,
  penaltyRatePercent: 5,
  currencyCode: 'EUR',
  status: 'CANCELLED',
  totalDeliveredQuantity: 200,
  totalUndeliveredQuantity: 0,
  totalPenaltyAmount: 12.5,
  penaltyCount: 1,
);

Future<void> _pumpContracts(WidgetTester tester, {required FakeContractsService service}) async {
  // The create-offer form is tall; a larger virtual screen keeps the
  // Pending/Active/History cards and action buttons mounted and hit-testable
  // (ListView only mounts/paints children within the viewport + cache
  // extent — see test/support/app_harness.dart for the same pattern).
  await tester.binding.setSurfaceSize(const Size(800, 3000));
  addTearDown(() => tester.binding.setSurfaceSize(null));

  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(
      value: auth,
      child: MaterialApp(home: Scaffold(body: ContractsScreen(contractsService: service))),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('ContractsScreen', () {
    testWidgets('shows pending, active, and history contracts in their columns', (tester) async {
      final service = FakeContractsService(contracts: [_pending, _active, _history]);

      await _pumpContracts(tester, service: service);

      expect(find.text('Iron Ore'), findsOneWidget);
      expect(find.text('Coal'), findsOneWidget);
      expect(find.text('Steel Beams'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Accept'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Reject'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Cancel'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeContractsService(fetchError: Exception('down'));

      await _pumpContracts(tester, service: service);

      expect(find.text('Could not load contracts. Please try again.'), findsOneWidget);
    });

    testWidgets('accepting a pending contract calls acceptContract and reloads', (tester) async {
      final service = FakeContractsService(contracts: [_pending]);

      await _pumpContracts(tester, service: service);
      await tester.tap(find.widgetWithText(FilledButton, 'Accept'));
      await tester.pumpAndSettle();

      expect(service.acceptedIds, ['contract-1']);
    });

    testWidgets('rejecting a pending contract calls rejectContract', (tester) async {
      final service = FakeContractsService(contracts: [_pending]);

      await _pumpContracts(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Reject'));
      await tester.pumpAndSettle();

      expect(service.rejectedIds, ['contract-1']);
    });

    testWidgets('cancelling an active contract calls cancelContract', (tester) async {
      final service = FakeContractsService(contracts: [_active]);

      await _pumpContracts(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(service.cancelledIds, ['contract-2']);
    });

    testWidgets('create-offer button is disabled until seller unit + exactly one item type are set', (tester) async {
      final service = FakeContractsService(
        myCompanies: const [ContractCompanyOption(id: 'seller-1', name: 'Acme Mining')],
        allCompanies: const [ContractCompanyOption(id: 'buyer-1', name: 'Acme Foundry')],
      );

      await _pumpContracts(tester, service: service);

      final createButtonFinder = find.widgetWithText(FilledButton, 'Create offer');
      expect(tester.widget<FilledButton>(createButtonFinder).onPressed, isNull);

      await tester.enterText(find.byKey(const ValueKey('seller-unit')), 'unit-uuid');
      await tester.enterText(find.byKey(const ValueKey('resource-type')), 'resource-uuid');
      await tester.tap(find.byKey(const ValueKey('buyer-company')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Acme Foundry').last);
      await tester.pumpAndSettle();

      expect(tester.widget<FilledButton>(createButtonFinder).onPressed, isNotNull);

      await tester.tap(createButtonFinder);
      await tester.pumpAndSettle();

      expect(service.lastProposeArgs?['sellerBuildingUnitId'], 'unit-uuid');
      expect(service.lastProposeArgs?['resourceTypeId'], 'resource-uuid');
    });
  });
}
