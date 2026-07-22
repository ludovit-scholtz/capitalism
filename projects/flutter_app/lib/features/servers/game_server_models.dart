/// Mirrors `GameServerSummary` in `projects/MasterApi/Types/Inputs.cs` and
/// `GameServerSummary` in `projects/master-frontend/src/lib/masterApi.ts`.
class GameServerSummary {
  const GameServerSummary({
    required this.id,
    required this.serverKey,
    required this.displayName,
    required this.description,
    required this.region,
    required this.environment,
    required this.backendUrl,
    required this.graphqlUrl,
    required this.frontendUrl,
    required this.version,
    required this.playerCount,
    required this.companyCount,
    required this.currentTick,
    required this.registeredAtUtc,
    required this.lastHeartbeatAtUtc,
    required this.isOnline,
  });

  factory GameServerSummary.fromJson(Map<String, dynamic> json) {
    return GameServerSummary(
      id: json['id'] as String,
      serverKey: json['serverKey'] as String,
      displayName: json['displayName'] as String,
      description: json['description'] as String? ?? '',
      region: json['region'] as String? ?? '',
      environment: json['environment'] as String? ?? '',
      backendUrl: json['backendUrl'] as String? ?? '',
      graphqlUrl: json['graphqlUrl'] as String,
      frontendUrl: json['frontendUrl'] as String? ?? '',
      version: json['version'] as String? ?? '',
      playerCount: (json['playerCount'] as num?)?.toInt() ?? 0,
      companyCount: (json['companyCount'] as num?)?.toInt() ?? 0,
      currentTick: (json['currentTick'] as num?)?.toInt() ?? 0,
      registeredAtUtc: json['registeredAtUtc'] as String? ?? '',
      lastHeartbeatAtUtc: json['lastHeartbeatAtUtc'] as String?,
      isOnline: json['isOnline'] as bool? ?? false,
    );
  }

  final String id;
  final String serverKey;
  final String displayName;
  final String description;
  final String region;
  final String environment;
  final String backendUrl;
  final String graphqlUrl;
  final String frontendUrl;
  final String version;
  final int playerCount;
  final int companyCount;
  final int currentTick;
  final String registeredAtUtc;
  final String? lastHeartbeatAtUtc;
  final bool isOnline;
}
