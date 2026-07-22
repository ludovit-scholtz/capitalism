// Data models for the 6 Operations (admin) screens, mirroring
// `projects/frontend/src/stores/gameAdmin.ts` and
// `projects/frontend/src/views/Operations*.vue`. GraphQL field names
// verified against `Api/Types/Query.Admin.cs`/`Query.OperationsAdmin.cs`.

class GameAdminPlayer {
  const GameAdminPlayer({
    required this.id,
    required this.displayName,
    required this.email,
    required this.role,
    required this.personalCash,
    required this.totalCompanyCash,
    required this.companyCount,
    required this.cityNames,
    required this.lastLoginAtUtc,
  });

  final String id;
  final String displayName;
  final String email;
  final String role;
  final double personalCash;
  final double totalCompanyCash;
  final int companyCount;
  final List<String> cityNames;
  final String? lastLoginAtUtc;

  factory GameAdminPlayer.fromJson(Map<String, dynamic> json) => GameAdminPlayer(
    id: json['id'] as String,
    displayName: (json['displayName'] as String?) ?? '',
    email: (json['email'] as String?) ?? '',
    role: (json['role'] as String?) ?? 'PLAYER',
    personalCash: (json['personalCash'] as num?)?.toDouble() ?? 0,
    totalCompanyCash: (json['totalCompanyCash'] as num?)?.toDouble() ?? 0,
    companyCount: (json['companyCount'] as num?)?.toInt() ?? 0,
    cityNames: ((json['cityNames'] as List<dynamic>?) ?? const []).cast<String>(),
    lastLoginAtUtc: json['lastLoginAtUtc'] as String?,
  );
}

class GameAdminDashboard {
  const GameAdminDashboard({
    required this.moneySupply,
    required this.totalPersonalCash,
    required this.totalCompanyCash,
    required this.externalMoneyInflowLast100Ticks,
    required this.totalShippingCostsLast100Ticks,
    required this.players,
  });

  final double moneySupply;
  final double totalPersonalCash;
  final double totalCompanyCash;
  final double externalMoneyInflowLast100Ticks;
  final double totalShippingCostsLast100Ticks;
  final List<GameAdminPlayer> players;

  factory GameAdminDashboard.fromJson(Map<String, dynamic> json) => GameAdminDashboard(
    moneySupply: (json['moneySupply'] as num?)?.toDouble() ?? 0,
    totalPersonalCash: (json['totalPersonalCash'] as num?)?.toDouble() ?? 0,
    totalCompanyCash: (json['totalCompanyCash'] as num?)?.toDouble() ?? 0,
    externalMoneyInflowLast100Ticks: (json['externalMoneyInflowLast100Ticks'] as num?)?.toDouble() ?? 0,
    totalShippingCostsLast100Ticks: (json['totalShippingCostsLast100Ticks'] as num?)?.toDouble() ?? 0,
    players: ((json['players'] as List<dynamic>?) ?? const []).map((e) => GameAdminPlayer.fromJson(e as Map<String, dynamic>)).toList(),
  );
}

class MoneyFlowItem {
  const MoneyFlowItem({required this.label, required this.amount, required this.percentage});

  final String label;
  final double amount;
  final double percentage;

  factory MoneyFlowItem.fromJson(Map<String, dynamic> json) => MoneyFlowItem(
    label: (json['label'] as String?) ?? '',
    amount: (json['amount'] as num?)?.toDouble() ?? 0,
    percentage: (json['percentage'] as num?)?.toDouble() ?? 0,
  );
}

class OperationsStatistics {
  const OperationsStatistics({
    required this.totalInflow,
    required this.totalOutflow,
    required this.netFlow,
    required this.totalPlayerCount,
    required this.totalCompanyCount,
    required this.inflowItems,
    required this.outflowItems,
  });

  final double totalInflow;
  final double totalOutflow;
  final double netFlow;
  final int totalPlayerCount;
  final int totalCompanyCount;
  final List<MoneyFlowItem> inflowItems;
  final List<MoneyFlowItem> outflowItems;

  factory OperationsStatistics.fromJson(Map<String, dynamic> json) => OperationsStatistics(
    totalInflow: (json['totalInflow'] as num?)?.toDouble() ?? 0,
    totalOutflow: (json['totalOutflow'] as num?)?.toDouble() ?? 0,
    netFlow: (json['netFlow'] as num?)?.toDouble() ?? 0,
    totalPlayerCount: (json['totalPlayerCount'] as num?)?.toInt() ?? 0,
    totalCompanyCount: (json['totalCompanyCount'] as num?)?.toInt() ?? 0,
    inflowItems: ((json['inflowItems'] as List<dynamic>?) ?? const []).map((e) => MoneyFlowItem.fromJson(e as Map<String, dynamic>)).toList(),
    outflowItems: ((json['outflowItems'] as List<dynamic>?) ?? const []).map((e) => MoneyFlowItem.fromJson(e as Map<String, dynamic>)).toList(),
  );
}

class ProductAnalyticsRow {
  const ProductAnalyticsRow({
    required this.productName,
    required this.industry,
    required this.totalSold,
    required this.totalRevenue,
    required this.avgSellingPrice,
    required this.activeSellerCount,
  });

  final String productName;
  final String? industry;
  final double totalSold;
  final double totalRevenue;
  final double avgSellingPrice;
  final int activeSellerCount;

  factory ProductAnalyticsRow.fromJson(Map<String, dynamic> json) => ProductAnalyticsRow(
    productName: (json['productName'] as String?) ?? '',
    industry: json['industry'] as String?,
    totalSold: (json['totalSold'] as num?)?.toDouble() ?? 0,
    totalRevenue: (json['totalRevenue'] as num?)?.toDouble() ?? 0,
    avgSellingPrice: (json['avgSellingPrice'] as num?)?.toDouble() ?? 0,
    activeSellerCount: (json['activeSellerCount'] as num?)?.toInt() ?? 0,
  );
}

class AdminNewsLocalization {
  const AdminNewsLocalization({required this.locale, required this.title, required this.summary});

  final String locale;
  final String title;
  final String? summary;

  factory AdminNewsLocalization.fromJson(Map<String, dynamic> json) => AdminNewsLocalization(
    locale: (json['locale'] as String?) ?? 'en',
    title: (json['title'] as String?) ?? '',
    summary: json['summary'] as String?,
  );
}

class AdminNewsEntry {
  const AdminNewsEntry({required this.id, required this.entryType, required this.status, required this.localizations});

  final String id;
  final String entryType;

  /// `DRAFT` or `PUBLISHED`.
  final String status;
  final List<AdminNewsLocalization> localizations;

  AdminNewsLocalization? localizationFor(String locale) {
    for (final candidate in localizations) {
      if (candidate.locale == locale) return candidate;
    }
    for (final candidate in localizations) {
      if (candidate.locale == 'en') return candidate;
    }
    return localizations.isEmpty ? null : localizations.first;
  }

  factory AdminNewsEntry.fromJson(Map<String, dynamic> json) => AdminNewsEntry(
    id: json['id'] as String,
    entryType: (json['entryType'] as String?) ?? 'NEWS',
    status: (json['status'] as String?) ?? 'DRAFT',
    localizations: ((json['localizations'] as List<dynamic>?) ?? const [])
        .map((e) => AdminNewsLocalization.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}
