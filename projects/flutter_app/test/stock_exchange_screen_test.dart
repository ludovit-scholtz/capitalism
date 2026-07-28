import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/exchange/stock_exchange_screen.dart';
import 'package:capitalism_app/features/exchange/stock_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_stock_service.dart';
import 'support/in_memory_token_storage.dart';

const _listing = StockListing(
  companyId: 'company-1',
  stockSymbol: 'ACME',
  companyName: 'Acme Corp',
  primaryCityName: 'Metropolis',
  primaryIndustry: 'MANUFACTURING',
  sharePrice: 42.5,
  dailyChangePercent: 3.2,
  marketValue: 1000000,
  playerOwnedShares: 10,
  bidPrice: 42.0,
  askPrice: 43.0,
);

const _controllableListing = StockListing(
  companyId: 'company-2',
  stockSymbol: 'RIVL',
  companyName: 'Rival Corp',
  primaryCityName: 'Metropolis',
  primaryIndustry: 'MANUFACTURING',
  sharePrice: 12,
  dailyChangePercent: -1.5,
  marketValue: 500000,
  playerOwnedShares: 6000,
  canProposeDividend: true,
  canClaimControl: true,
  canMerge: true,
);

Future<GoRouter> _pumpStockExchange(WidgetTester tester, {required FakeStockService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: StockExchangeScreen(stockService: service))),
      GoRoute(
        path: '/stock/trade/:companyId',
        builder: (context, state) => Scaffold(body: Text('Trade ${state.pathParameters['companyId']}')),
      ),
    ],
  );
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
  return router;
}

void main() {
  group('StockExchangeScreen', () {
    testWidgets('shows stock listings with price and change', (tester) async {
      final service = FakeStockService(listings: [_listing]);

      await _pumpStockExchange(tester, service: service);

      expect(find.text('Acme Corp (ACME)'), findsOneWidget);
      expect(find.text('+3.2%'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeStockService(listingsError: Exception('down'));

      await _pumpStockExchange(tester, service: service);

      expect(find.text('Could not load the stock exchange. Please try again.'), findsOneWidget);
    });

    testWidgets('tapping a listing navigates to stock trading', (tester) async {
      final service = FakeStockService(listings: [_listing]);

      await _pumpStockExchange(tester, service: service);
      await tester.tap(find.text('Acme Corp (ACME)'));
      await tester.pumpAndSettle();

      expect(find.text('Trade company-1'), findsOneWidget);
    });

    testWidgets('does not show governance actions for listings without control rights', (tester) async {
      final service = FakeStockService(listings: [_listing]);

      await _pumpStockExchange(tester, service: service);

      expect(find.text('Dividends'), findsNothing);
      expect(find.text('Claim control'), findsNothing);
      expect(find.text('Merge'), findsNothing);
    });

    testWidgets('shows governance actions and opens the dividends dialog with vote/propose', (tester) async {
      final service = FakeStockService(
        listings: [_controllableListing],
        dividendProposals: const [
          DividendProposal(
            id: 'proposal-1',
            stockSymbol: 'RIVL',
            dividendPerShare: 0.25,
            totalPayout: 1000,
            status: 'VOTING',
            ticksRemaining: 5,
            forVotes: 100,
            againstVotes: 20,
            myVoteChoice: null,
          ),
        ],
      );

      await _pumpStockExchange(tester, service: service);
      expect(find.text('Claim control'), findsOneWidget);
      expect(find.text('Merge'), findsOneWidget);

      await tester.tap(find.text('Dividends'));
      await tester.pumpAndSettle();

      expect(find.text('0.25/share · VOTING'), findsOneWidget);
      await tester.tap(find.text('Vote For'));
      await tester.pumpAndSettle();
      expect(service.lastVoteArgs, {'proposalId': 'proposal-1', 'choice': 'FOR'});

      await tester.enterText(find.widgetWithText(TextField, 'Dividend per share'), '1.0');
      await tester.tap(find.text('Propose'));
      await tester.pumpAndSettle();
      expect(service.lastProposeDividendArgs, {'stockSymbol': 'RIVL', 'dividendPerShare': 1.0});
    });

    testWidgets('claiming control confirms and calls replaceCeo with the current player id', (tester) async {
      final service = FakeStockService(
        listings: [_controllableListing],
        personAccountStockSummary: const PersonAccountStockSummary(
          playerId: 'player-42',
          availableCash: 0,
          shareholdings: [],
          stockTrades: [],
        ),
      );

      await _pumpStockExchange(tester, service: service);
      await tester.tap(find.text('Claim control'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Claim control'));
      await tester.pumpAndSettle();

      expect(service.lastReplaceCeoArgs, {'companyId': 'company-2', 'newCeoPlayerId': 'player-42'});
      expect(find.text('You are now the CEO of Rival Corp.'), findsOneWidget);
    });

    testWidgets('inline trade panel is collapsed by default and expands on Trade', (tester) async {
      final service = FakeStockService(listings: [_listing]);

      await _pumpStockExchange(tester, service: service);
      expect(find.byKey(const Key('trade-buy-company-1')), findsNothing);

      await tester.tap(find.byKey(const Key('trade-toggle-company-1')));
      await tester.pumpAndSettle();

      expect(find.text('Ask: 43.00'), findsOneWidget);
      expect(find.text('Bid: 42.00'), findsOneWidget);
      expect(find.byKey(const Key('trade-buy-company-1')), findsOneWidget);
      expect(find.byKey(const Key('trade-sell-company-1')), findsOneWidget);
    });

    testWidgets('buying inline calls buyShares with the entered quantity and selected account, without navigating away', (tester) async {
      final service = FakeStockService(
        listings: [_listing],
        bankAccounts: const [
          {'id': 'account-1', 'currencyCode': 'USD'},
        ],
      );

      await _pumpStockExchange(tester, service: service);
      await tester.tap(find.byKey(const Key('trade-toggle-company-1')));
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('trade-quantity-company-1')), '5');
      await tester.tap(find.byKey(const Key('trade-buy-company-1')));
      await tester.pumpAndSettle();

      expect(service.lastBuyArgs?['companyId'], 'company-1');
      expect(service.lastBuyArgs?['shareCount'], 5.0);
      expect(service.lastBuyArgs?['bankAccountId'], 'account-1');
      expect(find.text('Bought 5 shares of ACME.'), findsOneWidget);
      // Still on the exchange screen — no navigation to the trade desk.
      expect(find.text('Acme Corp (ACME)'), findsOneWidget);
    });

    testWidgets('selling inline calls sellShares and shows a failure message on error', (tester) async {
      final service = FakeStockService(listings: [_listing], tradeError: Exception('down'));

      await _pumpStockExchange(tester, service: service);
      await tester.tap(find.byKey(const Key('trade-toggle-company-1')));
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('trade-quantity-company-1')), '2');
      await tester.tap(find.byKey(const Key('trade-sell-company-1')));
      await tester.pumpAndSettle();

      expect(service.calls, contains('sellShares'));
      expect(find.text('Could not complete this trade.'), findsOneWidget);
    });

    testWidgets('merging picks a destination company and calls mergeCompany', (tester) async {
      final service = FakeStockService(
        listings: [_controllableListing],
        myCompanies: const [
          {'id': 'my-company-1', 'name': 'My Holding Co'},
        ],
      );

      await _pumpStockExchange(tester, service: service);
      await tester.tap(find.text('Merge'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Merge'));
      await tester.pumpAndSettle();

      expect(service.lastMergeArgs, {'targetCompanyId': 'company-2', 'destinationCompanyId': 'my-company-1'});
    });
  });
}
