import '../../core/graphql/graphql_service.dart';
import 'news_models.dart';

const _feedQuery = r'''
  query GameNewsFeed($includeDrafts: Boolean!) {
    gameNewsFeed(includeDrafts: $includeDrafts) {
      unreadCount
      items {
        id entryType status publishedAtUtc updatedAtUtc isRead
        localizations { locale title summary htmlContent }
      }
    }
  }
''';

const _markAllReadMutation = r'''
  mutation MarkAllGameNewsRead {
    markAllGameNewsRead
  }
''';

/// GraphQL calls for the game-wide news feed, matching
/// `projects/frontend/src/stores/news.ts`'s exact operations (`includeDrafts`
/// is a required arg — always `false` here, matching the web's `NewsView`).
class NewsService {
  const NewsService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<GameNewsFeed> fetchFeed() async {
    final data = await _graphQlService.request(_feedQuery, variables: {'includeDrafts': false});
    return GameNewsFeed.fromJson(data['gameNewsFeed'] as Map<String, dynamic>);
  }

  /// Returns the number of entries marked read.
  Future<int> markAllRead() async {
    final data = await _graphQlService.request(_markAllReadMutation);
    return (data['markAllGameNewsRead'] as num?)?.toInt() ?? 0;
  }
}
