import '../../core/graphql/graphql_service.dart';
import 'leaderboard_models.dart';

const _playerRankingsQuery = r'''
  query PlayerRankings {
    rankings {
      playerId displayName personalAccountName totalWealth totalWealthUsd
      personalCash sharesValue companyCount badgeTypes
    }
  }
''';

const _companyRankingsQuery = r'''
  query CompanyRankingsFull {
    companyRankings {
      companyId companyName playerId ownerDisplayName ownerPersonalAccountName
      totalWealth totalWealthUsd currencyCode cash buildingValue inventoryValue buildingCount
    }
  }
''';

const _endgameStatusQuery = r'''
  query EndgameStatus {
    endgameStatus {
      winningThresholdUsd
      topRealWorldRichest { id rank name wealthUsd }
    }
  }
''';

const _playerProfileQuery = r'''
  query GetPlayerProfile($playerId: UUID!) {
    playerProfile(playerId: $playerId) {
      playerId displayName bio createdAtUtc joinGameYear hasProSubscription
      totalWealthUsd totalCompanyEquityUsd companyCount leaderboardRank
      activeBuildingTypes citiesWithBuildings totalProductsSold
      hallOfFame {
        highestSingleTickRevenue highestSingleTickRevenueTick
        largestBuildingAcquisitionPrice largestBuildingAcquisitionName
        highestBrandQuality highestBrandQualityName accountAgeTicks
      }
    }
  }
''';

const _playerBadgesQuery = r'''
  query GetPlayerBadges($playerId: UUID!) {
    playerBadges(playerId: $playerId) { id badgeType rarity unlockCondition unlockedAtUtc unlockedAtTick }
  }
''';

const _rankHistoryQuery = r'''
  query GetPlayerRankHistory($playerId: UUID!, $ticksBack: Int!) {
    rankHistory(playerId: $playerId, ticksBack: $ticksBack) {
      snapshotTick snapshotUtc leaderboardRank wealthUsd percentileRank positionChange
    }
  }
''';

/// GraphQL calls for the leaderboard and player profile screens, matching
/// `projects/frontend/src/views/LeaderboardView.vue` /
/// `PlayerProfileView.vue` and `components/profile/PlayerProfileTabsContent.vue`'s
/// exact query shapes. All queries here are public (no auth required),
/// matching the backend.
class LeaderboardService {
  const LeaderboardService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<PlayerRanking>> fetchPlayerRankings() async {
    final result = await _graphQlService.request(_playerRankingsQuery);
    final list = result['rankings'] as List<dynamic>? ?? const [];
    return list.map((e) => PlayerRanking.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<CompanyRanking>> fetchCompanyRankings() async {
    final result = await _graphQlService.request(_companyRankingsQuery);
    final list = result['companyRankings'] as List<dynamic>? ?? const [];
    return list.map((e) => CompanyRanking.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<EndgameStatus?> fetchEndgameStatus() async {
    try {
      final result = await _graphQlService.request(_endgameStatusQuery);
      final data = result['endgameStatus'] as Map<String, dynamic>?;
      return data == null ? null : EndgameStatus.fromJson(data);
    } catch (_) {
      return null;
    }
  }

  Future<PlayerProfile?> fetchPlayerProfile(String playerId) async {
    final result = await _graphQlService.request(_playerProfileQuery, variables: {'playerId': playerId});
    final data = result['playerProfile'] as Map<String, dynamic>?;
    return data == null ? null : PlayerProfile.fromJson(data);
  }

  Future<List<PlayerBadge>> fetchPlayerBadges(String playerId) async {
    final result = await _graphQlService.request(_playerBadgesQuery, variables: {'playerId': playerId});
    final list = result['playerBadges'] as List<dynamic>? ?? const [];
    return list.map((e) => PlayerBadge.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<PlayerRankSnapshot>> fetchRankHistory(String playerId, {int ticksBack = 365}) async {
    final result = await _graphQlService.request(
      _rankHistoryQuery,
      variables: {'playerId': playerId, 'ticksBack': ticksBack},
    );
    final list = result['rankHistory'] as List<dynamic>? ?? const [];
    return list.map((e) => PlayerRankSnapshot.fromJson(e as Map<String, dynamic>)).toList();
  }
}
