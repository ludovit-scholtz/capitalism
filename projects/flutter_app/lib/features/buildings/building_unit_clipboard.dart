// Pure port of `projects/frontend/src/lib/unitClipboard.ts`. Serializes a
// unit's config fields (position/link/structural fields excluded) to a
// versioned JSON payload written to the OS clipboard via
// `flutter/services.dart`'s `Clipboard` — the same OS-level clipboard the
// web version uses via `navigator.clipboard`, so the schema is kept
// byte-for-byte identical to let a config be copied on web and pasted on
// mobile (or vice versa).

import 'dart:convert';

import 'building_grid_models.dart';

const String unitClipboardSchemaVersion = 'unit-config-v1';

enum ClipboardParseError { empty, invalidJson, schemaMismatch, incompatibleType }

class ClipboardUnitConfig {
  const ClipboardUnitConfig({
    required this.unitType,
    this.resourceTypeId,
    this.productTypeId,
    this.minPrice,
    this.maxPrice,
    this.purchaseSource,
    this.saleVisibility,
    this.budget,
    this.mediaHouseBuildingId,
    this.minQuality,
    this.brandScope,
    this.vendorLockCompanyId,
    this.lockedCityId,
    this.industryCategory,
    this.lowInventoryAlertThreshold,
  });

  final String unitType;
  final String? resourceTypeId;
  final String? productTypeId;
  final double? minPrice;
  final double? maxPrice;
  final String? purchaseSource;
  final String? saleVisibility;
  final double? budget;
  final String? mediaHouseBuildingId;
  final double? minQuality;
  final String? brandScope;
  final String? vendorLockCompanyId;
  final String? lockedCityId;
  final String? industryCategory;
  final double? lowInventoryAlertThreshold;
}

class ClipboardParseResult {
  const ClipboardParseResult.ok(this.config) : error = null;
  const ClipboardParseResult.error(this.error) : config = null;

  final ClipboardUnitConfig? config;
  final ClipboardParseError? error;

  bool get isOk => config != null;
}

String serializeUnitConfig(EditableGridUnit unit) => jsonEncode({
  '__schema': unitClipboardSchemaVersion,
  'unitType': unit.unitType,
  'resourceTypeId': unit.resourceTypeId,
  'productTypeId': unit.productTypeId,
  'minPrice': unit.minPrice,
  'maxPrice': unit.maxPrice,
  'purchaseSource': unit.purchaseSource,
  'saleVisibility': unit.saleVisibility,
  'budget': unit.budget,
  'mediaHouseBuildingId': unit.mediaHouseBuildingId,
  'minQuality': unit.minQuality,
  'brandScope': unit.brandScope,
  'vendorLockCompanyId': unit.vendorLockCompanyId,
  'lockedCityId': unit.lockedCityId,
  'industryCategory': unit.industryCategory,
  'lowInventoryAlertThreshold': unit.lowInventoryAlertThreshold,
});

/// [targetUnitType] enforces a strict type match (pasting onto an occupied
/// cell); omit it to accept any allowed type (pasting onto an empty cell).
ClipboardParseResult deserializeUnitConfig(String? raw, {String? targetUnitType}) {
  if (raw == null || raw.trim().isEmpty) return const ClipboardParseResult.error(ClipboardParseError.empty);
  Map<String, dynamic> json;
  try {
    final decoded = jsonDecode(raw);
    if (decoded is! Map<String, dynamic>) return const ClipboardParseResult.error(ClipboardParseError.invalidJson);
    json = decoded;
  } catch (_) {
    return const ClipboardParseResult.error(ClipboardParseError.invalidJson);
  }

  if (json['__schema'] != unitClipboardSchemaVersion) {
    return const ClipboardParseResult.error(ClipboardParseError.schemaMismatch);
  }
  final unitType = json['unitType'] as String?;
  if (unitType == null) return const ClipboardParseResult.error(ClipboardParseError.invalidJson);
  if (targetUnitType != null && unitType != targetUnitType) {
    return const ClipboardParseResult.error(ClipboardParseError.incompatibleType);
  }

  return ClipboardParseResult.ok(
    ClipboardUnitConfig(
      unitType: unitType,
      resourceTypeId: json['resourceTypeId'] as String?,
      productTypeId: json['productTypeId'] as String?,
      minPrice: (json['minPrice'] as num?)?.toDouble(),
      maxPrice: (json['maxPrice'] as num?)?.toDouble(),
      purchaseSource: json['purchaseSource'] as String?,
      saleVisibility: json['saleVisibility'] as String?,
      budget: (json['budget'] as num?)?.toDouble(),
      mediaHouseBuildingId: json['mediaHouseBuildingId'] as String?,
      minQuality: (json['minQuality'] as num?)?.toDouble(),
      brandScope: json['brandScope'] as String?,
      vendorLockCompanyId: json['vendorLockCompanyId'] as String?,
      lockedCityId: json['lockedCityId'] as String?,
      industryCategory: json['industryCategory'] as String?,
      lowInventoryAlertThreshold: (json['lowInventoryAlertThreshold'] as num?)?.toDouble(),
    ),
  );
}

/// Overwrites every config field on [unit] with [config]'s values —
/// position/id/level/link fields are never touched, matching the web's
/// `applyConfigToUnit`.
void applyConfigToUnit(EditableGridUnit unit, ClipboardUnitConfig config) {
  unit.resourceTypeId = config.resourceTypeId;
  unit.productTypeId = config.productTypeId;
  unit.minPrice = config.minPrice;
  unit.maxPrice = config.maxPrice;
  unit.purchaseSource = config.purchaseSource;
  unit.saleVisibility = config.saleVisibility;
  unit.budget = config.budget;
  unit.mediaHouseBuildingId = config.mediaHouseBuildingId;
  unit.minQuality = config.minQuality;
  unit.brandScope = config.brandScope;
  unit.vendorLockCompanyId = config.vendorLockCompanyId;
  unit.lockedCityId = config.lockedCityId;
  unit.industryCategory = config.industryCategory;
  unit.lowInventoryAlertThreshold = config.lowInventoryAlertThreshold;
}
