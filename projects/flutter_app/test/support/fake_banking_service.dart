import 'package:capitalism_app/features/banking/banking_models.dart';
import 'package:capitalism_app/features/banking/banking_service.dart';

class FakeBankingService implements BankingService {
  FakeBankingService({
    this.myLoans = const [],
    this.myCompanies = const [],
    this.allBanks = const [],
    this.myDeposits = const [],
    this.myBankAccounts = const [],
    this.bankInfo,
    this.bankLoans = const [],
    this.bankDeposits = const [],
    this.collateralBuildings = const [],
    this.companyBankAccounts = const [],
    this.bankStatement,
    this.rateHistory = const [],
    this.loadError,
    this.actionError,
  });

  final List<LoanSummary> myLoans;
  final List<Map<String, String>> myCompanies;
  final List<BankListing> allBanks;
  final List<BankDeposit> myDeposits;
  final List<PlayerBankAccount> myBankAccounts;
  final BankInfo? bankInfo;
  final List<LoanSummary> bankLoans;
  final List<BankDeposit> bankDeposits;
  final List<CollateralBuilding> collateralBuildings;
  final List<Map<String, String>> companyBankAccounts;
  final BankStatementResult? bankStatement;
  final List<BankDepositRateHistoryEntry> rateHistory;
  final Object? loadError;
  final Object? actionError;

  final List<String> calls = [];
  Map<String, dynamic>? lastOpenAccountArgs;
  String? closedAccountId;
  double? lastCloseAmount;
  Map<String, dynamic>? lastSetRatesArgs;
  bool baseDepositActivated = false;
  Map<String, dynamic>? lastAcceptLoanArgs;
  String? lastRepaidLoanId;
  Map<String, dynamic>? lastUpdateDepositRateArgs;
  Map<String, dynamic>? lastBankStatementArgs;

  @override
  Future<List<LoanSummary>> fetchMyLoans() async {
    calls.add('fetchMyLoans');
    if (loadError != null) throw loadError!;
    return myLoans;
  }

  @override
  Future<List<Map<String, String>>> fetchMyCompanies() async {
    calls.add('fetchMyCompanies');
    return myCompanies;
  }

  @override
  Future<List<BankListing>> fetchAllBanks() async {
    calls.add('fetchAllBanks');
    if (loadError != null) throw loadError!;
    return allBanks;
  }

  @override
  Future<List<BankDeposit>> fetchMyDeposits() async {
    calls.add('fetchMyDeposits');
    return myDeposits;
  }

  @override
  Future<List<PlayerBankAccount>> fetchMyBankAccounts() async {
    calls.add('fetchMyBankAccounts');
    return myBankAccounts;
  }

  @override
  Future<void> openBankAccount({required String bankBuildingId, String? depositorCompanyId, required double amount}) async {
    calls.add('openBankAccount');
    if (actionError != null) throw actionError!;
    lastOpenAccountArgs = {'bankBuildingId': bankBuildingId, 'amount': amount};
  }

  @override
  Future<void> closeBankAccount(String depositId, {double amount = 0}) async {
    calls.add('closeBankAccount');
    if (actionError != null) throw actionError!;
    closedAccountId = depositId;
    lastCloseAmount = amount;
  }

  @override
  Future<void> closeCompanyBankAccount(String bankAccountId) async {
    calls.add('closeCompanyBankAccount');
    closedAccountId = bankAccountId;
  }

  @override
  Future<BankInfo> fetchBankInfo(String bankBuildingId) async {
    calls.add('fetchBankInfo');
    if (loadError != null) throw loadError!;
    return bankInfo!;
  }

  @override
  Future<List<LoanSummary>> fetchBankLoans(String bankBuildingId) async {
    calls.add('fetchBankLoans');
    return bankLoans;
  }

  @override
  Future<List<BankDeposit>> fetchBankDeposits(String bankBuildingId) async {
    calls.add('fetchBankDeposits');
    return bankDeposits;
  }

  @override
  Future<void> setBankRates({required String bankBuildingId, required double depositRate, required double lendingRate}) async {
    calls.add('setBankRates');
    if (actionError != null) throw actionError!;
    lastSetRatesArgs = {'depositRate': depositRate, 'lendingRate': lendingRate};
  }

  @override
  Future<void> initiateBaseDeposit(String bankBuildingId) async {
    calls.add('initiateBaseDeposit');
    if (actionError != null) throw actionError!;
    baseDepositActivated = true;
  }

  @override
  Future<List<CollateralBuilding>> fetchMyCollateralBuildings({String? bankBuildingId}) async {
    calls.add('fetchMyCollateralBuildings');
    return collateralBuildings;
  }

  @override
  Future<List<Map<String, String>>> fetchCompanyBankAccounts(String companyId) async {
    calls.add('fetchCompanyBankAccounts');
    return companyBankAccounts;
  }

  @override
  Future<void> acceptLoan({
    required String bankBuildingId,
    required String borrowerCompanyId,
    required double principalAmount,
    int? durationTicks,
    String? collateralBuildingId,
    String? bankAccountId,
  }) async {
    calls.add('acceptLoan');
    if (actionError != null) throw actionError!;
    lastAcceptLoanArgs = {
      'bankBuildingId': bankBuildingId,
      'borrowerCompanyId': borrowerCompanyId,
      'principalAmount': principalAmount,
      'durationTicks': durationTicks,
    };
  }

  @override
  Future<void> repayLoanDebt({required String loanId, String? bankAccountId}) async {
    calls.add('repayLoanDebt');
    if (actionError != null) throw actionError!;
    lastRepaidLoanId = loanId;
  }

  @override
  Future<void> updateBankDepositRate({required String bankBuildingId, required double newRatePercent}) async {
    calls.add('updateBankDepositRate');
    if (actionError != null) throw actionError!;
    lastUpdateDepositRateArgs = {'bankBuildingId': bankBuildingId, 'newRatePercent': newRatePercent};
  }

  @override
  Future<List<BankDepositRateHistoryEntry>> fetchBankDepositRateHistory(String bankBuildingId) async {
    calls.add('fetchBankDepositRateHistory');
    return rateHistory;
  }

  @override
  Future<BankStatementResult> fetchBankStatement({
    String? companyId,
    String? accountId,
    int limit = 50,
    int offset = 0,
    int? fromTick,
    int? toTick,
  }) async {
    calls.add('fetchBankStatement');
    if (loadError != null) throw loadError!;
    lastBankStatementArgs = {
      'companyId': companyId,
      'accountId': accountId,
      'limit': limit,
      'offset': offset,
      'fromTick': fromTick,
      'toTick': toTick,
    };
    return bankStatement!;
  }
}
