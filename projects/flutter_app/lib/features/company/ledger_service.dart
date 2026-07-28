// GraphQL calls for the Ledger screen, matching the exact field/argument
// names `LedgerView.vue`'s `LEDGER_QUERY`/`DRILL_QUERY` use.

import '../../core/graphql/graphql_service.dart';
import '../trade/trade_models.dart';
import 'company_models.dart';
import 'ledger_models.dart';

const _ledgerPageQuery = r'''
  query GetCompanyLedger($companyId: UUID!, $gameYear: Int) {
    gameState {
      currentTick
    }
    companyLedger(companyId: $companyId, gameYear: $gameYear) {
      companyId companyName gameYear isCurrentGameYear currentCash
      primaryCurrencyCode primaryCurrencySymbol hasMixedCurrencies
      totalRevenue totalGovernmentContractRevenue totalMediaHouseIncome totalRentIncome totalPropertyMaintenance
      totalPurchasingCosts totalShippingCosts totalLaborCosts totalEnergyCosts totalMarketingCosts totalTaxPaid totalOtherCosts
      taxableIncome estimatedIncomeTax netIncome
      totalDepositInterestReceived totalDepositInterestPaid totalLoanInterestIncome totalLoanInterestExpense
      propertyValue propertyAppreciation buildingValue inventoryValue totalDepositsPlaced totalAssets totalPropertyPurchases
      totalStockPurchaseCashOut totalStockSaleCashIn cashFromOperations cashFromInvestments cashFromBanking firstRecordedTick lastRecordedTick
      incomeTaxDueAtTick incomeTaxDueGameTimeUtc incomeTaxDueGameYear isIncomeTaxSettled
      history {
        gameYear isCurrentGameYear netIncome firstRecordedTick lastRecordedTick
      }
      buildingSummaries { buildingId buildingName buildingType revenue costs currencyCode }
    }
    companyCityFinancialBreakdown(companyId: $companyId, gameYear: $gameYear) {
      cityId cityName currencyCode revenue costs profit
      revenueTrend { tick revenue }
    }
    logisticsShipments: getCrossCityShipments(companyId: $companyId) {
      id sourceCityName destinationCityName sourceBuildingName destinationBuildingName
      productTypeName resourceTypeName quantity expectedArrivalTick scheduledDepartureTick transitTicks
      status failureReason
    }
    cityUnlockStatuses(companyId: $companyId) {
      cityId cityName countryCode isUnlocked requiredNetWorth currentNetWorth currency progressPercent estimatedTicksToUnlock
    }
  }
''';

const _ledgerDrillDownQuery = r'''
  query GetLedgerDrillDown($companyId: UUID!, $category: String!, $gameYear: Int) {
    ledgerDrillDown(companyId: $companyId, category: $category, gameYear: $gameYear) {
      id category description amount recordedAtTick
      buildingId buildingName buildingType
      productName resourceName
      currencyCode
      eventTag eventDescription
    }
  }
''';

class LedgerPageData {
  const LedgerPageData({
    required this.ledger,
    required this.cityFinancialBreakdown,
    required this.logisticsShipments,
    required this.cityUnlockStatuses,
    required this.currentTick,
  });

  final CompanyLedger? ledger;
  final List<CityFinancialBreakdown> cityFinancialBreakdown;
  final List<TradeRoute> logisticsShipments;
  final List<CityUnlockStatus> cityUnlockStatuses;
  final int? currentTick;
}

class LedgerService {
  const LedgerService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<LedgerPageData> fetchLedgerPage(String companyId, {int? gameYear}) async {
    final result = await _graphQlService.request(_ledgerPageQuery, variables: {'companyId': companyId, 'gameYear': gameYear});
    final ledgerJson = result['companyLedger'] as Map<String, dynamic>?;
    final gameStateJson = result['gameState'] as Map<String, dynamic>?;
    return LedgerPageData(
      ledger: ledgerJson == null ? null : CompanyLedger.fromJson(ledgerJson),
      cityFinancialBreakdown: ((result['companyCityFinancialBreakdown'] as List<dynamic>?) ?? const [])
          .map((e) => CityFinancialBreakdown.fromJson(e as Map<String, dynamic>))
          .toList(),
      logisticsShipments: ((result['logisticsShipments'] as List<dynamic>?) ?? const [])
          .map((e) => TradeRoute.fromJson(e as Map<String, dynamic>))
          .toList(),
      cityUnlockStatuses: ((result['cityUnlockStatuses'] as List<dynamic>?) ?? const [])
          .map((e) => CityUnlockStatus.fromJson(e as Map<String, dynamic>))
          .toList(),
      currentTick: (gameStateJson?['currentTick'] as num?)?.toInt(),
    );
  }

  Future<List<LedgerEntryResult>> fetchDrillDown(String companyId, {required String category, int? gameYear}) async {
    final result = await _graphQlService.request(
      _ledgerDrillDownQuery,
      variables: {'companyId': companyId, 'category': category, 'gameYear': gameYear},
    );
    return ((result['ledgerDrillDown'] as List<dynamic>?) ?? const [])
        .map((e) => LedgerEntryResult.fromJson(e as Map<String, dynamic>))
        .toList();
  }
}
