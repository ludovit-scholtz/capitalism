import '../../core/graphql/graphql_service.dart';
import 'company_models.dart';

const _companyContractsQuery = r'''
  query CompanyContracts($companyId: UUID!) {
    companyContracts(companyId: $companyId) {
      id cityId cityName currencyCode title description productTypeId productName quantityRequired minimumQuality budgetCap
      deadlineTick status winnerCompanyId winnerCompanyName createdAtTick bidCount awardedBidPricePerUnit
      fulfilledQuantity fulfillmentPercent
    }
    myContractBids(companyId: $companyId) {
      id contractId companyId companyName bidPricePerUnit estimatedDeliveryTick submittedAtTick contractStatus
    }
  }
''';

const _fulfillShipmentMutation = r'''
  mutation FulfillContractShipment($input: FulfillContractShipmentInput!) {
    fulfillContractShipment(input: $input) { contractId status quantityDelivered quantityRequired fulfillmentPercent settledRevenue latePenaltyApplied }
  }
''';

const _companySettingsQuery = r'''
  query GetCompanySettings($companyId: UUID!) {
    companySettings(companyId: $companyId) {
      companyId companyName cash totalSharesIssued dividendPayoutRatio foundedAtTick administrationOverheadRate ageFactor assetFactor assetValue currencyCode
      citySalarySettings { cityId cityName currencyCode baseSalaryPerManhour salaryMultiplier effectiveSalaryPerManhour }
      pendingDividendProposal { id dividendPercent votingCloseTick ticksRemaining forVotes againstVotes myVoteChoice }
    }
  }
''';

const _updateCompanySettingsMutation = r'''
  mutation UpdateCompanySettings($input: UpdateCompanySettingsInput!) {
    updateCompanySettings(input: $input) { id name dividendPayoutRatio }
  }
''';

const _proposeDividendMutation = r'''
  mutation ProposeDividend($input: ProposeDividendInput!) {
    proposeDividend(input: $input) { id status ticksRemaining }
  }
''';

const _voteDividendMutation = r'''
  mutation VoteDividend($input: VoteDividendInput!) {
    voteDividend(input: $input) { id status forVotes againstVotes myVoteChoice }
  }
''';

const _brandQualityQuery = r'''
  query BrandQualityOverview($companyId: UUID!) {
    brandQualityOverview(companyId: $companyId) {
      companyId totalResearchBudgetUsd
      brands { id name scope productTypeId productName industryCategory quality marketingQuality combinedBrandQuality accumulatedResearchBudget baseResearchBudget marketingEfficiencyMultiplier }
    }
  }
''';

/// GraphQL calls for the Company Contracts, Company Settings, and Company
/// Research screens. The Ledger screen has its own `LedgerService`
/// (`ledger_service.dart`) since its query is much larger.
class CompanyService {
  const CompanyService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<(List<CompanyContractCard>, List<ContractBid>)> fetchCompanyContracts(String companyId) async {
    final result = await _graphQlService.request(_companyContractsQuery, variables: {'companyId': companyId});
    final contracts = (result['companyContracts'] as List<dynamic>? ?? const [])
        .map((e) => CompanyContractCard.fromJson(e as Map<String, dynamic>))
        .toList();
    final bids = (result['myContractBids'] as List<dynamic>? ?? const [])
        .map((e) => ContractBid.fromJson(e as Map<String, dynamic>))
        .toList();
    return (contracts, bids);
  }

  Future<void> fulfillShipment({required String contractId, required double quantity}) {
    return _graphQlService.request(
      _fulfillShipmentMutation,
      variables: {
        'input': {'contractId': contractId, 'quantity': quantity},
      },
    );
  }

  Future<CompanySettings> fetchCompanySettings(String companyId) async {
    final result = await _graphQlService.request(_companySettingsQuery, variables: {'companyId': companyId});
    return CompanySettings.fromJson(result['companySettings'] as Map<String, dynamic>);
  }

  Future<void> updateCompanySettings({
    required String companyId,
    required String name,
    required double dividendPayoutRatio,
    required List<Map<String, dynamic>> citySalarySettings,
  }) {
    return _graphQlService.request(
      _updateCompanySettingsMutation,
      variables: {
        'input': {
          'companyId': companyId,
          'name': name,
          'dividendPayoutRatio': dividendPayoutRatio,
          'citySalarySettings': citySalarySettings,
        },
      },
    );
  }

  Future<void> proposeDividend({required String companyId, required double dividendPercent}) {
    return _graphQlService.request(
      _proposeDividendMutation,
      variables: {
        'input': {'companyId': companyId, 'dividendPercent': dividendPercent},
      },
    );
  }

  Future<void> voteDividend({required String companyId, required bool approve}) {
    return _graphQlService.request(
      _voteDividendMutation,
      variables: {
        'input': {'companyId': companyId, 'vote': approve ? 'APPROVE' : 'REJECT'},
      },
    );
  }

  Future<BrandQualityOverview> fetchBrandQualityOverview(String companyId) async {
    final result = await _graphQlService.request(_brandQualityQuery, variables: {'companyId': companyId});
    return BrandQualityOverview.fromJson(result['brandQualityOverview'] as Map<String, dynamic>);
  }
}
