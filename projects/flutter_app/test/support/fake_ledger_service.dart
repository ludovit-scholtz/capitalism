import 'package:capitalism_app/features/company/ledger_models.dart';
import 'package:capitalism_app/features/company/ledger_service.dart';

class FakeLedgerService implements LedgerService {
  FakeLedgerService({this.page, this.pageError, this.drillEntries = const [], this.drillError});

  final LedgerPageData? page;
  final Object? pageError;
  final List<LedgerEntryResult> drillEntries;
  final Object? drillError;

  final List<String> calls = [];
  int? lastRequestedGameYear;
  String? lastDrillCategory;

  @override
  Future<LedgerPageData> fetchLedgerPage(String companyId, {int? gameYear}) async {
    calls.add('fetchLedgerPage');
    lastRequestedGameYear = gameYear;
    if (pageError != null) throw pageError!;
    return page ??
        const LedgerPageData(ledger: null, cityFinancialBreakdown: [], logisticsShipments: [], cityUnlockStatuses: [], currentTick: null);
  }

  @override
  Future<List<LedgerEntryResult>> fetchDrillDown(String companyId, {required String category, int? gameYear}) async {
    calls.add('fetchDrillDown');
    lastDrillCategory = category;
    if (drillError != null) throw drillError!;
    return drillEntries;
  }
}
