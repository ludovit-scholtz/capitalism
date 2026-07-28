// Data models for the Ledger, Company Contracts, Company Settings, and
// Company Research screens, mirroring `projects/frontend/src/views/
// LedgerView.vue`/`CompanyContractsView.vue`/`CompanySettingsView.vue`/
// `CompanyResearchView.vue`. GraphQL field names verified against
// `Api/Types/Query.Ledger.cs`, `Query.GovernmentContracts.cs`,
// `Mutation.GovernmentContracts.cs`, `Mutation.Company.cs`,
// `Mutation.StockExchange.DividendGovernance.cs`, `Query.Research.cs`.

class CompanyLedger {
  const CompanyLedger({
    required this.companyName,
    required this.gameYear,
    required this.currentCash,
    required this.primaryCurrencyCode,
    required this.totalRevenue,
    required this.totalPurchasingCosts,
    required this.totalShippingCosts,
    required this.totalLaborCosts,
    required this.totalEnergyCosts,
    required this.totalMarketingCosts,
    required this.totalTaxPaid,
    required this.totalOtherCosts,
    required this.netIncome,
    required this.totalAssets,
    this.isCurrentGameYear = true,
    this.primaryCurrencySymbol,
    this.hasMixedCurrencies = false,
    this.totalGovernmentContractRevenue = 0,
    this.totalMediaHouseIncome = 0,
    this.totalRentIncome = 0,
    this.totalPropertyMaintenance = 0,
    this.taxableIncome = 0,
    this.estimatedIncomeTax = 0,
    this.totalDepositInterestReceived = 0,
    this.totalDepositInterestPaid = 0,
    this.totalLoanInterestIncome = 0,
    this.totalLoanInterestExpense = 0,
    this.propertyValue = 0,
    this.propertyAppreciation = 0,
    this.buildingValue = 0,
    this.inventoryValue = 0,
    this.totalDepositsPlaced = 0,
    this.totalPropertyPurchases = 0,
    this.totalStockPurchaseCashOut = 0,
    this.totalStockSaleCashIn = 0,
    this.cashFromOperations = 0,
    this.cashFromInvestments = 0,
    this.cashFromBanking = 0,
    this.firstRecordedTick = 0,
    this.lastRecordedTick = 0,
    this.incomeTaxDueAtTick = 0,
    this.incomeTaxDueGameTimeUtc,
    this.incomeTaxDueGameYear = 0,
    this.isIncomeTaxSettled = false,
    this.buildingSummaries = const [],
    this.history = const [],
  });

  final String companyName;
  final int gameYear;
  final double currentCash;
  final String primaryCurrencyCode;
  final double totalRevenue;
  final double totalPurchasingCosts;
  final double totalShippingCosts;
  final double totalLaborCosts;
  final double totalEnergyCosts;
  final double totalMarketingCosts;
  final double totalTaxPaid;
  final double totalOtherCosts;
  final double netIncome;
  final double totalAssets;
  final bool isCurrentGameYear;
  final String? primaryCurrencySymbol;
  final bool hasMixedCurrencies;
  final double totalGovernmentContractRevenue;
  final double totalMediaHouseIncome;
  final double totalRentIncome;
  final double totalPropertyMaintenance;
  final double taxableIncome;
  final double estimatedIncomeTax;
  final double totalDepositInterestReceived;
  final double totalDepositInterestPaid;
  final double totalLoanInterestIncome;
  final double totalLoanInterestExpense;
  final double propertyValue;
  final double propertyAppreciation;
  final double buildingValue;
  final double inventoryValue;
  final double totalDepositsPlaced;
  final double totalPropertyPurchases;
  final double totalStockPurchaseCashOut;
  final double totalStockSaleCashIn;
  final double cashFromOperations;
  final double cashFromInvestments;
  final double cashFromBanking;
  final int firstRecordedTick;
  final int lastRecordedTick;
  final int incomeTaxDueAtTick;
  final String? incomeTaxDueGameTimeUtc;
  final int incomeTaxDueGameYear;
  final bool isIncomeTaxSettled;
  final List<BuildingLedgerSummary> buildingSummaries;
  final List<CompanyLedgerHistoryYear> history;

  factory CompanyLedger.fromJson(Map<String, dynamic> json) => CompanyLedger(
    companyName: (json['companyName'] as String?) ?? '',
    gameYear: (json['gameYear'] as num?)?.toInt() ?? 0,
    currentCash: (json['currentCash'] as num?)?.toDouble() ?? 0,
    primaryCurrencyCode: (json['primaryCurrencyCode'] as String?) ?? 'EUR',
    totalRevenue: (json['totalRevenue'] as num?)?.toDouble() ?? 0,
    totalPurchasingCosts: (json['totalPurchasingCosts'] as num?)?.toDouble() ?? 0,
    totalShippingCosts: (json['totalShippingCosts'] as num?)?.toDouble() ?? 0,
    totalLaborCosts: (json['totalLaborCosts'] as num?)?.toDouble() ?? 0,
    totalEnergyCosts: (json['totalEnergyCosts'] as num?)?.toDouble() ?? 0,
    totalMarketingCosts: (json['totalMarketingCosts'] as num?)?.toDouble() ?? 0,
    totalTaxPaid: (json['totalTaxPaid'] as num?)?.toDouble() ?? 0,
    totalOtherCosts: (json['totalOtherCosts'] as num?)?.toDouble() ?? 0,
    netIncome: (json['netIncome'] as num?)?.toDouble() ?? 0,
    totalAssets: (json['totalAssets'] as num?)?.toDouble() ?? 0,
    isCurrentGameYear: json['isCurrentGameYear'] as bool? ?? true,
    primaryCurrencySymbol: json['primaryCurrencySymbol'] as String?,
    hasMixedCurrencies: json['hasMixedCurrencies'] as bool? ?? false,
    totalGovernmentContractRevenue: (json['totalGovernmentContractRevenue'] as num?)?.toDouble() ?? 0,
    totalMediaHouseIncome: (json['totalMediaHouseIncome'] as num?)?.toDouble() ?? 0,
    totalRentIncome: (json['totalRentIncome'] as num?)?.toDouble() ?? 0,
    totalPropertyMaintenance: (json['totalPropertyMaintenance'] as num?)?.toDouble() ?? 0,
    taxableIncome: (json['taxableIncome'] as num?)?.toDouble() ?? 0,
    estimatedIncomeTax: (json['estimatedIncomeTax'] as num?)?.toDouble() ?? 0,
    totalDepositInterestReceived: (json['totalDepositInterestReceived'] as num?)?.toDouble() ?? 0,
    totalDepositInterestPaid: (json['totalDepositInterestPaid'] as num?)?.toDouble() ?? 0,
    totalLoanInterestIncome: (json['totalLoanInterestIncome'] as num?)?.toDouble() ?? 0,
    totalLoanInterestExpense: (json['totalLoanInterestExpense'] as num?)?.toDouble() ?? 0,
    propertyValue: (json['propertyValue'] as num?)?.toDouble() ?? 0,
    propertyAppreciation: (json['propertyAppreciation'] as num?)?.toDouble() ?? 0,
    buildingValue: (json['buildingValue'] as num?)?.toDouble() ?? 0,
    inventoryValue: (json['inventoryValue'] as num?)?.toDouble() ?? 0,
    totalDepositsPlaced: (json['totalDepositsPlaced'] as num?)?.toDouble() ?? 0,
    totalPropertyPurchases: (json['totalPropertyPurchases'] as num?)?.toDouble() ?? 0,
    totalStockPurchaseCashOut: (json['totalStockPurchaseCashOut'] as num?)?.toDouble() ?? 0,
    totalStockSaleCashIn: (json['totalStockSaleCashIn'] as num?)?.toDouble() ?? 0,
    cashFromOperations: (json['cashFromOperations'] as num?)?.toDouble() ?? 0,
    cashFromInvestments: (json['cashFromInvestments'] as num?)?.toDouble() ?? 0,
    cashFromBanking: (json['cashFromBanking'] as num?)?.toDouble() ?? 0,
    firstRecordedTick: (json['firstRecordedTick'] as num?)?.toInt() ?? 0,
    lastRecordedTick: (json['lastRecordedTick'] as num?)?.toInt() ?? 0,
    incomeTaxDueAtTick: (json['incomeTaxDueAtTick'] as num?)?.toInt() ?? 0,
    incomeTaxDueGameTimeUtc: json['incomeTaxDueGameTimeUtc'] as String?,
    incomeTaxDueGameYear: (json['incomeTaxDueGameYear'] as num?)?.toInt() ?? 0,
    isIncomeTaxSettled: json['isIncomeTaxSettled'] as bool? ?? false,
    buildingSummaries: ((json['buildingSummaries'] as List<dynamic>?) ?? const [])
        .map((e) => BuildingLedgerSummary.fromJson(e as Map<String, dynamic>))
        .toList(),
    history: ((json['history'] as List<dynamic>?) ?? const [])
        .map((e) => CompanyLedgerHistoryYear.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

class BuildingLedgerSummary {
  const BuildingLedgerSummary({
    required this.buildingId,
    required this.buildingName,
    required this.buildingType,
    required this.revenue,
    required this.costs,
    required this.currencyCode,
  });

  final String buildingId;
  final String buildingName;
  final String buildingType;
  final double revenue;
  final double costs;
  final String currencyCode;

  factory BuildingLedgerSummary.fromJson(Map<String, dynamic> json) => BuildingLedgerSummary(
    buildingId: json['buildingId'] as String,
    buildingName: (json['buildingName'] as String?) ?? '',
    buildingType: (json['buildingType'] as String?) ?? '',
    revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
    costs: (json['costs'] as num?)?.toDouble() ?? 0,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
  );
}

class CompanyLedgerHistoryYear {
  const CompanyLedgerHistoryYear({
    required this.gameYear,
    required this.isCurrentGameYear,
    required this.netIncome,
    required this.firstRecordedTick,
    required this.lastRecordedTick,
  });

  final int gameYear;
  final bool isCurrentGameYear;
  final double netIncome;
  final int firstRecordedTick;
  final int lastRecordedTick;

  factory CompanyLedgerHistoryYear.fromJson(Map<String, dynamic> json) => CompanyLedgerHistoryYear(
    gameYear: (json['gameYear'] as num?)?.toInt() ?? 0,
    isCurrentGameYear: json['isCurrentGameYear'] as bool? ?? false,
    netIncome: (json['netIncome'] as num?)?.toDouble() ?? 0,
    firstRecordedTick: (json['firstRecordedTick'] as num?)?.toInt() ?? 0,
    lastRecordedTick: (json['lastRecordedTick'] as num?)?.toInt() ?? 0,
  );
}

class CompanyContractCard {
  const CompanyContractCard({
    required this.id,
    required this.title,
    required this.productName,
    required this.quantityRequired,
    required this.status,
    required this.fulfilledQuantity,
    required this.fulfillmentPercent,
  });

  final String id;
  final String title;
  final String productName;
  final double quantityRequired;

  /// `OPEN`, `AWARDED`, `FULFILLED`, or `EXPIRED`.
  final String status;
  final double? fulfilledQuantity;
  final double? fulfillmentPercent;

  factory CompanyContractCard.fromJson(Map<String, dynamic> json) => CompanyContractCard(
    id: json['id'] as String,
    title: (json['title'] as String?) ?? '',
    productName: (json['productName'] as String?) ?? '',
    quantityRequired: (json['quantityRequired'] as num?)?.toDouble() ?? 0,
    status: (json['status'] as String?) ?? 'OPEN',
    fulfilledQuantity: (json['fulfilledQuantity'] as num?)?.toDouble(),
    fulfillmentPercent: (json['fulfillmentPercent'] as num?)?.toDouble(),
  );
}

class ContractBid {
  const ContractBid({required this.id, required this.contractId, required this.bidPricePerUnit, required this.contractStatus});

  final String id;
  final String contractId;
  final double bidPricePerUnit;
  final String contractStatus;

  factory ContractBid.fromJson(Map<String, dynamic> json) => ContractBid(
    id: json['id'] as String,
    contractId: json['contractId'] as String,
    bidPricePerUnit: (json['bidPricePerUnit'] as num?)?.toDouble() ?? 0,
    contractStatus: (json['contractStatus'] as String?) ?? 'OPEN',
  );
}

class CitySalarySetting {
  const CitySalarySetting({
    required this.cityId,
    required this.cityName,
    required this.currencyCode,
    required this.baseSalaryPerManhour,
    required this.salaryMultiplier,
  });

  final String cityId;
  final String cityName;
  final String currencyCode;
  final double baseSalaryPerManhour;
  final double salaryMultiplier;

  factory CitySalarySetting.fromJson(Map<String, dynamic> json) => CitySalarySetting(
    cityId: json['cityId'] as String,
    cityName: (json['cityName'] as String?) ?? '',
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    baseSalaryPerManhour: (json['baseSalaryPerManhour'] as num?)?.toDouble() ?? 0,
    salaryMultiplier: (json['salaryMultiplier'] as num?)?.toDouble() ?? 1,
  );
}

class PendingDividendProposal {
  const PendingDividendProposal({
    required this.id,
    required this.dividendPercent,
    required this.ticksRemaining,
    required this.forVotes,
    required this.againstVotes,
    required this.myVoteChoice,
    this.votingCloseTick = 0,
  });

  final String id;
  final double dividendPercent;
  final int ticksRemaining;
  final int forVotes;
  final int againstVotes;
  final String? myVoteChoice;
  final int votingCloseTick;

  int get totalVotes => forVotes + againstVotes;

  /// Matches `pendingDividendApprovePct` in
  /// `projects/frontend/src/views/CompanySettingsView.vue`.
  int get approvePercent => totalVotes <= 0 ? 0 : ((forVotes / totalVotes) * 100).round();

  int get rejectPercent => totalVotes <= 0 ? 0 : (100 - approvePercent).clamp(0, 100);

  factory PendingDividendProposal.fromJson(Map<String, dynamic> json) => PendingDividendProposal(
    id: json['id'] as String,
    dividendPercent: (json['dividendPercent'] as num?)?.toDouble() ?? 0,
    ticksRemaining: (json['ticksRemaining'] as num?)?.toInt() ?? 0,
    forVotes: (json['forVotes'] as num?)?.toInt() ?? 0,
    againstVotes: (json['againstVotes'] as num?)?.toInt() ?? 0,
    myVoteChoice: json['myVoteChoice'] as String?,
    votingCloseTick: (json['votingCloseTick'] as num?)?.toInt() ?? 0,
  );
}

class CompanySettings {
  const CompanySettings({
    required this.companyName,
    required this.dividendPayoutRatio,
    required this.administrationOverheadRate,
    required this.ageFactor,
    required this.assetFactor,
    required this.citySalarySettings,
    required this.pendingDividendProposal,
  });

  final String companyName;
  final double dividendPayoutRatio;
  final double administrationOverheadRate;
  final double ageFactor;
  final double assetFactor;
  final List<CitySalarySetting> citySalarySettings;
  final PendingDividendProposal? pendingDividendProposal;

  factory CompanySettings.fromJson(Map<String, dynamic> json) => CompanySettings(
    companyName: (json['companyName'] as String?) ?? '',
    dividendPayoutRatio: (json['dividendPayoutRatio'] as num?)?.toDouble() ?? 0,
    administrationOverheadRate: (json['administrationOverheadRate'] as num?)?.toDouble() ?? 0,
    ageFactor: (json['ageFactor'] as num?)?.toDouble() ?? 1,
    assetFactor: (json['assetFactor'] as num?)?.toDouble() ?? 1,
    citySalarySettings: ((json['citySalarySettings'] as List<dynamic>?) ?? const [])
        .map((e) => CitySalarySetting.fromJson(e as Map<String, dynamic>))
        .toList(),
    pendingDividendProposal: json['pendingDividendProposal'] == null
        ? null
        : PendingDividendProposal.fromJson(json['pendingDividendProposal'] as Map<String, dynamic>),
  );
}

class CompanyBrand {
  const CompanyBrand({
    required this.id,
    required this.name,
    required this.productName,
    required this.quality,
    required this.marketingQuality,
    required this.combinedBrandQuality,
    required this.accumulatedResearchBudget,
  });

  final String id;
  final String name;
  final String? productName;
  final double quality;
  final double marketingQuality;
  final double combinedBrandQuality;
  final double accumulatedResearchBudget;

  factory CompanyBrand.fromJson(Map<String, dynamic> json) => CompanyBrand(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    productName: json['productName'] as String?,
    quality: (json['quality'] as num?)?.toDouble() ?? 0,
    marketingQuality: (json['marketingQuality'] as num?)?.toDouble() ?? 0,
    combinedBrandQuality: (json['combinedBrandQuality'] as num?)?.toDouble() ?? 0,
    accumulatedResearchBudget: (json['accumulatedResearchBudget'] as num?)?.toDouble() ?? 0,
  );
}

class BrandQualityOverview {
  const BrandQualityOverview({required this.totalResearchBudgetUsd, required this.brands});

  final double totalResearchBudgetUsd;
  final List<CompanyBrand> brands;

  factory BrandQualityOverview.fromJson(Map<String, dynamic> json) => BrandQualityOverview(
    totalResearchBudgetUsd: (json['totalResearchBudgetUsd'] as num?)?.toDouble() ?? 0,
    brands: ((json['brands'] as List<dynamic>?) ?? const []).map((e) => CompanyBrand.fromJson(e as Map<String, dynamic>)).toList(),
  );
}
