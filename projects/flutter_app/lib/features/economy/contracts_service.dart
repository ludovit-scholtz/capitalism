import '../../core/graphql/graphql_service.dart';
import 'contracts_models.dart';

const _myContractsQuery = r'''
  query MyContracts {
    myContracts(take: 200, skip: 0) {
      id sellerCompanyId sellerCompanyName buyerCompanyId buyerCompanyName sellerBuildingUnitId
      resourceTypeId resourceTypeName productTypeId productTypeName quantityPerTick pricePerUnit
      durationTicks remainingTicks startTick penaltyRatePercent currencyCode status createdAtTick
      totalDeliveredQuantity totalUndeliveredQuantity totalPenaltyAmount penaltyCount
    }
  }
''';

const _meCompaniesQuery = r'''
  query MeCompanies { me { companies { id name } } }
''';

const _companyRankingsQuery = r'''
  query CompanyRankings { companyRankings { companyId companyName } }
''';

const _proposeMutation = r'''
  mutation ProposeSupplyContract($input: ProposeSupplyContractInput!) {
    proposeSupplyContract(input: $input) { success message contract { id } }
  }
''';

const _acceptMutation = r'''
  mutation AcceptSupplyContract($id: UUID!) { acceptSupplyContract(id: $id) { success } }
''';

const _rejectMutation = r'''
  mutation RejectSupplyContract($id: UUID!) { rejectSupplyContract(id: $id) { success } }
''';

const _cancelMutation = r'''
  mutation CancelSupplyContract($id: UUID!) { cancelSupplyContract(id: $id) { success } }
''';

/// GraphQL calls for the player's cross-company supply contracts dashboard,
/// matching `projects/frontend/src/views/ContractsView.vue`'s exact
/// query/mutation contract.
class ContractsService {
  const ContractsService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<SupplyContract>> fetchContracts() async {
    final result = await _graphQlService.request(_myContractsQuery);
    final list = result['myContracts'] as List<dynamic>? ?? const [];
    return list.map((e) => SupplyContract.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<ContractCompanyOption>> fetchMyCompanies() async {
    final result = await _graphQlService.request(_meCompaniesQuery);
    final companies = (result['me'] as Map<String, dynamic>?)?['companies'] as List<dynamic>? ?? const [];
    return companies
        .map((e) => ContractCompanyOption(id: (e as Map<String, dynamic>)['id'] as String, name: e['name'] as String))
        .toList();
  }

  Future<List<ContractCompanyOption>> fetchAllCompanies() async {
    final result = await _graphQlService.request(_companyRankingsQuery);
    final companies = result['companyRankings'] as List<dynamic>? ?? const [];
    return companies
        .map(
          (e) => ContractCompanyOption(
            id: (e as Map<String, dynamic>)['companyId'] as String,
            name: e['companyName'] as String,
          ),
        )
        .toList();
  }

  Future<void> proposeContract({
    required String sellerCompanyId,
    required String buyerCompanyId,
    required String sellerBuildingUnitId,
    String? resourceTypeId,
    String? productTypeId,
    required double quantityPerTick,
    required double pricePerUnit,
    required int durationTicks,
    required double penaltyRatePercent,
  }) async {
    await _graphQlService.request(
      _proposeMutation,
      variables: {
        'input': {
          'sellerCompanyId': sellerCompanyId,
          'buyerCompanyId': buyerCompanyId,
          'sellerBuildingUnitId': sellerBuildingUnitId,
          'resourceTypeId': resourceTypeId,
          'productTypeId': productTypeId,
          'quantityPerTick': quantityPerTick,
          'pricePerUnit': pricePerUnit,
          'durationTicks': durationTicks,
          'penaltyRatePercent': penaltyRatePercent,
        },
      },
    );
  }

  Future<void> acceptContract(String id) => _graphQlService.request(_acceptMutation, variables: {'id': id});

  Future<void> rejectContract(String id) => _graphQlService.request(_rejectMutation, variables: {'id': id});

  Future<void> cancelContract(String id) => _graphQlService.request(_cancelMutation, variables: {'id': id});
}
