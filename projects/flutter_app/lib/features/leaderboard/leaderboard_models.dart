// Data models for the Leaderboard and Player Profile screens, mirroring
// `projects/frontend/src/views/LeaderboardView.vue` /
// `PlayerProfileView.vue`. GraphQL field names verified against
// `Api/Types/Query.Types.Rankings.cs`, `Query.Types.Endgame.cs`,
// `Query.Profile.cs`, and `Query.PlayerProfile.cs`.

class PlayerRanking {
  const PlayerRanking({
    required this.playerId,
    required this.displayName,
    required this.personalAccountName,
    required this.totalWealthUsd,
    required this.personalCash,
    required this.sharesValue,
    required this.companyCount,
    required this.badgeTypes,
  });

  final String playerId;
  final String displayName;
  final String personalAccountName;
  final double totalWealthUsd;
  final double personalCash;
  final double sharesValue;
  final int companyCount;
  final List<String> badgeTypes;

  /// Mirrors `getPlayerAlias`: prefer the personal account alias.
  String get alias => personalAccountName.isNotEmpty ? personalAccountName : displayName;

  factory PlayerRanking.fromJson(Map<String, dynamic> json) => PlayerRanking(
    playerId: json['playerId'] as String,
    displayName: (json['displayName'] as String?) ?? '',
    personalAccountName: (json['personalAccountName'] as String?) ?? '',
    totalWealthUsd: (json['totalWealthUsd'] as num?)?.toDouble() ?? 0,
    personalCash: (json['personalCash'] as num?)?.toDouble() ?? 0,
    sharesValue: (json['sharesValue'] as num?)?.toDouble() ?? 0,
    companyCount: (json['companyCount'] as num?)?.toInt() ?? 0,
    badgeTypes: ((json['badgeTypes'] as List<dynamic>?) ?? const []).cast<String>(),
  );
}

class CompanyRanking {
  const CompanyRanking({
    required this.companyId,
    required this.companyName,
    required this.playerId,
    required this.ownerDisplayName,
    required this.ownerPersonalAccountName,
    required this.totalWealthUsd,
    required this.currencyCode,
    required this.cash,
    required this.buildingValue,
    required this.inventoryValue,
    required this.buildingCount,
  });

  final String companyId;
  final String companyName;
  final String playerId;
  final String ownerDisplayName;
  final String ownerPersonalAccountName;
  final double totalWealthUsd;
  final String currencyCode;
  final double cash;
  final double buildingValue;
  final double inventoryValue;
  final int buildingCount;

  String get ownerAlias => ownerPersonalAccountName.isNotEmpty ? ownerPersonalAccountName : ownerDisplayName;

  factory CompanyRanking.fromJson(Map<String, dynamic> json) => CompanyRanking(
    companyId: json['companyId'] as String,
    companyName: (json['companyName'] as String?) ?? '',
    playerId: json['playerId'] as String,
    ownerDisplayName: (json['ownerDisplayName'] as String?) ?? '',
    ownerPersonalAccountName: (json['ownerPersonalAccountName'] as String?) ?? '',
    totalWealthUsd: (json['totalWealthUsd'] as num?)?.toDouble() ?? 0,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    cash: (json['cash'] as num?)?.toDouble() ?? 0,
    buildingValue: (json['buildingValue'] as num?)?.toDouble() ?? 0,
    inventoryValue: (json['inventoryValue'] as num?)?.toDouble() ?? 0,
    buildingCount: (json['buildingCount'] as num?)?.toInt() ?? 0,
  );
}

class RealWorldWealth {
  const RealWorldWealth({required this.id, required this.rank, required this.name, required this.wealthUsd});

  final String id;
  final int rank;
  final String name;
  final double wealthUsd;

  factory RealWorldWealth.fromJson(Map<String, dynamic> json) => RealWorldWealth(
    id: json['id'] as String,
    rank: (json['rank'] as num?)?.toInt() ?? 0,
    name: (json['name'] as String?) ?? '',
    wealthUsd: (json['wealthUsd'] as num?)?.toDouble() ?? 0,
  );
}

class EndgameStatus {
  const EndgameStatus({required this.winningThresholdUsd, required this.topRealWorldRichest});

  final double winningThresholdUsd;
  final List<RealWorldWealth> topRealWorldRichest;

  factory EndgameStatus.fromJson(Map<String, dynamic> json) => EndgameStatus(
    winningThresholdUsd: (json['winningThresholdUsd'] as num?)?.toDouble() ?? 0,
    topRealWorldRichest: ((json['topRealWorldRichest'] as List<dynamic>?) ?? const [])
        .map((e) => RealWorldWealth.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

/// Compact icon lookup mirroring `lib/profileBadges.ts`'s `profileBadgeCatalog`.
const Map<String, String> profileBadgeIcons = {
  'FIRST_B2B_TRADE': '🤝',
  'LOAN_MASTER': '🏦',
  'MEDIA_MOGUL': '📺',
  'BANK_BARON': '💳',
  'MARKET_DOMINATOR_V2': '👑',
  'TOP_RANK': '🥇',
  'WEALTH_MILESTONE': '💰',
  'FIRST_MILLION': '💰',
  'MONOPOLIST': '🏛️',
  'MASTER_TRADER': '📈',
  'POWER_MAGNATE': '⚡',
  'CITY_PIONEER': '🌆',
  'EXPORT_CHAMPION': '🚢',
  'INDUSTRY_LEADER': '🏭',
  'MARKET_DOMINATOR': '👑',
  'RANK_CLIMBER': '🚀',
  'LEGENDARY_TYCOON': '💎',
};

String profileBadgeIcon(String badgeType) => profileBadgeIcons[badgeType] ?? '🏅';

/// Mirrors `rankBadge`: medal emoji for top 3, else the plain number.
String rankBadge(int rank) {
  switch (rank) {
    case 1:
      return '🥇';
    case 2:
      return '🥈';
    case 3:
      return '🥉';
    default:
      return '#$rank';
  }
}

class PlayerHallOfFame {
  const PlayerHallOfFame({
    required this.highestSingleTickRevenue,
    required this.highestSingleTickRevenueTick,
    required this.largestBuildingAcquisitionPrice,
    required this.largestBuildingAcquisitionName,
    required this.highestBrandQuality,
    required this.highestBrandQualityName,
    required this.accountAgeTicks,
  });

  final double highestSingleTickRevenue;
  final int highestSingleTickRevenueTick;
  final double largestBuildingAcquisitionPrice;
  final String? largestBuildingAcquisitionName;
  final double highestBrandQuality;
  final String? highestBrandQualityName;
  final int accountAgeTicks;

  factory PlayerHallOfFame.fromJson(Map<String, dynamic> json) => PlayerHallOfFame(
    highestSingleTickRevenue: (json['highestSingleTickRevenue'] as num?)?.toDouble() ?? 0,
    highestSingleTickRevenueTick: (json['highestSingleTickRevenueTick'] as num?)?.toInt() ?? 0,
    largestBuildingAcquisitionPrice: (json['largestBuildingAcquisitionPrice'] as num?)?.toDouble() ?? 0,
    largestBuildingAcquisitionName: json['largestBuildingAcquisitionName'] as String?,
    highestBrandQuality: (json['highestBrandQuality'] as num?)?.toDouble() ?? 0,
    highestBrandQualityName: json['highestBrandQualityName'] as String?,
    accountAgeTicks: (json['accountAgeTicks'] as num?)?.toInt() ?? 0,
  );
}

class PlayerProfile {
  const PlayerProfile({
    required this.playerId,
    required this.displayName,
    required this.bio,
    required this.createdAtUtc,
    required this.joinGameYear,
    required this.hasProSubscription,
    required this.totalWealthUsd,
    required this.totalCompanyEquityUsd,
    required this.companyCount,
    required this.leaderboardRank,
    required this.activeBuildingTypes,
    required this.citiesWithBuildings,
    required this.totalProductsSold,
    required this.hallOfFame,
  });

  final String playerId;
  final String displayName;
  final String? bio;
  final String createdAtUtc;
  final int joinGameYear;
  final bool hasProSubscription;
  final double totalWealthUsd;
  final double totalCompanyEquityUsd;
  final int companyCount;
  final int leaderboardRank;
  final List<String> activeBuildingTypes;
  final int citiesWithBuildings;
  final double totalProductsSold;
  final PlayerHallOfFame hallOfFame;

  factory PlayerProfile.fromJson(Map<String, dynamic> json) => PlayerProfile(
    playerId: json['playerId'] as String,
    displayName: (json['displayName'] as String?) ?? '',
    bio: json['bio'] as String?,
    createdAtUtc: (json['createdAtUtc'] as String?) ?? '',
    joinGameYear: (json['joinGameYear'] as num?)?.toInt() ?? 0,
    hasProSubscription: json['hasProSubscription'] as bool? ?? false,
    totalWealthUsd: (json['totalWealthUsd'] as num?)?.toDouble() ?? 0,
    totalCompanyEquityUsd: (json['totalCompanyEquityUsd'] as num?)?.toDouble() ?? 0,
    companyCount: (json['companyCount'] as num?)?.toInt() ?? 0,
    leaderboardRank: (json['leaderboardRank'] as num?)?.toInt() ?? 0,
    activeBuildingTypes: ((json['activeBuildingTypes'] as List<dynamic>?) ?? const []).cast<String>(),
    citiesWithBuildings: (json['citiesWithBuildings'] as num?)?.toInt() ?? 0,
    totalProductsSold: (json['totalProductsSold'] as num?)?.toDouble() ?? 0,
    hallOfFame: PlayerHallOfFame.fromJson((json['hallOfFame'] as Map<String, dynamic>?) ?? const {}),
  );
}

class PlayerBadge {
  const PlayerBadge({
    required this.id,
    required this.badgeType,
    required this.rarity,
    required this.unlockCondition,
    required this.unlockedAtUtc,
  });

  final String id;
  final String badgeType;

  /// `COMMON`, `RARE`, `EPIC`, or `LEGENDARY`.
  final String rarity;
  final String unlockCondition;
  final String unlockedAtUtc;

  factory PlayerBadge.fromJson(Map<String, dynamic> json) => PlayerBadge(
    id: json['id'] as String,
    badgeType: (json['badgeType'] as String?) ?? '',
    rarity: (json['rarity'] as String?) ?? 'COMMON',
    unlockCondition: (json['unlockCondition'] as String?) ?? '',
    unlockedAtUtc: (json['unlockedAtUtc'] as String?) ?? '',
  );
}

class PlayerRankSnapshot {
  const PlayerRankSnapshot({
    required this.snapshotTick,
    required this.snapshotUtc,
    required this.leaderboardRank,
    required this.wealthUsd,
    required this.positionChange,
  });

  final int snapshotTick;
  final String snapshotUtc;
  final int leaderboardRank;
  final double wealthUsd;
  final int? positionChange;

  factory PlayerRankSnapshot.fromJson(Map<String, dynamic> json) => PlayerRankSnapshot(
    snapshotTick: (json['snapshotTick'] as num?)?.toInt() ?? 0,
    snapshotUtc: (json['snapshotUtc'] as String?) ?? '',
    leaderboardRank: (json['leaderboardRank'] as num?)?.toInt() ?? 0,
    wealthUsd: (json['wealthUsd'] as num?)?.toDouble() ?? 0,
    positionChange: (json['positionChange'] as num?)?.toInt(),
  );
}
