// Data models for the Personal Ledger screen, mirroring
// `projects/frontend/src/views/PersonalLedgerView.vue`. GraphQL field
// names verified against `Api/Types/Query.Auth.cs` (`personAccount`).

class PersonalShareholding {
  const PersonalShareholding({required this.companyName, required this.shareCount, required this.ownershipRatio, required this.marketValue});

  final String companyName;
  final double shareCount;
  final double ownershipRatio;
  final double marketValue;

  factory PersonalShareholding.fromJson(Map<String, dynamic> json) => PersonalShareholding(
    companyName: (json['companyName'] as String?) ?? '',
    shareCount: (json['shareCount'] as num?)?.toDouble() ?? 0,
    ownershipRatio: (json['ownershipRatio'] as num?)?.toDouble() ?? 0,
    marketValue: (json['marketValue'] as num?)?.toDouble() ?? 0,
  );
}

class PersonalDividendPayment {
  const PersonalDividendPayment({
    this.id = '',
    required this.companyName,
    required this.totalAmount,
    required this.gameYear,
    this.recordedAtTick = 0,
    this.recordedAtUtc = '',
    this.description,
  });

  final String id;
  final String companyName;
  final double totalAmount;
  final int gameYear;
  final int recordedAtTick;
  final String recordedAtUtc;
  final String? description;

  factory PersonalDividendPayment.fromJson(Map<String, dynamic> json) => PersonalDividendPayment(
    id: (json['id'] as String?) ?? '',
    companyName: (json['companyName'] as String?) ?? '',
    totalAmount: (json['totalAmount'] as num?)?.toDouble() ?? 0,
    gameYear: (json['gameYear'] as num?)?.toInt() ?? 0,
    recordedAtTick: (json['recordedAtTick'] as num?)?.toInt() ?? 0,
    recordedAtUtc: (json['recordedAtUtc'] as String?) ?? '',
    description: json['description'] as String?,
  );
}

class PersonalInterestPayment {
  const PersonalInterestPayment({
    required this.id,
    required this.companyName,
    required this.amount,
    required this.recordedAtTick,
    required this.recordedAtUtc,
    required this.currencyCode,
    this.bankBuildingName,
    this.description,
  });

  final String id;
  final String companyName;
  final double amount;
  final int recordedAtTick;
  final String recordedAtUtc;
  final String currencyCode;
  final String? bankBuildingName;
  final String? description;

  factory PersonalInterestPayment.fromJson(Map<String, dynamic> json) => PersonalInterestPayment(
    id: json['id'] as String,
    companyName: (json['companyName'] as String?) ?? '',
    amount: (json['amount'] as num?)?.toDouble() ?? 0,
    recordedAtTick: (json['recordedAtTick'] as num?)?.toInt() ?? 0,
    recordedAtUtc: (json['recordedAtUtc'] as String?) ?? '',
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    bankBuildingName: json['bankBuildingName'] as String?,
    description: json['description'] as String?,
  );
}

class PersonalStockTrade {
  const PersonalStockTrade({required this.companyName, required this.direction, required this.shareCount, required this.totalValue});

  final String companyName;

  /// `BUY` or `SELL`.
  final String direction;
  final double shareCount;
  final double totalValue;

  factory PersonalStockTrade.fromJson(Map<String, dynamic> json) => PersonalStockTrade(
    companyName: (json['companyName'] as String?) ?? '',
    direction: (json['direction'] as String?) ?? 'BUY',
    shareCount: (json['shareCount'] as num?)?.toDouble() ?? 0,
    totalValue: (json['totalValue'] as num?)?.toDouble() ?? 0,
  );
}

class PersonAccount {
  const PersonAccount({
    required this.displayName,
    required this.personalCash,
    required this.taxReserve,
    required this.availableCash,
    required this.totalNetWealth,
    required this.shareholdings,
    required this.dividendPayments,
    required this.stockTrades,
    this.interestPayments = const [],
  });

  final String displayName;
  final double personalCash;
  final double taxReserve;
  final double availableCash;
  final double totalNetWealth;
  final List<PersonalShareholding> shareholdings;
  final List<PersonalDividendPayment> dividendPayments;
  final List<PersonalStockTrade> stockTrades;
  final List<PersonalInterestPayment> interestPayments;

  factory PersonAccount.fromJson(Map<String, dynamic> json) => PersonAccount(
    displayName: (json['displayName'] as String?) ?? '',
    personalCash: (json['personalCash'] as num?)?.toDouble() ?? 0,
    taxReserve: (json['taxReserve'] as num?)?.toDouble() ?? 0,
    availableCash: (json['availableCash'] as num?)?.toDouble() ?? 0,
    totalNetWealth: (json['totalNetWealth'] as num?)?.toDouble() ?? 0,
    shareholdings: ((json['shareholdings'] as List<dynamic>?) ?? const [])
        .map((e) => PersonalShareholding.fromJson(e as Map<String, dynamic>))
        .toList(),
    dividendPayments: ((json['dividendPayments'] as List<dynamic>?) ?? const [])
        .map((e) => PersonalDividendPayment.fromJson(e as Map<String, dynamic>))
        .toList(),
    stockTrades: ((json['stockTrades'] as List<dynamic>?) ?? const [])
        .map((e) => PersonalStockTrade.fromJson(e as Map<String, dynamic>))
        .toList(),
    interestPayments: ((json['interestPayments'] as List<dynamic>?) ?? const [])
        .map((e) => PersonalInterestPayment.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}
