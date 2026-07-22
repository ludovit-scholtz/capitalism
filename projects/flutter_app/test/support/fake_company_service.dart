import 'package:capitalism_app/features/company/company_models.dart';
import 'package:capitalism_app/features/company/company_service.dart';

class FakeCompanyService implements CompanyService {
  FakeCompanyService({
    this.ledger,
    this.cityBreakdown = const [],
    this.contracts = const [],
    this.bids = const [],
    this.settings,
    this.brandOverview,
    this.ledgerError,
    this.contractsError,
    this.settingsError,
    this.researchError,
    this.actionError,
  });

  final CompanyLedger? ledger;
  final List<CityFinancialBreakdown> cityBreakdown;
  final List<CompanyContractCard> contracts;
  final List<ContractBid> bids;
  final CompanySettings? settings;
  final BrandQualityOverview? brandOverview;
  final Object? ledgerError;
  final Object? contractsError;
  final Object? settingsError;
  final Object? researchError;
  final Object? actionError;

  final List<String> calls = [];
  Map<String, dynamic>? lastFulfillArgs;
  Map<String, dynamic>? lastUpdateSettingsArgs;
  bool? lastVoteApprove;

  @override
  Future<(CompanyLedger, List<CityFinancialBreakdown>)> fetchLedger(String companyId, {int? gameYear}) async {
    calls.add('fetchLedger');
    if (ledgerError != null) throw ledgerError!;
    return (ledger!, cityBreakdown);
  }

  @override
  Future<(List<CompanyContractCard>, List<ContractBid>)> fetchCompanyContracts(String companyId) async {
    calls.add('fetchCompanyContracts');
    if (contractsError != null) throw contractsError!;
    return (contracts, bids);
  }

  @override
  Future<void> fulfillShipment({required String contractId, required double quantity}) async {
    calls.add('fulfillShipment');
    if (actionError != null) throw actionError!;
    lastFulfillArgs = {'contractId': contractId, 'quantity': quantity};
  }

  @override
  Future<CompanySettings> fetchCompanySettings(String companyId) async {
    calls.add('fetchCompanySettings');
    if (settingsError != null) throw settingsError!;
    return settings!;
  }

  @override
  Future<void> updateCompanySettings({
    required String companyId,
    required String name,
    required double dividendPayoutRatio,
    required List<Map<String, dynamic>> citySalarySettings,
  }) async {
    calls.add('updateCompanySettings');
    if (actionError != null) throw actionError!;
    lastUpdateSettingsArgs = {'name': name, 'dividendPayoutRatio': dividendPayoutRatio};
  }

  @override
  Future<void> proposeDividend({required String companyId, required double dividendPercent}) async {
    calls.add('proposeDividend');
  }

  @override
  Future<void> voteDividend({required String companyId, required bool approve}) async {
    calls.add('voteDividend');
    if (actionError != null) throw actionError!;
    lastVoteApprove = approve;
  }

  @override
  Future<BrandQualityOverview> fetchBrandQualityOverview(String companyId) async {
    calls.add('fetchBrandQualityOverview');
    if (researchError != null) throw researchError!;
    return brandOverview!;
  }
}
