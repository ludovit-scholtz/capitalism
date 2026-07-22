import '../../core/graphql/graphql_service.dart';
import 'forex_models.dart';

const _ratesQuery = r'''
  query ForexRates { fxRates { baseCurrencyCode quoteCurrencyCode rate rateDate source quoteCurrencySymbol } }
''';

const _balancesQuery = r'''
  query ForexBalances { playerCurrencyBalances { currencyCode currencySymbol balance } }
''';

const _historyQuery = r'''
  query ForexHistory {
    forexTradeHistory { id fromCurrencyCode toCurrencyCode fromAmount toAmount feeAmount rate executedAtTick executedAtUtc fromCurrencySymbol toCurrencySymbol }
  }
''';

const _quoteQuery = r'''
  query ForexQuote($input: GetForexQuoteInput!) {
    forexQuote(input: $input) {
      fromCurrencyCode toCurrencyCode fromAmount toAmount feeAmount feePercent rate availableFromBalance
      fromCurrencySymbol toCurrencySymbol quoteNonce quotedAtUtc quoteExpiresInSeconds
    }
  }
''';

const _executeSwapMutation = r'''
  mutation ExecuteForexSwap($input: ExecuteForexSwapInput!) {
    executeForexSwap(input: $input) { tradeId fromCurrencyCode toCurrencyCode fromAmount toAmount feeAmount rate newFromBalance newToBalance }
  }
''';

/// GraphQL calls for the Forex Exchange screen, matching
/// `projects/frontend/src/views/ForexExchangeView.vue`'s Swap/Rates/History
/// tabs. Deliberately not ported: the Transfer tab (a generic bank-transfer
/// panel, not forex-specific) and the Gold tab's full AMM
/// (quote/swap/pool-create/add-liquidity/remove-liquidity) — see the
/// screen's top-of-file comment.
class ForexService {
  const ForexService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<FxRate>> fetchRates() async {
    final result = await _graphQlService.request(_ratesQuery);
    final list = result['fxRates'] as List<dynamic>? ?? const [];
    return list.map((e) => FxRate.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<CurrencyBalance>> fetchBalances() async {
    final result = await _graphQlService.request(_balancesQuery);
    final list = result['playerCurrencyBalances'] as List<dynamic>? ?? const [];
    return list.map((e) => CurrencyBalance.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<ForexTrade>> fetchHistory() async {
    final result = await _graphQlService.request(_historyQuery);
    final list = result['forexTradeHistory'] as List<dynamic>? ?? const [];
    return list.map((e) => ForexTrade.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<ForexQuote> fetchQuote({required String fromCurrencyCode, required String toCurrencyCode, required double amount}) async {
    final result = await _graphQlService.request(
      _quoteQuery,
      variables: {
        'input': {'fromCurrencyCode': fromCurrencyCode, 'toCurrencyCode': toCurrencyCode, 'amount': amount},
      },
    );
    return ForexQuote.fromJson(result['forexQuote'] as Map<String, dynamic>);
  }

  Future<void> executeSwap({required String fromCurrencyCode, required String toCurrencyCode, required double amount}) {
    return _graphQlService.request(
      _executeSwapMutation,
      variables: {
        'input': {'fromCurrencyCode': fromCurrencyCode, 'toCurrencyCode': toCurrencyCode, 'amount': amount},
      },
    );
  }
}
