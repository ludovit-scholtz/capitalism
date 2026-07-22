import 'package:capitalism_app/features/news/news_models.dart';
import 'package:capitalism_app/features/news/news_service.dart';

class FakeNewsService implements NewsService {
  FakeNewsService({this.feed = const GameNewsFeed(unreadCount: 0, items: []), this.fetchError});

  final GameNewsFeed feed;
  final Object? fetchError;

  final List<String> calls = [];
  int markAllReadCallCount = 0;

  @override
  Future<GameNewsFeed> fetchFeed() async {
    calls.add('fetchFeed');
    if (fetchError != null) throw fetchError!;
    return feed;
  }

  @override
  Future<int> markAllRead() async {
    calls.add('markAllRead');
    markAllReadCallCount++;
    return 0;
  }
}
