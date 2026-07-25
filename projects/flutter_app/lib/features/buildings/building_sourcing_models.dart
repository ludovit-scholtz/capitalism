// Data models for the Global Exchange sourcing/vendor-selector comparison
// surfaces for PURCHASE units (ROADMAP 136), mirroring
// `projects/frontend/src/types/exchange.ts` (`GlobalExchangeOffer`) and
// `projects/frontend/src/types/building.ts` (`ProcurementPreview`,
// `SourcingCandidate`) — field lists verified against `useBuildingDetail.ts`.
// Read-only comparison only, matching the three named operations
// (`sourcingCandidates`/`globalExchangeOffers`/`procurementPreview`) — no
// vendor-lock mutation is introduced here.

class GlobalExchangeOffer {
  const GlobalExchangeOffer({
    required this.cityId,
    required this.cityName,
    required this.resourceName,
    required this.unitSymbol,
    required this.exchangePricePerUnit,
    required this.estimatedQuality,
    required this.transitCostPerUnit,
    required this.deliveredPricePerUnit,
    required this.distanceKm,
  });

  final String cityId;
  final String cityName;
  final String? resourceName;
  final String? unitSymbol;
  final double exchangePricePerUnit;
  final double estimatedQuality;
  final double transitCostPerUnit;
  final double deliveredPricePerUnit;
  final double distanceKm;

  factory GlobalExchangeOffer.fromJson(Map<String, dynamic> json) => GlobalExchangeOffer(
    cityId: json['cityId'] as String,
    cityName: (json['cityName'] as String?) ?? '',
    resourceName: json['resourceName'] as String?,
    unitSymbol: json['unitSymbol'] as String?,
    exchangePricePerUnit: (json['exchangePricePerUnit'] as num?)?.toDouble() ?? 0,
    estimatedQuality: (json['estimatedQuality'] as num?)?.toDouble() ?? 0,
    transitCostPerUnit: (json['transitCostPerUnit'] as num?)?.toDouble() ?? 0,
    deliveredPricePerUnit: (json['deliveredPricePerUnit'] as num?)?.toDouble() ?? 0,
    distanceKm: (json['distanceKm'] as num?)?.toDouble() ?? 0,
  );
}

/// `sourceType`: `GLOBAL_EXCHANGE` | `LOCAL_B2B` | `LOCKED_VENDOR` | `NO_SOURCE`.
class ProcurementPreview {
  const ProcurementPreview({
    required this.sourceType,
    required this.sourceCityName,
    required this.sourceVendorName,
    required this.exchangePricePerUnit,
    required this.transitCostPerUnit,
    required this.deliveredPricePerUnit,
    required this.estimatedQuality,
    required this.canExecute,
    required this.blockReason,
    required this.blockMessage,
  });

  final String sourceType;
  final String? sourceCityName;
  final String? sourceVendorName;
  final double? exchangePricePerUnit;
  final double? transitCostPerUnit;
  final double? deliveredPricePerUnit;
  final double? estimatedQuality;
  final bool canExecute;
  final String? blockReason;
  final String? blockMessage;

  factory ProcurementPreview.fromJson(Map<String, dynamic> json) => ProcurementPreview(
    sourceType: (json['sourceType'] as String?) ?? 'NO_SOURCE',
    sourceCityName: json['sourceCityName'] as String?,
    sourceVendorName: json['sourceVendorName'] as String?,
    exchangePricePerUnit: (json['exchangePricePerUnit'] as num?)?.toDouble(),
    transitCostPerUnit: (json['transitCostPerUnit'] as num?)?.toDouble(),
    deliveredPricePerUnit: (json['deliveredPricePerUnit'] as num?)?.toDouble(),
    estimatedQuality: (json['estimatedQuality'] as num?)?.toDouble(),
    canExecute: json['canExecute'] as bool? ?? false,
    blockReason: json['blockReason'] as String?,
    blockMessage: json['blockMessage'] as String?,
  );
}

/// `sourceType` additionally includes `PLAYER_EXCHANGE_ORDER` vs.
/// [ProcurementPreview].
class SourcingCandidate {
  const SourcingCandidate({
    required this.sourceType,
    required this.sourceCityName,
    required this.sourceVendorName,
    required this.exchangePricePerUnit,
    required this.transitCostPerUnit,
    required this.deliveredPricePerUnit,
    required this.estimatedQuality,
    required this.distanceKm,
    required this.isEligible,
    required this.blockReason,
    required this.blockMessage,
    required this.isRecommended,
    required this.rank,
  });

  final String sourceType;
  final String? sourceCityName;
  final String? sourceVendorName;
  final double? exchangePricePerUnit;
  final double? transitCostPerUnit;
  final double? deliveredPricePerUnit;
  final double? estimatedQuality;
  final double? distanceKm;
  final bool isEligible;
  final String? blockReason;
  final String? blockMessage;
  final bool isRecommended;
  final int rank;

  factory SourcingCandidate.fromJson(Map<String, dynamic> json) => SourcingCandidate(
    sourceType: (json['sourceType'] as String?) ?? 'NO_SOURCE',
    sourceCityName: json['sourceCityName'] as String?,
    sourceVendorName: json['sourceVendorName'] as String?,
    exchangePricePerUnit: (json['exchangePricePerUnit'] as num?)?.toDouble(),
    transitCostPerUnit: (json['transitCostPerUnit'] as num?)?.toDouble(),
    deliveredPricePerUnit: (json['deliveredPricePerUnit'] as num?)?.toDouble(),
    estimatedQuality: (json['estimatedQuality'] as num?)?.toDouble(),
    distanceKm: (json['distanceKm'] as num?)?.toDouble(),
    isEligible: json['isEligible'] as bool? ?? false,
    blockReason: json['blockReason'] as String?,
    blockMessage: json['blockMessage'] as String?,
    isRecommended: json['isRecommended'] as bool? ?? false,
    rank: (json['rank'] as num?)?.toInt() ?? 0,
  );
}
