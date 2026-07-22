// Data models for the Loan Marketplace, Bank Management, Bank Loan
// Request, and Bank Statement screens, mirroring
// `projects/frontend/src/views/LoanMarketplaceView.vue`/
// `BankManagementView.vue`/`BankLoanRequestView.vue`/`BankStatementView.vue`
// and `projects/frontend/src/lib/bankManagementQueries.ts`. GraphQL field
// names verified against `Api/Types/Inputs.Banking.cs`/`Inputs.Lending.cs`.

class LoanSummary {
  const LoanSummary({
    required this.id,
    required this.bankBuildingId,
    required this.bankBuildingName,
    required this.loanCurrencyCode,
    required this.originalPrincipal,
    required this.remainingPrincipal,
    required this.annualInterestRatePercent,
    required this.nextPaymentTick,
    required this.paymentAmount,
    required this.status,
    required this.missedPayments,
  });

  final String id;
  final String bankBuildingId;
  final String bankBuildingName;
  final String loanCurrencyCode;
  final double originalPrincipal;
  final double remainingPrincipal;
  final double annualInterestRatePercent;
  final int nextPaymentTick;
  final double paymentAmount;

  /// `ACTIVE`, `PAID_OFF`, or `DEFAULTED`.
  final String status;
  final int missedPayments;

  factory LoanSummary.fromJson(Map<String, dynamic> json) => LoanSummary(
    id: json['id'] as String,
    bankBuildingId: (json['bankBuildingId'] as String?) ?? '',
    bankBuildingName: (json['bankBuildingName'] as String?) ?? '',
    loanCurrencyCode: (json['loanCurrencyCode'] as String?) ?? 'EUR',
    originalPrincipal: (json['originalPrincipal'] as num?)?.toDouble() ?? 0,
    remainingPrincipal: (json['remainingPrincipal'] as num?)?.toDouble() ?? 0,
    annualInterestRatePercent: (json['annualInterestRatePercent'] as num?)?.toDouble() ?? 0,
    nextPaymentTick: (json['nextPaymentTick'] as num?)?.toInt() ?? 0,
    paymentAmount: (json['paymentAmount'] as num?)?.toDouble() ?? 0,
    status: (json['status'] as String?) ?? 'ACTIVE',
    missedPayments: (json['missedPayments'] as num?)?.toInt() ?? 0,
  );
}

class BankListing {
  const BankListing({
    required this.bankBuildingId,
    required this.bankBuildingName,
    required this.cityName,
    required this.depositInterestRatePercent,
    required this.lendingInterestRatePercent,
    required this.availableLendingCapacity,
    required this.baseCapitalDeposited,
    required this.lenderCompanyId,
  });

  final String bankBuildingId;
  final String bankBuildingName;
  final String cityName;
  final double depositInterestRatePercent;
  final double lendingInterestRatePercent;
  final double availableLendingCapacity;
  final bool baseCapitalDeposited;
  final String? lenderCompanyId;

  factory BankListing.fromJson(Map<String, dynamic> json) => BankListing(
    bankBuildingId: json['bankBuildingId'] as String,
    bankBuildingName: (json['bankBuildingName'] as String?) ?? '',
    cityName: (json['cityName'] as String?) ?? '',
    depositInterestRatePercent: (json['depositInterestRatePercent'] as num?)?.toDouble() ?? 0,
    lendingInterestRatePercent: (json['lendingInterestRatePercent'] as num?)?.toDouble() ?? 0,
    availableLendingCapacity: (json['availableLendingCapacity'] as num?)?.toDouble() ?? 0,
    baseCapitalDeposited: json['baseCapitalDeposited'] as bool? ?? false,
    lenderCompanyId: json['lenderCompanyId'] as String?,
  );
}

class PlayerBankAccount {
  const PlayerBankAccount({
    required this.id,
    required this.accountNumber,
    required this.currencyCode,
    required this.balance,
    required this.companyName,
    required this.bankBuildingId,
    required this.isDepositAccount,
  });

  final String id;
  final String? accountNumber;
  final String currencyCode;
  final double balance;
  final String? companyName;
  final String? bankBuildingId;
  final bool isDepositAccount;

  factory PlayerBankAccount.fromJson(Map<String, dynamic> json) => PlayerBankAccount(
    id: json['id'] as String,
    accountNumber: json['accountNumber'] as String?,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    balance: (json['balance'] as num?)?.toDouble() ?? 0,
    companyName: json['companyName'] as String?,
    bankBuildingId: json['bankBuildingId'] as String?,
    isDepositAccount: json['isDepositAccount'] as bool? ?? false,
  );
}

class BankDeposit {
  const BankDeposit({required this.id, required this.amount, required this.depositInterestRatePercent, required this.isBaseCapital});

  final String id;
  final double amount;
  final double depositInterestRatePercent;
  final bool isBaseCapital;

  factory BankDeposit.fromJson(Map<String, dynamic> json) => BankDeposit(
    id: json['id'] as String,
    amount: (json['amount'] as num?)?.toDouble() ?? 0,
    depositInterestRatePercent: (json['depositInterestRatePercent'] as num?)?.toDouble() ?? 0,
    isBaseCapital: json['isBaseCapital'] as bool? ?? false,
  );
}

class BankInfo {
  const BankInfo({
    required this.bankBuildingId,
    required this.bankBuildingName,
    required this.cityCurrencyCode,
    required this.lenderCompanyId,
    required this.depositInterestRatePercent,
    required this.lendingInterestRatePercent,
    required this.totalDeposits,
    required this.availableLendingCapacity,
    required this.baseCapitalDeposited,
    required this.baseCapitalRequirement,
    required this.liquidityStatus,
  });

  final String bankBuildingId;
  final String bankBuildingName;
  final String cityCurrencyCode;
  final String? lenderCompanyId;
  final double depositInterestRatePercent;
  final double lendingInterestRatePercent;
  final double totalDeposits;
  final double availableLendingCapacity;
  final bool baseCapitalDeposited;
  final double baseCapitalRequirement;
  final String? liquidityStatus;

  factory BankInfo.fromJson(Map<String, dynamic> json) => BankInfo(
    bankBuildingId: json['bankBuildingId'] as String,
    bankBuildingName: (json['bankBuildingName'] as String?) ?? '',
    cityCurrencyCode: (json['cityCurrencyCode'] as String?) ?? 'EUR',
    lenderCompanyId: json['lenderCompanyId'] as String?,
    depositInterestRatePercent: (json['depositInterestRatePercent'] as num?)?.toDouble() ?? 0,
    lendingInterestRatePercent: (json['lendingInterestRatePercent'] as num?)?.toDouble() ?? 0,
    totalDeposits: (json['totalDeposits'] as num?)?.toDouble() ?? 0,
    availableLendingCapacity: (json['availableLendingCapacity'] as num?)?.toDouble() ?? 0,
    baseCapitalDeposited: json['baseCapitalDeposited'] as bool? ?? false,
    baseCapitalRequirement: (json['baseCapitalRequirement'] as num?)?.toDouble() ?? 0,
    liquidityStatus: json['liquidityStatus'] as String?,
  );
}

class CollateralBuilding {
  const CollateralBuilding({
    required this.buildingId,
    required this.buildingName,
    required this.remainingBorrowingCapacity,
    required this.currencyCode,
    required this.isEligible,
    required this.ineligibilityReason,
  });

  final String buildingId;
  final String buildingName;
  final double remainingBorrowingCapacity;
  final String currencyCode;
  final bool isEligible;
  final String? ineligibilityReason;

  factory CollateralBuilding.fromJson(Map<String, dynamic> json) => CollateralBuilding(
    buildingId: json['buildingId'] as String,
    buildingName: (json['buildingName'] as String?) ?? '',
    remainingBorrowingCapacity: (json['remainingBorrowingCapacity'] as num?)?.toDouble() ?? 0,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    isEligible: json['isEligible'] as bool? ?? false,
    ineligibilityReason: json['ineligibilityReason'] as String?,
  );
}

class BankStatementRow {
  const BankStatementRow({required this.id, required this.description, required this.category, required this.amount, required this.runningBalance});

  final String id;
  final String? description;
  final String? category;
  final double amount;
  final double runningBalance;

  factory BankStatementRow.fromJson(Map<String, dynamic> json) => BankStatementRow(
    id: json['id'] as String,
    description: json['description'] as String?,
    category: json['category'] as String?,
    amount: (json['amount'] as num?)?.toDouble() ?? 0,
    runningBalance: (json['runningBalance'] as num?)?.toDouble() ?? 0,
  );
}

class BankStatementResult {
  const BankStatementResult({
    required this.companyName,
    required this.currencyCode,
    required this.currentBalance,
    required this.totalEntries,
    required this.rows,
  });

  final String companyName;
  final String currencyCode;
  final double currentBalance;
  final int totalEntries;
  final List<BankStatementRow> rows;

  factory BankStatementResult.fromJson(Map<String, dynamic> json) => BankStatementResult(
    companyName: (json['companyName'] as String?) ?? '',
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    currentBalance: (json['currentBalance'] as num?)?.toDouble() ?? 0,
    totalEntries: (json['totalEntries'] as num?)?.toInt() ?? 0,
    rows: ((json['rows'] as List<dynamic>?) ?? const []).map((e) => BankStatementRow.fromJson(e as Map<String, dynamic>)).toList(),
  );
}
