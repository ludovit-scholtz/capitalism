// Data models for the Dashboard screen, mirroring
// `projects/frontend/src/views/DashboardView.vue`'s initial combined query.
// GraphQL field names verified against `projects/Api/Types/Query.*.cs`.

class ScheduledAction {
  const ScheduledAction({
    required this.id,
    required this.actionType,
    required this.buildingName,
    required this.ticksRemaining,
  });

  final String id;
  final String actionType;
  final String buildingName;
  final int ticksRemaining;

  factory ScheduledAction.fromJson(Map<String, dynamic> json) => ScheduledAction(
    id: json['id'] as String,
    actionType: (json['actionType'] as String?) ?? 'UPDATE',
    buildingName: (json['buildingName'] as String?) ?? 'Building',
    ticksRemaining: (json['ticksRemaining'] as num?)?.toInt() ?? 0,
  );
}

/// A building unit's grid position and type — enough for the dashboard's
/// supply-chain strip to sort/label/color units without needing the full
/// `BuildingUnitDetail` model used by the building-detail grid editor.
class DashboardUnit {
  const DashboardUnit({required this.id, required this.unitType, required this.gridX, required this.gridY});

  final String id;
  final String unitType;
  final int gridX;
  final int gridY;

  factory DashboardUnit.fromJson(Map<String, dynamic> json) => DashboardUnit(
    id: json['id'] as String,
    unitType: (json['unitType'] as String?) ?? '',
    gridX: (json['gridX'] as num?)?.toInt() ?? 0,
    gridY: (json['gridY'] as num?)?.toInt() ?? 0,
  );
}

class DashboardBuilding {
  const DashboardBuilding({
    required this.id,
    required this.name,
    required this.type,
    required this.level,
    required this.powerStatus,
    required this.destroyedAtUtc,
    required this.hasDefaultedCollateralLoan,
    required this.unitCount,
    this.cityId = '',
    this.units = const [],
  });

  final String id;
  final String name;
  final String type;
  final int level;
  final String powerStatus;
  final String? destroyedAtUtc;
  final bool hasDefaultedCollateralLoan;
  final int unitCount;

  /// Needed to dedupe buildings by city for the per-city power-grid summary
  /// (Buildings tab).
  final String cityId;

  /// Per-unit grid position/type, for the supply-chain strip. Kept separate
  /// from [unitCount] (which existed first and several call sites already
  /// depend on as a plain int) rather than deriving it from `units.length`,
  /// to avoid a breaking constructor change.
  final List<DashboardUnit> units;

  bool get isDestroyed => destroyedAtUtc != null;

  /// Mirrors the web's badge rule: only flag power status when it's not the
  /// healthy default.
  bool get hasPowerIssue => powerStatus != 'POWERED';

  factory DashboardBuilding.fromJson(Map<String, dynamic> json) => DashboardBuilding(
    id: json['id'] as String,
    name: json['name'] as String,
    type: (json['type'] as String?) ?? 'FACTORY',
    level: (json['level'] as num?)?.toInt() ?? 1,
    powerStatus: (json['powerStatus'] as String?) ?? 'POWERED',
    destroyedAtUtc: json['destroyedAtUtc'] as String?,
    hasDefaultedCollateralLoan: json['hasDefaultedCollateralLoan'] as bool? ?? false,
    units: ((json['units'] as List<dynamic>?) ?? const [])
        .map((u) => DashboardUnit.fromJson(u as Map<String, dynamic>))
        .toList(),
    unitCount: ((json['units'] as List<dynamic>?) ?? const []).length,
    cityId: (json['cityId'] as String?) ?? '',
  );
}

class DashboardCompany {
  const DashboardCompany({required this.id, required this.name, required this.cash, required this.buildings});

  final String id;
  final String name;
  final double cash;
  final List<DashboardBuilding> buildings;

  factory DashboardCompany.fromJson(Map<String, dynamic> json) => DashboardCompany(
    id: json['id'] as String,
    name: json['name'] as String,
    cash: (json['cash'] as num?)?.toDouble() ?? 0,
    buildings: ((json['buildings'] as List<dynamic>?) ?? const [])
        .map((b) => DashboardBuilding.fromJson(b as Map<String, dynamic>))
        .toList(),
  );
}

/// Eligibility snapshot for the "Launch New Company" flow, mirroring
/// `AdditionalCompanyPrerequisites` (`Api/Types/AccountExchangeTypes.cs`).
class AdditionalCompanyPrerequisites {
  const AdditionalCompanyPrerequisites({
    required this.companyCount,
    required this.underMaxCap,
    required this.hasExistingCompany,
    required this.companyAgeRequirementMet,
    required this.ticksUntilAgeRequirementMet,
    required this.profitabilityRequirementMet,
    required this.balanceRequirementMet,
    required this.allRequirementsMet,
  });

  final int companyCount;
  final bool underMaxCap;
  final bool hasExistingCompany;
  final bool companyAgeRequirementMet;
  final int ticksUntilAgeRequirementMet;
  final bool profitabilityRequirementMet;
  final bool balanceRequirementMet;
  final bool allRequirementsMet;

  factory AdditionalCompanyPrerequisites.fromJson(Map<String, dynamic> json) => AdditionalCompanyPrerequisites(
    companyCount: (json['companyCount'] as num?)?.toInt() ?? 0,
    underMaxCap: json['underMaxCap'] as bool? ?? false,
    hasExistingCompany: json['hasExistingCompany'] as bool? ?? false,
    companyAgeRequirementMet: json['companyAgeRequirementMet'] as bool? ?? false,
    ticksUntilAgeRequirementMet: (json['ticksUntilAgeRequirementMet'] as num?)?.toInt() ?? 0,
    profitabilityRequirementMet: json['profitabilityRequirementMet'] as bool? ?? false,
    balanceRequirementMet: json['balanceRequirementMet'] as bool? ?? false,
    allRequirementsMet: json['allRequirementsMet'] as bool? ?? false,
  );
}

/// A city choice for the "Launch New Company" wizard — deliberately a
/// smaller model than `OnboardingCity`, which carries onboarding-only
/// fields (population, resources) not needed here.
class NewCompanyCity {
  const NewCompanyCity({required this.id, required this.name, required this.currencyCode});

  final String id;
  final String name;
  final String currencyCode;

  factory NewCompanyCity.fromJson(Map<String, dynamic> json) => NewCompanyCity(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    currencyCode: (json['currencyCode'] as String?) ?? 'USD',
  );
}

class NewCompanyResult {
  const NewCompanyResult({required this.id, required this.name});

  final String id;
  final String name;

  factory NewCompanyResult.fromJson(Map<String, dynamic> json) =>
      NewCompanyResult(id: json['id'] as String, name: (json['name'] as String?) ?? '');
}

class DashboardData {
  const DashboardData({
    required this.companies,
    required this.currentTick,
    required this.taxRate,
    required this.pendingActions,
  });

  final List<DashboardCompany> companies;
  final int currentTick;
  final double taxRate;
  final List<ScheduledAction> pendingActions;
}
