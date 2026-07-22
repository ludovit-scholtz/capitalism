import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/company/company_models.dart';
import 'package:capitalism_app/features/company/company_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_company_service.dart';
import 'support/in_memory_token_storage.dart';

const _ledger = CompanyLedger(
  companyName: 'Acme Corp',
  gameYear: 2026,
  currentCash: 50000,
  primaryCurrencyCode: 'EUR',
  totalRevenue: 100000,
  totalPurchasingCosts: 20000,
  totalShippingCosts: 5000,
  totalLaborCosts: 15000,
  totalEnergyCosts: 3000,
  totalMarketingCosts: 2000,
  totalTaxPaid: 10000,
  totalOtherCosts: 1000,
  netIncome: 44000,
  totalAssets: 200000,
);

const _cityBreakdown = CityFinancialBreakdown(cityName: 'Metropolis', currencyCode: 'EUR', revenue: 80000, costs: 40000, profit: 40000);

const _awardedContract = CompanyContractCard(
  id: 'contract-1',
  title: 'Steel Supply',
  productName: 'Steel Beams',
  quantityRequired: 100,
  status: 'AWARDED',
  fulfilledQuantity: 20,
  fulfillmentPercent: 20,
);

const _bid = ContractBid(id: 'bid-1', contractId: 'contract-2', bidPricePerUnit: 5, contractStatus: 'BIDDING');

const _settings = CompanySettings(
  companyName: 'Acme Corp',
  dividendPayoutRatio: 0.2,
  administrationOverheadRate: 0.05,
  ageFactor: 1.1,
  assetFactor: 1.2,
  citySalarySettings: [
    CitySalarySetting(cityId: 'city-1', cityName: 'Metropolis', currencyCode: 'EUR', baseSalaryPerManhour: 10, salaryMultiplier: 1.0),
  ],
  pendingDividendProposal: PendingDividendProposal(id: 'proposal-1', dividendPercent: 15, ticksRemaining: 10, forVotes: 3, againstVotes: 1, myVoteChoice: null),
);

const _brandOverview = BrandQualityOverview(
  totalResearchBudgetUsd: 5000,
  brands: [CompanyBrand(id: 'brand-1', name: 'SuperBrand', productName: 'Steel Beams', quality: 0.8, marketingQuality: 0.7, combinedBrandQuality: 0.75, accumulatedResearchBudget: 2000)],
);

Future<void> _pump(WidgetTester tester, Widget widget) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp(home: Scaffold(body: widget))),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('LedgerScreen', () {
    testWidgets('shows P&L summary and city breakdown', (tester) async {
      final service = FakeCompanyService(ledger: _ledger, cityBreakdown: const [_cityBreakdown]);

      await _pump(tester, LedgerScreen(companyId: 'company-1', companyService: service));

      expect(find.text('Acme Corp Ledger'), findsOneWidget);
      expect(find.text('Metropolis'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeCompanyService(ledgerError: Exception('down'));

      await _pump(tester, LedgerScreen(companyId: 'company-1', companyService: service));

      expect(find.text('Could not load the ledger. Please try again.'), findsOneWidget);
    });
  });

  group('CompanyContractsScreen', () {
    testWidgets('shows awarded contract with shipping form and bid history', (tester) async {
      final service = FakeCompanyService(contracts: const [_awardedContract], bids: const [_bid]);

      await _pump(tester, CompanyContractsScreen(companyId: 'company-1', companyService: service));

      expect(find.text('Steel Supply'), findsOneWidget);
      await tester.enterText(find.byType(TextField), '10');
      await tester.tap(find.widgetWithText(FilledButton, 'Ship'));
      await tester.pumpAndSettle();

      expect(service.lastFulfillArgs?['contractId'], 'contract-1');
      expect(service.lastFulfillArgs?['quantity'], 10.0);
    });
  });

  group('CompanySettingsScreen', () {
    testWidgets('loads settings and saves changes', (tester) async {
      final service = FakeCompanyService(settings: _settings);

      await _pump(tester, CompanySettingsScreen(companyId: 'company-1', companyService: service));

      expect(find.widgetWithText(TextField, 'Company name'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Save changes'));
      await tester.pumpAndSettle();

      expect(service.lastUpdateSettingsArgs?['name'], 'Acme Corp');
    });

    testWidgets('voting on a dividend proposal calls voteDividend', (tester) async {
      final service = FakeCompanyService(settings: _settings);

      await _pump(tester, CompanySettingsScreen(companyId: 'company-1', companyService: service));
      await tester.tap(find.widgetWithText(FilledButton, 'Approve'));
      await tester.pumpAndSettle();

      expect(service.lastVoteApprove, isTrue);
    });
  });

  group('CompanyResearchScreen', () {
    testWidgets('shows brand quality overview', (tester) async {
      final service = FakeCompanyService(brandOverview: _brandOverview);

      await _pump(tester, CompanyResearchScreen(companyId: 'company-1', companyService: service));

      expect(find.text('SuperBrand'), findsOneWidget);
      expect(find.text('75%'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeCompanyService(researchError: Exception('down'));

      await _pump(tester, CompanyResearchScreen(companyId: 'company-1', companyService: service));

      expect(find.text('Could not load research data. Please try again.'), findsOneWidget);
    });
  });
}
