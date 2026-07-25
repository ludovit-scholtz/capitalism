/// Snapshot of the shared server tick clock, mirroring the subset of
/// `GameState` (`projects/frontend/src/types/index.ts`) that
/// [GameStatusBar]'s tick-progress indicator needs. Field names verified
/// against `Api/Data/Entities/GameState.cs` and `stores/gameState.ts`'s
/// query.
library;

class GameStateModel {
  const GameStateModel({
    required this.currentTick,
    required this.lastTickAtUtc,
    required this.tickIntervalSeconds,
    required this.taxRate,
  });

  final int currentTick;
  final DateTime? lastTickAtUtc;
  final double tickIntervalSeconds;
  final double taxRate;

  factory GameStateModel.fromJson(Map<String, dynamic> json) => GameStateModel(
    currentTick: (json['currentTick'] as num?)?.toInt() ?? 0,
    lastTickAtUtc: json['lastTickAtUtc'] != null ? DateTime.tryParse(json['lastTickAtUtc'] as String) : null,
    tickIntervalSeconds: (json['tickIntervalSeconds'] as num?)?.toDouble() ?? 10,
    taxRate: (json['taxRate'] as num?)?.toDouble() ?? 0,
  );
}
