import 'package:capitalism_app/features/exchange/forex_models.dart';
import 'package:capitalism_app/features/exchange/forex_service.dart';

class FakeForexService implements ForexService {
  FakeForexService({
    this.rates = const [],
    this.balances = const [],
    this.history = const [],
    this.quote,
    this.loadError,
    this.swapError,
  });

  final List<FxRate> rates;
  final List<CurrencyBalance> balances;
  final List<ForexTrade> history;
  final ForexQuote? quote;
  final Object? loadError;
  final Object? swapError;

  final List<String> calls = [];
  Map<String, dynamic>? lastSwapArgs;

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
  Future<void> executeSwap({required String fromCurrencyCode, required String toCurrencyCode, required double amount}) async {
    calls.add('executeSwap');
    if (swapError != null) throw swapError!;
    lastSwapArgs = {'fromCurrencyCode': fromCurrencyCode, 'toCurrencyCode': toCurrencyCode, 'amount': amount};
  }
}
