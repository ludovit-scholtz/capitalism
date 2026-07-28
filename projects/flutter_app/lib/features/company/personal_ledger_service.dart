import '../../core/graphql/graphql_service.dart';
import 'personal_ledger_models.dart';

const _personAccountQuery = r'''
  query PersonalLedgerAccount {
    personAccount {
      playerId displayName personalCash taxReserve availableCash totalNetWealth activeAccountType activeCompanyId
      shareholdings { companyId companyName shareCount ownershipRatio sharePrice marketValue }
      interestPayments { id companyId companyName bankBuildingId bankBuildingName amount recordedAtTick recordedAtUtc currencyCode description }
      dividendPayments { id companyId companyName shareCount amountPerShare totalAmount gameYear recordedAtTick recordedAtUtc description }
      stockTrades { id companyId companyName direction shareCount pricePerShare totalValue recordedAtTick recordedAtUtc }
    }
  }
''';

/// GraphQL calls for the Personal Ledger screen, matching
/// `projects/frontend/src/views/PersonalLedgerView.vue`'s core
/// `personAccount` query. The endgame race progress/leaderboard section is
/// fed by `LeaderboardService.fetchEndgameStatus()` (reused rather than
/// duplicated — same `endgameStatus` query the Leaderboard screen already
/// uses), matching the web's separate `endgameStore`.
class PersonalLedgerService {
  const PersonalLedgerService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<PersonAccount?> fetchPersonAccount() async {
    final result = await _graphQlService.request(_personAccountQuery);
    final data = result['personAccount'] as Map<String, dynamic>?;
    return data == null ? null : PersonAccount.fromJson(data);
  }
}
