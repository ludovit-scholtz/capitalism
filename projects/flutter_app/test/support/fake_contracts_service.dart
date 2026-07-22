import 'package:capitalism_app/features/economy/contracts_models.dart';
import 'package:capitalism_app/features/economy/contracts_service.dart';

class FakeContractsService implements ContractsService {
  FakeContractsService({
    this.contracts = const [],
    this.myCompanies = const [],
    this.allCompanies = const [],
    this.fetchError,
    this.actionError,
  });

  final List<SupplyContract> contracts;
  final List<ContractCompanyOption> myCompanies;
  final List<ContractCompanyOption> allCompanies;
  final Object? fetchError;
  final Object? actionError;

  final List<String> calls = [];
  final List<String> acceptedIds = [];
  final List<String> rejectedIds = [];
  final List<String> cancelledIds = [];
  Map<String, dynamic>? lastProposeArgs;

  @override
  Future<List<SupplyContract>> fetchContracts() async {
    calls.add('fetchContracts');
    if (fetchError != null) throw fetchError!;
    return contracts;
  }

  @override
  Future<List<ContractCompanyOption>> fetchMyCompanies() async {
    calls.add('fetchMyCompanies');
    return myCompanies;
  }

  @override
  Future<List<ContractCompanyOption>> fetchAllCompanies() async {
    calls.add('fetchAllCompanies');
    return allCompanies;
  }

  @override
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
    calls.add('proposeContract');
    if (actionError != null) throw actionError!;
    lastProposeArgs = {
      'sellerCompanyId': sellerCompanyId,
      'buyerCompanyId': buyerCompanyId,
      'sellerBuildingUnitId': sellerBuildingUnitId,
      'resourceTypeId': resourceTypeId,
      'productTypeId': productTypeId,
      'quantityPerTick': quantityPerTick,
      'pricePerUnit': pricePerUnit,
      'durationTicks': durationTicks,
      'penaltyRatePercent': penaltyRatePercent,
    };
  }

  @override
  Future<void> acceptContract(String id) async {
    calls.add('acceptContract');
    if (actionError != null) throw actionError!;
    acceptedIds.add(id);
  }

  @override
  Future<void> rejectContract(String id) async {
    calls.add('rejectContract');
    if (actionError != null) throw actionError!;
    rejectedIds.add(id);
  }

  @override
  Future<void> cancelContract(String id) async {
    calls.add('cancelContract');
    if (actionError != null) throw actionError!;
    cancelledIds.add(id);
  }
}
