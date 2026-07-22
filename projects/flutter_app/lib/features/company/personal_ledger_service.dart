import '../../core/graphql/graphql_service.dart';
import 'personal_ledger_models.dart';

const _personAccountQuery = r'''
  query PersonalLedgerAccount {
    personAccount {
      playerId displayName personalCash taxReserve availableCash totalNetWealth activeAccountType activeCompanyId
      shareholdings { companyId companyName shareCount ownershipRatio sharePrice marketValue }
      dividendPayments { id companyId companyName shareCount amountPerShare totalAmount gameYear recordedAtTick recordedAtUtc description }
      stockTrades { id companyId companyName direction shareCount pricePerShare totalValue recordedAtTick recordedAtUtc }
    }
  }
''';

/// GraphQL calls for the Personal Ledger screen, matching
/// `projects/frontend/src/views/PersonalLedgerView.vue`'s core
/// `personAccount` query. Deliberately not ported: the endgame race
/// progress bar/leaderboard and milestone toast notifications (both tied
/// to a separate `endgameStore`), and the interest-payments history table
/// with its ALL/INTEREST/DIVIDEND filter — see the screen's top-of-file
/// comment.
class PersonalLedgerService {
  const PersonalLedgerService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<PersonAccount?> fetchPersonAccount() async {
    final result = await _graphQlService.request(_personAccountQuery);
    final data = result['personAccount'] as Map<String, dynamic>?;
    return data == null ? null : PersonAccount.fromJson(data);
  }
}
