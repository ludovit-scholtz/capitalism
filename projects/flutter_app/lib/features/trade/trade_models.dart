// Data model for the Trade Routes screen, mirroring
// `projects/frontend/src/views/TradeRoutesView.vue`. GraphQL field names
// verified against `Api/Types/Query.TradeRoutes.cs` (`myTradeRoutes`).

class TradeRoute {
  const TradeRoute({
    required this.id,
    required this.sourceBuildingName,
    required this.sourceCityName,
    required this.destinationBuildingName,
    required this.destinationCityName,
    required this.productTypeName,
    required this.resourceTypeName,
    required this.quantity,
    required this.expectedArrivalTick,
    required this.status,
    required this.failureReason,
  });

  final String id;
  final String sourceBuildingName;
  final String sourceCityName;
  final String destinationBuildingName;
  final String destinationCityName;
  final String? productTypeName;
  final String? resourceTypeName;
  final double quantity;
  final int expectedArrivalTick;

  /// `SCHEDULED`, `IN_TRANSIT`, `COMPLETED`, or `FAILED`.
  final String status;
  final String? failureReason;

  String get itemName => productTypeName ?? resourceTypeName ?? 'Unknown item';

  factory TradeRoute.fromJson(Map<String, dynamic> json) => TradeRoute(
    id: json['id'] as String,
    sourceBuildingName: (json['sourceBuildingName'] as String?) ?? '',
    sourceCityName: (json['sourceCityName'] as String?) ?? '',
    destinationBuildingName: (json['destinationBuildingName'] as String?) ?? '',
    destinationCityName: (json['destinationCityName'] as String?) ?? '',
    productTypeName: json['productTypeName'] as String?,
    resourceTypeName: json['resourceTypeName'] as String?,
    quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
    expectedArrivalTick: (json['expectedArrivalTick'] as num?)?.toInt() ?? 0,
    status: (json['status'] as String?) ?? 'SCHEDULED',
    failureReason: json['failureReason'] as String?,
  );
}
