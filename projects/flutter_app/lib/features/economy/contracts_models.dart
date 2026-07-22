// Data models for the player's cross-company supply-contract dashboard,
// mirroring `projects/frontend/src/views/ContractsView.vue` — distinct from
// `CompanyContractsView.vue` (government procurement) and
// `CityContractsTab.vue` (browsing open offers in a city). GraphQL field
// names verified against `projects/Api/Types/Query.SupplyContracts.cs` /
// `Mutation.SupplyContracts.cs`.

class SupplyContract {
  const SupplyContract({
    required this.id,
    required this.sellerCompanyId,
    required this.sellerCompanyName,
    required this.buyerCompanyId,
    required this.buyerCompanyName,
    required this.resourceTypeName,
    required this.productTypeName,
    required this.quantityPerTick,
    required this.pricePerUnit,
    required this.remainingTicks,
    required this.penaltyRatePercent,
    required this.currencyCode,
    required this.status,
    required this.totalDeliveredQuantity,
    required this.totalUndeliveredQuantity,
    required this.totalPenaltyAmount,
    required this.penaltyCount,
  });

  final String id;
  final String sellerCompanyId;
  final String sellerCompanyName;
  final String buyerCompanyId;
  final String buyerCompanyName;
  final String? resourceTypeName;
  final String? productTypeName;
  final double quantityPerTick;
  final double pricePerUnit;
  final int remainingTicks;
  final double penaltyRatePercent;
  final String currencyCode;

  /// `PENDING`, `ACTIVE`, `FULFILLED`, `BREACHED`, or `CANCELLED`.
  final String status;
  final double totalDeliveredQuantity;
  final double totalUndeliveredQuantity;
  final double totalPenaltyAmount;
  final int penaltyCount;

  String get itemName => resourceTypeName ?? productTypeName ?? 'Unknown item';

  /// Mirrors `deliveryBadge` in `ContractsView.vue` exactly.
  String get healthBadge {
    if (penaltyCount > 0) return 'error';
    if (totalUndeliveredQuantity > 0) return 'warning';
    return 'ok';
  }

  factory SupplyContract.fromJson(Map<String, dynamic> json) => SupplyContract(
    id: json['id'] as String,
    sellerCompanyId: json['sellerCompanyId'] as String,
    sellerCompanyName: (json['sellerCompanyName'] as String?) ?? '',
    buyerCompanyId: json['buyerCompanyId'] as String,
    buyerCompanyName: (json['buyerCompanyName'] as String?) ?? '',
    resourceTypeName: json['resourceTypeName'] as String?,
    productTypeName: json['productTypeName'] as String?,
    quantityPerTick: (json['quantityPerTick'] as num?)?.toDouble() ?? 0,
    pricePerUnit: (json['pricePerUnit'] as num?)?.toDouble() ?? 0,
    remainingTicks: (json['remainingTicks'] as num?)?.toInt() ?? 0,
    penaltyRatePercent: (json['penaltyRatePercent'] as num?)?.toDouble() ?? 0,
    currencyCode: (json['currencyCode'] as String?) ?? 'USD',
    status: (json['status'] as String?) ?? 'PENDING',
    totalDeliveredQuantity: (json['totalDeliveredQuantity'] as num?)?.toDouble() ?? 0,
    totalUndeliveredQuantity: (json['totalUndeliveredQuantity'] as num?)?.toDouble() ?? 0,
    totalPenaltyAmount: (json['totalPenaltyAmount'] as num?)?.toDouble() ?? 0,
    penaltyCount: (json['penaltyCount'] as num?)?.toInt() ?? 0,
  );
}

class ContractCompanyOption {
  const ContractCompanyOption({required this.id, required this.name});

  final String id;
  final String name;
}

/// Fixed duration choices offered by the web's create-contract form.
const List<int> contractDurationOptions = [25, 50, 100, 200, 500];
