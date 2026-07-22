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

const _rateHistoryQuery = r'''
  query GetFxRateHistory($quoteCurrencyCode: String!, $ticksBack: Int) {
    fxRateHistory(quoteCurrencyCode: $quoteCurrencyCode, ticksBack: $ticksBack) { gameTick midRate }
  }
''';

const _activeMarketEventsQuery = r'''
  query ForexActiveMarketEvents {
    getActiveMarketEvents { id title description magnitudeMultiplier ticksRemaining affectedResourceName }
  }
''';

const _myBankAccountsQuery = r'''
  query ForexMyBankAccounts {
    myBankAccounts { id accountNumber currencyCode currencySymbol balance ownerDisplayName }
  }
''';

const _transferFundsMutation = r'''
  mutation TransferFunds($input: TransferFundsInput!) {
    transferFunds(input: $input) { amount currencyCode }
  }
''';

const _goldPoolsQuery = r'''
  query GoldAmmPools {
    goldAmmPools {
      id currencyCode currencySymbol fiatReserve goldReserve impliedGoldPrice
      myPosition { id liquidityShares sharePercent claimableFiat claimableGold }
    }
  }
''';

const _myGoldBalanceQuery = r'''
  query MyGoldBalance { myGoldBalance { balance blockedInPools availableBalance } }
''';

const _goldSwapQuoteQuery = r'''
  query GoldAmmSwapQuote($input: GetGoldAmmSwapQuoteInput!) {
    goldAmmSwapQuote(input: $input) { direction currencyCode inputAmount outputAmount feeAmount slippagePercent }
  }
''';

const _executeGoldSwapMutation = r'''
  mutation ExecuteGoldAmmSwap($input: ExecuteGoldAmmSwapInput!) {
    executeGoldAmmSwap(input: $input) { tradeId direction currencyCode inputAmount outputAmount feeAmount }
  }
''';

const _addGoldLiquidityMutation = r'''
  mutation AddGoldAmmLiquidity($input: AddGoldAmmLiquidityInput!) {
    addGoldAmmLiquidity(input: $input) { poolId positionId liquidityShares }
  }
''';

const _createGoldPoolMutation = r'''
  mutation CreateGoldAmmPool($input: CreateGoldAmmPoolInput!) {
    createGoldAmmPool(input: $input) { poolId positionId liquidityShares }
  }
''';

const _removeGoldLiquidityMutation = r'''
  mutation RemoveGoldAmmLiquidity($input: RemoveGoldAmmLiquidityInput!) {
    removeGoldAmmLiquidity(input: $input) { positionId fiatReturned goldReturned remainingShares }
  }
''';

/// GraphQL calls for the Forex Exchange screen, matching
/// `projects/frontend/src/views/ForexExchangeView.vue`'s Swap/Transfer/
/// Rates/History/Gold tabs.
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

  Future<void> executeSwap({
    required String fromCurrencyCode,
    required String toCurrencyCode,
    required double amount,
    String? quoteNonce,
    int? acceptedSlippageBps,
  }) {
    return _graphQlService.request(
      _executeSwapMutation,
      variables: {
        'input': {
          'fromCurrencyCode': fromCurrencyCode,
          'toCurrencyCode': toCurrencyCode,
          'amount': amount,
          'quoteNonce': quoteNonce,
          'acceptedSlippageBps': acceptedSlippageBps,
        },
      },
    );
  }

  Future<List<FxRateHistoryPoint>> fetchRateHistory(String quoteCurrencyCode, {int ticksBack = 100}) async {
    final result = await _graphQlService.request(
      _rateHistoryQuery,
      variables: {'quoteCurrencyCode': quoteCurrencyCode, 'ticksBack': ticksBack},
    );
    final list = result['fxRateHistory'] as List<dynamic>? ?? const [];
    return list.map((e) => FxRateHistoryPoint.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<MarketEvent>> fetchActiveMarketEvents() async {
    final result = await _graphQlService.request(_activeMarketEventsQuery);
    final list = result['getActiveMarketEvents'] as List<dynamic>? ?? const [];
    return list.map((e) => MarketEvent.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<BankAccountOption>> fetchMyBankAccounts() async {
    final result = await _graphQlService.request(_myBankAccountsQuery);
    final list = result['myBankAccounts'] as List<dynamic>? ?? const [];
    return list.map((e) => BankAccountOption.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<TransferFundsResult> transferFunds({
    required String fromBankAccountId,
    required String toBankAccountId,
    required double amount,
    String? description,
  }) async {
    final result = await _graphQlService.request(
      _transferFundsMutation,
      variables: {
        'input': {
          'fromBankAccountId': fromBankAccountId,
          'toBankAccountId': toBankAccountId,
          'amount': amount,
          'description': description,
        },
      },
    );
    return TransferFundsResult.fromJson(result['transferFunds'] as Map<String, dynamic>);
  }

  Future<List<GoldAmmPool>> fetchGoldPools() async {
    final result = await _graphQlService.request(_goldPoolsQuery);
    final list = result['goldAmmPools'] as List<dynamic>? ?? const [];
    return list.map((e) => GoldAmmPool.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<GoldBalance> fetchMyGoldBalance() async {
    final result = await _graphQlService.request(_myGoldBalanceQuery);
    return GoldBalance.fromJson(result['myGoldBalance'] as Map<String, dynamic>);
  }

  Future<GoldAmmSwapQuote> fetchGoldSwapQuote({
    required String direction,
    required String currencyCode,
    required double amount,
  }) async {
    final result = await _graphQlService.request(
      _goldSwapQuoteQuery,
      variables: {
        'input': {'direction': direction, 'currencyCode': currencyCode, 'amount': amount},
      },
    );
    return GoldAmmSwapQuote.fromJson(result['goldAmmSwapQuote'] as Map<String, dynamic>);
  }

  Future<void> executeGoldSwap({
    required String direction,
    required String currencyCode,
    required double amount,
    required double minOutputAmount,
  }) {
    return _graphQlService.request(
      _executeGoldSwapMutation,
      variables: {
        'input': {
          'direction': direction,
          'currencyCode': currencyCode,
          'amount': amount,
          'minOutputAmount': minOutputAmount,
        },
      },
    );
  }

  Future<void> addGoldLiquidity({required String poolId, required double fiatAmount, required double maxGoldAmount}) {
    return _graphQlService.request(
      _addGoldLiquidityMutation,
      variables: {
        'input': {'poolId': poolId, 'fiatAmount': fiatAmount, 'maxGoldAmount': maxGoldAmount},
      },
    );
  }

  Future<void> createGoldPool({required String currencyCode, required double fiatAmount, required double goldAmount}) {
    return _graphQlService.request(
      _createGoldPoolMutation,
      variables: {
        'input': {'currencyCode': currencyCode, 'fiatAmount': fiatAmount, 'goldAmount': goldAmount},
      },
    );
  }

  Future<void> removeGoldLiquidity({required String positionId, required double shareFraction}) {
    return _graphQlService.request(
      _removeGoldLiquidityMutation,
      variables: {
        'input': {'positionId': positionId, 'shareFraction': shareFraction},
      },
    );
  }
}
