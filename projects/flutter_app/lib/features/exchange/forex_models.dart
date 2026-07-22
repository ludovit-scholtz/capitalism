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
  });

  final String fromCurrencyCode;
  final String toCurrencyCode;
  final double fromAmount;
  final double toAmount;
  final double feeAmount;
  final double rate;
  final String? quoteNonce;

  factory ForexQuote.fromJson(Map<String, dynamic> json) => ForexQuote(
    fromCurrencyCode: (json['fromCurrencyCode'] as String?) ?? '',
    toCurrencyCode: (json['toCurrencyCode'] as String?) ?? '',
    fromAmount: (json['fromAmount'] as num?)?.toDouble() ?? 0,
    toAmount: (json['toAmount'] as num?)?.toDouble() ?? 0,
    feeAmount: (json['feeAmount'] as num?)?.toDouble() ?? 0,
    rate: (json['rate'] as num?)?.toDouble() ?? 0,
    quoteNonce: json['quoteNonce'] as String?,
  );
}
