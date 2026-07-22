import 'package:capitalism_app/features/leaderboard/leaderboard_models.dart';
import 'package:capitalism_app/features/leaderboard/leaderboard_service.dart';

class FakeLeaderboardService implements LeaderboardService {
  FakeLeaderboardService({
    this.players = const [],
    this.companies = const [],
    this.endgame,
    this.profiles = const {},
    this.badgesByPlayer = const {},
    this.rankHistoryByPlayer = const {},
    this.playersError,
    this.companiesError,
    this.profileError,
  });

  final List<PlayerRanking> players;
  final List<CompanyRanking> companies;
  final EndgameStatus? endgame;
  final Map<String, PlayerProfile> profiles;
  final Map<String, List<PlayerBadge>> badgesByPlayer;
  final Map<String, List<PlayerRankSnapshot>> rankHistoryByPlayer;
  final Object? playersError;
  final Object? companiesError;
  final Object? profileError;

  final List<String> calls = [];
  int companyRankingsCallCount = 0;

  @override
  Future<List<PlayerRanking>> fetchPlayerRankings() async {
    calls.add('fetchPlayerRankings');
    if (playersError != null) throw playersError!;
    return players;
  }

  @override
  Future<List<CompanyRanking>> fetchCompanyRankings() async {
    calls.add('fetchCompanyRankings');
    companyRankingsCallCount++;
    if (companiesError != null) throw companiesError!;
    return companies;
  }

  @override
  Future<EndgameStatus?> fetchEndgameStatus() async {
    calls.add('fetchEndgameStatus');
    return endgame;
  }

  @override
  Future<PlayerProfile?> fetchPlayerProfile(String playerId) async {
    calls.add('fetchPlayerProfile');
    if (profileError != null) throw profileError!;
    return profiles[playerId];
  }

  @override
  Future<List<PlayerBadge>> fetchPlayerBadges(String playerId) async {
    calls.add('fetchPlayerBadges');
    return badgesByPlayer[playerId] ?? const [];
  }

  @override
  Future<List<PlayerRankSnapshot>> fetchRankHistory(String playerId, {int ticksBack = 365}) async {
    calls.add('fetchRankHistory');
    return rankHistoryByPlayer[playerId] ?? const [];
  }
}
