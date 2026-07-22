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
  });

  final String id;
  final String name;
  final String type;
  final int level;
  final String powerStatus;
  final String? destroyedAtUtc;
  final bool hasDefaultedCollateralLoan;
  final int unitCount;

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
    unitCount: ((json['units'] as List<dynamic>?) ?? const []).length,
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
