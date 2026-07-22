// Data models for the Forex Exchange screen, mirroring
// `projects/frontend/src/views/ForexExchangeView.vue`. GraphQL field names
// verified against `Api/Types/Inputs.Forex.cs`.

class FxRate {
  const FxRate({required this.baseCurrencyCode, required this.quoteCurrencyCode, required this.rate});

  final String baseCurrencyCode;
  final String quoteCurrencyCode;
  final double rate;

  factory FxRate.fromJson(Map<String, dynamic> json) => FxRate(
    baseCurrencyCode: (json['baseCurrencyCode'] as String?) ?? 'EUR',
    quoteCurrencyCode: (json['quoteCurrencyCode'] as String?) ?? '',
    rate: (json['rate'] as num?)?.toDouble() ?? 0,
  );
}

class CurrencyBalance {
  const CurrencyBalance({required this.currencyCode, required this.currencySymbol, required this.balance});

  final String currencyCode;
  final String currencySymbol;
  final double balance;

  factory CurrencyBalance.fromJson(Map<String, dynamic> json) => CurrencyBalance(
    currencyCode: (json['currencyCode'] as String?) ?? '',
    currencySymbol: (json['currencySymbol'] as String?) ?? '',
    balance: (json['balance'] as num?)?.toDouble() ?? 0,
  );
}

class ForexTrade {
  const ForexTrade({
    required this.fromCurrencyCode,
    required this.toCurrencyCode,
    required this.fromAmount,
    required this.toAmount,
    required this.rate,
    required this.executedAtUtc,
  });

  final String fromCurrencyCode;
  final String toCurrencyCode;
  final double fromAmount;
  final double toAmount;
  final double rate;
  final String executedAtUtc;

  factory ForexTrade.fromJson(Map<String, dynamic> json) => ForexTrade(
    fromCurrencyCode: (json['fromCurrencyCode'] as String?) ?? '',
    toCurrencyCode: (json['toCurrencyCode'] as String?) ?? '',
    fromAmount: (json['fromAmount'] as num?)?.toDouble() ?? 0,
    toAmount: (json['toAmount'] as num?)?.toDouble() ?? 0,
    rate: (json['rate'] as num?)?.toDouble() ?? 0,
    executedAtUtc: (json['executedAtUtc'] as String?) ?? '',
  );
}

class ForexQuote {
  const ForexQuote({
    required this.fromCurrencyCode,
    required this.toCurrencyCode,
    required this.fromAmount,
    required this.toAmount,
    required this.feeAmount,
    required this.rate,
    required this.quoteNonce,
    this.quoteExpiresInSeconds = 30,
  });

  final String fromCurrencyCode;
  final String toCurrencyCode;
  final double fromAmount;
  final double toAmount;
  final double feeAmount;
  final double rate;
  final String? quoteNonce;

  /// Quote validity window — `ExecuteForexSwapInput.quoteNonce` must be
  /// redeemed before this elapses (`Query.Forex.cs`'s 30s quote window).
  final int quoteExpiresInSeconds;

  factory ForexQuote.fromJson(Map<String, dynamic> json) => ForexQuote(
    fromCurrencyCode: (json['fromCurrencyCode'] as String?) ?? '',
    toCurrencyCode: (json['toCurrencyCode'] as String?) ?? '',
    fromAmount: (json['fromAmount'] as num?)?.toDouble() ?? 0,
    toAmount: (json['toAmount'] as num?)?.toDouble() ?? 0,
    feeAmount: (json['feeAmount'] as num?)?.toDouble() ?? 0,
    rate: (json['rate'] as num?)?.toDouble() ?? 0,
    quoteNonce: json['quoteNonce'] as String?,
    quoteExpiresInSeconds: (json['quoteExpiresInSeconds'] as num?)?.toInt() ?? 30,
  );
}

class FxRateHistoryPoint {
  const FxRateHistoryPoint({required this.gameTick, required this.midRate});

  final int gameTick;
  final double midRate;

  factory FxRateHistoryPoint.fromJson(Map<String, dynamic> json) => FxRateHistoryPoint(
    gameTick: (json['gameTick'] as num?)?.toInt() ?? 0,
    midRate: (json['midRate'] as num?)?.toDouble() ?? 0,
  );
}

/// Mirrors `MarketEventView` (`Api/Types/Query.Economy.cs`) — used for the
/// commodity-shock banner.
class MarketEvent {
  const MarketEvent({
    required this.id,
    required this.title,
    required this.description,
    required this.magnitudeMultiplier,
    required this.ticksRemaining,
    required this.affectedResourceName,
  });

  final String id;
  final String title;
  final String description;
  final double magnitudeMultiplier;
  final int ticksRemaining;
  final String? affectedResourceName;

  factory MarketEvent.fromJson(Map<String, dynamic> json) => MarketEvent(
    id: json['id'] as String,
    title: (json['title'] as String?) ?? '',
    description: (json['description'] as String?) ?? '',
    magnitudeMultiplier: (json['magnitudeMultiplier'] as num?)?.toDouble() ?? 1,
    ticksRemaining: (json['ticksRemaining'] as num?)?.toInt() ?? 0,
    affectedResourceName: json['affectedResourceName'] as String?,
  );
}

/// Mirrors `PlayerBankAccountSummary` fields needed for the transfer-panel
/// account picker (`Api/Types/Mutation.BankAccountTransfer.cs`).
class BankAccountOption {
  const BankAccountOption({
    required this.id,
    required this.accountNumber,
    required this.currencyCode,
    required this.currencySymbol,
    required this.balance,
    required this.ownerDisplayName,
  });

  final String id;
  final String accountNumber;
  final String currencyCode;
  final String currencySymbol;
  final double balance;
  final String ownerDisplayName;

  factory BankAccountOption.fromJson(Map<String, dynamic> json) => BankAccountOption(
    id: json['id'] as String,
    accountNumber: (json['accountNumber'] as String?) ?? '',
    currencyCode: (json['currencyCode'] as String?) ?? '',
    currencySymbol: (json['currencySymbol'] as String?) ?? '',
    balance: (json['balance'] as num?)?.toDouble() ?? 0,
    ownerDisplayName: (json['ownerDisplayName'] as String?) ?? '',
  );
}

/// Mirrors `TransferFundsResult` (`Api/Types/Mutation.BankAccountTransfer.cs`).
class TransferFundsResult {
  const TransferFundsResult({required this.amount, required this.currencyCode});

  final double amount;
  final String currencyCode;

  factory TransferFundsResult.fromJson(Map<String, dynamic> json) => TransferFundsResult(
    amount: (json['amount'] as num?)?.toDouble() ?? 0,
    currencyCode: (json['currencyCode'] as String?) ?? '',
  );
}

/// Mirrors `GoldAmmPositionInfo` (`Api/Types/Query.GoldAmm.cs`).
class GoldAmmPosition {
  const GoldAmmPosition({
    required this.id,
    required this.liquidityShares,
    required this.sharePercent,
    required this.claimableFiat,
    required this.claimableGold,
  });

  final String id;
  final double liquidityShares;
  final double sharePercent;
  final double claimableFiat;
  final double claimableGold;

  factory GoldAmmPosition.fromJson(Map<String, dynamic> json) => GoldAmmPosition(
    id: json['id'] as String,
    liquidityShares: (json['liquidityShares'] as num?)?.toDouble() ?? 0,
    sharePercent: (json['sharePercent'] as num?)?.toDouble() ?? 0,
    claimableFiat: (json['claimableFiat'] as num?)?.toDouble() ?? 0,
    claimableGold: (json['claimableGold'] as num?)?.toDouble() ?? 0,
  );
}

/// Mirrors `GoldAmmPoolInfo` (`Api/Types/Query.GoldAmm.cs`).
class GoldAmmPool {
  const GoldAmmPool({
    required this.id,
    required this.currencyCode,
    required this.currencySymbol,
    required this.fiatReserve,
    required this.goldReserve,
    required this.impliedGoldPrice,
    this.myPosition,
  });

  final String id;
  final String currencyCode;
  final String currencySymbol;
  final double fiatReserve;
  final double goldReserve;
  final double impliedGoldPrice;
  final GoldAmmPosition? myPosition;

  factory GoldAmmPool.fromJson(Map<String, dynamic> json) => GoldAmmPool(
    id: json['id'] as String,
    currencyCode: (json['currencyCode'] as String?) ?? '',
    currencySymbol: (json['currencySymbol'] as String?) ?? '',
    fiatReserve: (json['fiatReserve'] as num?)?.toDouble() ?? 0,
    goldReserve: (json['goldReserve'] as num?)?.toDouble() ?? 0,
    impliedGoldPrice: (json['impliedGoldPrice'] as num?)?.toDouble() ?? 0,
    myPosition: json['myPosition'] == null ? null : GoldAmmPosition.fromJson(json['myPosition'] as Map<String, dynamic>),
  );
}

/// Mirrors `GoldBalanceInfo` (`Api/Types/Query.GoldAmm.cs`).
class GoldBalance {
  const GoldBalance({required this.balance, required this.blockedInPools, required this.availableBalance});

  final double balance;
  final double blockedInPools;
  final double availableBalance;

  factory GoldBalance.fromJson(Map<String, dynamic> json) => GoldBalance(
    balance: (json['balance'] as num?)?.toDouble() ?? 0,
    blockedInPools: (json['blockedInPools'] as num?)?.toDouble() ?? 0,
    availableBalance: (json['availableBalance'] as num?)?.toDouble() ?? 0,
  );
}

/// Mirrors `GoldAmmSwapQuote` (`Api/Types/Query.GoldAmm.cs`).
class GoldAmmSwapQuote {
  const GoldAmmSwapQuote({
    required this.direction,
    required this.currencyCode,
    required this.inputAmount,
    required this.outputAmount,
    required this.feeAmount,
    required this.slippagePercent,
  });

  /// `FIAT_TO_GOLD` or `GOLD_TO_FIAT`.
  final String direction;
  final String currencyCode;
  final double inputAmount;
  final double outputAmount;
  final double feeAmount;
  final double slippagePercent;

  factory GoldAmmSwapQuote.fromJson(Map<String, dynamic> json) => GoldAmmSwapQuote(
    direction: (json['direction'] as String?) ?? 'FIAT_TO_GOLD',
    currencyCode: (json['currencyCode'] as String?) ?? '',
    inputAmount: (json['inputAmount'] as num?)?.toDouble() ?? 0,
    outputAmount: (json['outputAmount'] as num?)?.toDouble() ?? 0,
    feeAmount: (json['feeAmount'] as num?)?.toDouble() ?? 0,
    slippagePercent: (json['slippagePercent'] as num?)?.toDouble() ?? 0,
  );
}
