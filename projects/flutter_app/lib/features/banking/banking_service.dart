import '../../core/graphql/graphql_service.dart';
import 'banking_models.dart';

const _myLoansQuery = r'''
  query MyLoans {
    myLoans {
      id bankBuildingId bankBuildingName loanCurrencyCode originalPrincipal remainingPrincipal
      annualInterestRatePercent nextPaymentTick paymentAmount paymentsMade totalPayments
      status missedPayments accumulatedPenalty defaultedAtTick
      collateralBuildingId collateralBuildingName collateralAppraisedValue collateralListingPrice collateralListingCurrencyCode
    }
  }
''';

const _myCompaniesQuery = r'''
  query BankingMyCompanies { myCompanies { id name cash buildings { id type name } } }
''';

const _allBanksQuery = r'''
  query AllBanks {
    allBanks {
      bankBuildingId bankBuildingName cityId cityName lenderCompanyId lenderCompanyName
      depositInterestRatePercent lendingInterestRatePercent totalDeposits lendableCapacity
      outstandingLoanPrincipal availableLendingCapacity baseCapitalDeposited
    }
  }
''';

const _myDepositsQuery = r'''
  query MyDeposits {
    myDeposits {
      id bankBuildingId bankBuildingName depositorCompanyId depositorCompanyName amount
      depositInterestRatePercent isBaseCapital isActive depositedAtTick depositedAtUtc totalInterestPaid
    }
  }
''';

const _myBankAccountsQuery = r'''
  query MyBankAccounts {
    myBankAccounts {
      id accountNumber currencyCode currencySymbol balance companyId companyName
      ownerType ownerDisplayName bankBuildingId cityId isDepositAccount
    }
  }
''';

const _openBankAccountMutation = r'''
  mutation OpenBankAccount($input: OpenBankAccountInput!) {
    openBankAccount(input: $input) { id amount depositInterestRatePercent isActive }
  }
''';

const _closeBankAccountMutation = r'''
  mutation CloseBankAccountById($input: CloseBankAccountInput!) {
    closeBankAccount(input: $input) { id isActive withdrawnAtUtc }
  }
''';

const _closeCompanyBankAccountMutation = r'''
  mutation CloseCompanyBankAccountById($input: CloseCompanyBankAccountInput!) {
    closeCompanyBankAccount(input: $input) { id accountNumber currencyCode closedAtUtc }
  }
''';

const _bankInfoQuery = r'''
  query BankInfo($id: UUID!) {
    bankInfo(bankBuildingId: $id) {
      bankBuildingId bankBuildingName cityId cityName cityCurrencyCode cityCurrencySymbol baseCapitalRequirement
      lenderCompanyId lenderCompanyName depositInterestRatePercent lendingInterestRatePercent totalDeposits
      lendableCapacity outstandingLoanPrincipal availableLendingCapacity baseCapitalDeposited centralBankDebt
      centralBankInterestRatePercent reserveRequirement availableCash reserveShortfall liquidityStatus
      pendingDepositInterestRatePercent pendingDepositRateEffectiveTick
    }
  }
''';

const _bankLoansQuery = r'''
  query BankLoans($bankBuildingId: UUID!) {
    bankLoans(bankBuildingId: $bankBuildingId) {
      id borrowerCompanyId borrowerCompanyName bankBuildingId bankBuildingName loanCurrencyCode
      originalPrincipal remainingPrincipal annualInterestRatePercent nextPaymentTick paymentAmount
      status missedPayments
    }
  }
''';

const _bankDepositsQuery = r'''
  query BankDeposits($id: UUID!) {
    bankDeposits(bankBuildingId: $id) { id bankBuildingId depositorCompanyName amount depositInterestRatePercent isBaseCapital isActive }
  }
''';

const _setBankRatesMutation = r'''
  mutation SetBankRates($input: SetBankRatesInput!) {
    setBankRates(input: $input) { bankBuildingId depositInterestRatePercent lendingInterestRatePercent }
  }
''';

const _initiateBaseDepositMutation = r'''
  mutation InitiateBaseDeposit($bankBuildingId: UUID!) {
    initiateBaseDeposit(bankBuildingId: $bankBuildingId) { bankBuildingId baseCapitalDeposited }
  }
''';

const _myCollateralBuildingsQuery = r'''
  query MyCollateralBuildings($bankBuildingId: UUID) {
    myCollateralBuildings(bankBuildingId: $bankBuildingId) {
      buildingId buildingName buildingType level appraisedValue maxBorrowable existingSecuredExposure
      remainingBorrowingCapacity currencyCode isEligible ineligibilityReason
    }
  }
''';

const _companyBankAccountsQuery = r'''
  query CompanyBankAccounts($companyId: UUID!) {
    companyBankAccounts(companyId: $companyId) { id accountNumber currencyCode balance }
  }
''';

const _acceptLoanMutation = r'''
  mutation AcceptLoan($input: AcceptLoanInput!) {
    acceptLoan(input: $input) { id status originalPrincipal }
  }
''';

const _repayLoanDebtMutation = r'''
  mutation RepayLoanDebt($input: RepayLoanDebtInput!) {
    repayLoanDebt(input: $input) { id status remainingPrincipal }
  }
''';

const _updateBankDepositRateMutation = r'''
  mutation UpdateBankDepositRate($input: UpdateBankDepositRateInput!) {
    updateBankDepositRate(input: $input) {
      bankBuildingId depositInterestRatePercent pendingDepositInterestRatePercent pendingDepositRateEffectiveTick
    }
  }
''';

const _bankDepositRateHistoryQuery = r'''
  query BankDepositRateHistory($bankBuildingId: UUID!) {
    bankDepositRateHistory(bankBuildingId: $bankBuildingId) {
      id previousRatePercent newRatePercent effectiveTick isApplied
    }
  }
''';

const _bankStatementQuery = r'''
  query BankStatement($companyId: UUID, $accountId: UUID, $limit: Int, $offset: Int, $fromTick: Long, $toTick: Long) {
    bankStatement(companyId: $companyId, accountId: $accountId, limit: $limit, offset: $offset, fromTick: $fromTick, toTick: $toTick) {
      companyId companyName currencyCode currencySymbol currentBalance totalEntries
      rows { id recordedAtTick recordedAtUtc description category amount runningBalance buildingId buildingName }
    }
  }
''';

/// GraphQL calls for the Loan Marketplace, Bank Management, Bank Loan
/// Request, and Bank Statement screens.
class BankingService {
  const BankingService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<LoanSummary>> fetchMyLoans() async {
    final result = await _graphQlService.request(_myLoansQuery);
    final list = result['myLoans'] as List<dynamic>? ?? const [];
    return list.map((e) => LoanSummary.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<Map<String, String>>> fetchMyCompanies() async {
    final result = await _graphQlService.request(_myCompaniesQuery);
    final companies = result['myCompanies'] as List<dynamic>? ?? const [];
    return companies
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'name': e['name'] as String})
        .toList();
  }

  Future<List<BankListing>> fetchAllBanks() async {
    final result = await _graphQlService.request(_allBanksQuery);
    final list = result['allBanks'] as List<dynamic>? ?? const [];
    return list.map((e) => BankListing.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<BankDeposit>> fetchMyDeposits() async {
    final result = await _graphQlService.request(_myDepositsQuery);
    final list = result['myDeposits'] as List<dynamic>? ?? const [];
    return list.map((e) => BankDeposit.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<PlayerBankAccount>> fetchMyBankAccounts() async {
    final result = await _graphQlService.request(_myBankAccountsQuery);
    final list = result['myBankAccounts'] as List<dynamic>? ?? const [];
    return list.map((e) => PlayerBankAccount.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> openBankAccount({required String bankBuildingId, String? depositorCompanyId, required double amount}) {
    return _graphQlService.request(
      _openBankAccountMutation,
      variables: {
        'input': {'bankBuildingId': bankBuildingId, 'depositorCompanyId': depositorCompanyId, 'amount': amount},
      },
    );
  }

  /// Pass [amount] `0` only for a zero-balance closure; otherwise pass the
  /// exact withdrawal amount (the full balance to close the account, or a
  /// smaller amount for a partial withdrawal that keeps it open) — matches
  /// `Mutation.Banking.cs`'s `CloseBankAccount` validation.
  Future<void> closeBankAccount(String depositId, {double amount = 0}) {
    return _graphQlService.request(
      _closeBankAccountMutation,
      variables: {
        'input': {'depositId': depositId, 'amount': amount},
      },
    );
  }

  Future<void> closeCompanyBankAccount(String bankAccountId) {
    return _graphQlService.request(
      _closeCompanyBankAccountMutation,
      variables: {
        'input': {'bankAccountId': bankAccountId},
      },
    );
  }

  Future<BankInfo> fetchBankInfo(String bankBuildingId) async {
    final result = await _graphQlService.request(_bankInfoQuery, variables: {'id': bankBuildingId});
    return BankInfo.fromJson(result['bankInfo'] as Map<String, dynamic>);
  }

  Future<List<LoanSummary>> fetchBankLoans(String bankBuildingId) async {
    final result = await _graphQlService.request(_bankLoansQuery, variables: {'bankBuildingId': bankBuildingId});
    final list = result['bankLoans'] as List<dynamic>? ?? const [];
    return list.map((e) => LoanSummary.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<BankDeposit>> fetchBankDeposits(String bankBuildingId) async {
    final result = await _graphQlService.request(_bankDepositsQuery, variables: {'id': bankBuildingId});
    final list = result['bankDeposits'] as List<dynamic>? ?? const [];
    return list.map((e) => BankDeposit.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> setBankRates({required String bankBuildingId, required double depositRate, required double lendingRate}) {
    return _graphQlService.request(
      _setBankRatesMutation,
      variables: {
        'input': {'bankBuildingId': bankBuildingId, 'depositInterestRatePercent': depositRate, 'lendingInterestRatePercent': lendingRate},
      },
    );
  }

  Future<void> initiateBaseDeposit(String bankBuildingId) {
    return _graphQlService.request(_initiateBaseDepositMutation, variables: {'bankBuildingId': bankBuildingId});
  }

  Future<List<CollateralBuilding>> fetchMyCollateralBuildings({String? bankBuildingId}) async {
    final result = await _graphQlService.request(_myCollateralBuildingsQuery, variables: {'bankBuildingId': bankBuildingId});
    final list = result['myCollateralBuildings'] as List<dynamic>? ?? const [];
    return list.map((e) => CollateralBuilding.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<Map<String, String>>> fetchCompanyBankAccounts(String companyId) async {
    final result = await _graphQlService.request(_companyBankAccountsQuery, variables: {'companyId': companyId});
    final list = result['companyBankAccounts'] as List<dynamic>? ?? const [];
    return list
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'currencyCode': e['currencyCode'] as String})
        .toList();
  }

  Future<void> acceptLoan({
    required String bankBuildingId,
    required String borrowerCompanyId,
    required double principalAmount,
    int? durationTicks,
    String? collateralBuildingId,
    String? bankAccountId,
  }) {
    return _graphQlService.request(
      _acceptLoanMutation,
      variables: {
        'input': {
          'loanOfferId': bankBuildingId,
          'borrowerCompanyId': borrowerCompanyId,
          'principalAmount': principalAmount,
          'durationTicks': durationTicks,
          'collateralBuildingId': collateralBuildingId,
          'bankAccountId': bankAccountId,
        },
      },
    );
  }

  Future<void> repayLoanDebt({required String loanId, String? bankAccountId}) {
    return _graphQlService.request(
      _repayLoanDebtMutation,
      variables: {
        'input': {'loanId': loanId, 'bankAccountId': bankAccountId},
      },
    );
  }

  Future<void> updateBankDepositRate({required String bankBuildingId, required double newRatePercent}) {
    return _graphQlService.request(
      _updateBankDepositRateMutation,
      variables: {
        'input': {'bankBuildingId': bankBuildingId, 'newRatePercent': newRatePercent},
      },
    );
  }

  Future<List<BankDepositRateHistoryEntry>> fetchBankDepositRateHistory(String bankBuildingId) async {
    final result = await _graphQlService.request(_bankDepositRateHistoryQuery, variables: {'bankBuildingId': bankBuildingId});
    final list = result['bankDepositRateHistory'] as List<dynamic>? ?? const [];
    return list.map((e) => BankDepositRateHistoryEntry.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<BankStatementResult> fetchBankStatement({
    String? companyId,
    String? accountId,
    int limit = 50,
    int offset = 0,
    int? fromTick,
    int? toTick,
  }) async {
    final result = await _graphQlService.request(
      _bankStatementQuery,
      variables: {
        'companyId': companyId,
        'accountId': accountId,
        'limit': limit,
        'offset': offset,
        'fromTick': fromTick,
        'toTick': toTick,
      },
    );
    return BankStatementResult.fromJson(result['bankStatement'] as Map<String, dynamic>);
  }
}
