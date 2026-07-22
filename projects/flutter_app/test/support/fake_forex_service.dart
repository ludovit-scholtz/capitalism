import 'package:capitalism_app/features/exchange/forex_models.dart';
import 'package:capitalism_app/features/exchange/forex_service.dart';

class FakeForexService implements ForexService {
  FakeForexService({
    this.rates = const [],
    this.balances = const [],
    this.history = const [],
    this.quote,
    this.rateHistory = const [],
    this.activeMarketEvents = const [],
    this.bankAccounts = const [],
    this.goldPools = const [],
    this.goldBalance = const GoldBalance(balance: 0, blockedInPools: 0, availableBalance: 0),
    this.goldSwapQuote,
    this.loadError,
    this.swapError,
    this.transferError,
    this.goldError,
  });

  final List<FxRate> rates;
  final List<CurrencyBalance> balances;
  final List<ForexTrade> history;
  final ForexQuote? quote;
  final List<FxRateHistoryPoint> rateHistory;
  final List<MarketEvent> activeMarketEvents;
  final List<BankAccountOption> bankAccounts;
  final List<GoldAmmPool> goldPools;
  final GoldBalance goldBalance;
  final GoldAmmSwapQuote? goldSwapQuote;
  final Object? loadError;
  final Object? swapError;
  final Object? transferError;
  final Object? goldError;

  final List<String> calls = [];
  Map<String, dynamic>? lastSwapArgs;
  Map<String, dynamic>? lastTransferArgs;
  Map<String, dynamic>? lastGoldSwapArgs;
  Map<String, dynamic>? lastAddLiquidityArgs;
  Map<String, dynamic>? lastCreatePoolArgs;
  Map<String, dynamic>? lastRemoveLiquidityArgs;

  @override
  Future<List<FxRate>> fetchRates() async {
    calls.add('fetchRates');
    if (loadError != null) throw loadError!;
    return rates;
  }

  @override
  Future<List<CurrencyBalance>> fetchBalances() async {
    calls.add('fetchBalances');
    if (loadError != null) throw loadError!;
    return balances;
  }

  @override
  Future<List<ForexTrade>> fetchHistory() async {
    calls.add('fetchHistory');
    if (loadError != null) throw loadError!;
    return history;
  }

  @override
  Future<ForexQuote> fetchQuote({required String fromCurrencyCode, required String toCurrencyCode, required double amount}) async {
    calls.add('fetchQuote');
    return quote ??
        ForexQuote(
          fromCurrencyCode: fromCurrencyCode,
          toCurrencyCode: toCurrencyCode,
          fromAmount: amount,
          toAmount: amount * 0.9,
          feeAmount: amount * 0.01,
          rate: 0.9,
          quoteNonce: 'nonce-1',
        );
  }

  @override
  Future<void> executeSwap({
    required String fromCurrencyCode,
    required String toCurrencyCode,
    required double amount,
    String? quoteNonce,
    int? acceptedSlippageBps,
  }) async {
    calls.add('executeSwap');
    if (swapError != null) throw swapError!;
    lastSwapArgs = {
      'fromCurrencyCode': fromCurrencyCode,
      'toCurrencyCode': toCurrencyCode,
      'amount': amount,
      'quoteNonce': quoteNonce,
      'acceptedSlippageBps': acceptedSlippageBps,
    };
  }

  @override
  Future<List<FxRateHistoryPoint>> fetchRateHistory(String quoteCurrencyCode, {int ticksBack = 100}) async {
    calls.add('fetchRateHistory');
    return rateHistory;
  }

  @override
  Future<List<MarketEvent>> fetchActiveMarketEvents() async {
    calls.add('fetchActiveMarketEvents');
    return activeMarketEvents;
  }

  @override
  Future<List<BankAccountOption>> fetchMyBankAccounts() async {
    calls.add('fetchMyBankAccounts');
    if (loadError != null) throw loadError!;
    return bankAccounts;
  }

  @override
  Future<TransferFundsResult> transferFunds({
    required String fromBankAccountId,
    required String toBankAccountId,
    required double amount,
    String? description,
  }) async {
    calls.add('transferFunds');
    if (transferError != null) throw transferError!;
    lastTransferArgs = {
      'fromBankAccountId': fromBankAccountId,
      'toBankAccountId': toBankAccountId,
      'amount': amount,
      'description': description,
    };
    return TransferFundsResult(amount: amount, currencyCode: 'EUR');
  }

  @override
  Future<List<GoldAmmPool>> fetchGoldPools() async {
    calls.add('fetchGoldPools');
    if (loadError != null) throw loadError!;
    return goldPools;
  }

  @override
  Future<GoldBalance> fetchMyGoldBalance() async {
    calls.add('fetchMyGoldBalance');
    if (loadError != null) throw loadError!;
    return goldBalance;
  }

  @override
  Future<GoldAmmSwapQuote> fetchGoldSwapQuote({
    required String direction,
    required String currencyCode,
    required double amount,
  }) async {
    calls.add('fetchGoldSwapQuote');
    if (goldError != null) throw goldError!;
    return goldSwapQuote ??
        GoldAmmSwapQuote(
          direction: direction,
          currencyCode: currencyCode,
          inputAmount: amount,
          outputAmount: amount * 0.98,
          feeAmount: amount * 0.01,
          slippagePercent: 0.5,
        );
  }

  @override
  Future<void> executeGoldSwap({
    required String direction,
    required String currencyCode,
    required double amount,
    required double minOutputAmount,
  }) async {
    calls.add('executeGoldSwap');
    if (goldError != null) throw goldError!;
    lastGoldSwapArgs = {
      'direction': direction,
      'currencyCode': currencyCode,
      'amount': amount,
      'minOutputAmount': minOutputAmount,
    };
  }

  @override
  Future<void> addGoldLiquidity({required String poolId, required double fiatAmount, required double maxGoldAmount}) async {
    calls.add('addGoldLiquidity');
    if (goldError != null) throw goldError!;
    lastAddLiquidityArgs = {'poolId': poolId, 'fiatAmount': fiatAmount, 'maxGoldAmount': maxGoldAmount};
  }

  @override
  Future<void> createGoldPool({required String currencyCode, required double fiatAmount, required double goldAmount}) async {
    calls.add('createGoldPool');
    if (goldError != null) throw goldError!;
    lastCreatePoolArgs = {'currencyCode': currencyCode, 'fiatAmount': fiatAmount, 'goldAmount': goldAmount};
  }

  @override
  Future<void> removeGoldLiquidity({required String positionId, required double shareFraction}) async {
    calls.add('removeGoldLiquidity');
    if (goldError != null) throw goldError!;
    lastRemoveLiquidityArgs = {'positionId': positionId, 'shareFraction': shareFraction};
  }
}
