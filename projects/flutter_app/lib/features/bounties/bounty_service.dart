import '../../core/config/app_config.dart';
import '../../core/graphql/graphql_service.dart';
import 'bounty_models.dart';

/// Matches `RankingRewardStatus.Awarded` in
/// `projects/MasterApi/Data/Entities/MasterRankingRewarding.cs` — the only
/// status value that represents a *completed* (paid-out) bounty; the other
/// value, `REJECTED`, is excluded so this screen only ever shows completed
/// bounties, not rejected proof submissions.
const _awardedStatus = 'AWARDED';

const _myRankingBountyHistoryQuery = r'''
  query MyRankingBountyHistory($input: RankingHistoryFilterInput) {
    myRankingBountyHistory(input: $input) {
      id
      bountyCode
      bountyDisplayName
      pointsAwarded
      status
      serverKey
      eventDateUtc
      awardedAtUtc
    }
  }
''';

/// GraphQL calls for the player's completed bounties, matching
/// `fetchMyRankingBountyHistory` in
/// `projects/master-frontend/src/lib/masterApi.ts` and the backend's
/// `Query.GetMyRankingBountyHistory` (Master API, requires auth).
class BountyService {
  const BountyService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<CompletedBounty>> fetchCompletedBounties() async {
    final result = await _graphQlService.request(
      _myRankingBountyHistoryQuery,
      variables: {
        'input': {'status': _awardedStatus, 'limit': 100, 'offset': 0},
      },
      endpoint: AppConfig.masterGraphqlUrl,
    );
    final list = result['myRankingBountyHistory'] as List<dynamic>? ?? const [];
    return list.map((e) => CompletedBounty.fromJson(e as Map<String, dynamic>)).toList();
  }
}
