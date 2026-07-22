// Data models for the game-wide news/changelog feed, mirroring
// `projects/frontend/src/views/NewsView.vue`. GraphQL field names verified
// against `projects/Api/Types/Query.News.cs` / `Mutation.Admin.cs`.

class GameNewsLocalization {
  const GameNewsLocalization({required this.locale, required this.title, required this.summary, required this.htmlContent});

  final String locale;
  final String title;
  final String? summary;
  final String htmlContent;

  factory GameNewsLocalization.fromJson(Map<String, dynamic> json) => GameNewsLocalization(
    locale: json['locale'] as String,
    title: (json['title'] as String?) ?? '',
    summary: json['summary'] as String?,
    htmlContent: (json['htmlContent'] as String?) ?? '',
  );
}

class GameNewsEntry {
  const GameNewsEntry({
    required this.id,
    required this.entryType,
    required this.publishedAtUtc,
    required this.updatedAtUtc,
    required this.isRead,
    required this.localizations,
  });

  final String id;

  /// `NEWS`, `CHANGELOG`, or `MARKET_REPORT`.
  final String entryType;
  final String? publishedAtUtc;
  final String? updatedAtUtc;
  final bool isRead;
  final List<GameNewsLocalization> localizations;

  /// Mirrors `pickGamesLocalization`'s fallback order: requested locale →
  /// `en` → first available.
  GameNewsLocalization? localizationFor(String locale) {
    for (final candidate in localizations) {
      if (candidate.locale == locale) return candidate;
    }
    for (final candidate in localizations) {
      if (candidate.locale == 'en') return candidate;
    }
    return localizations.isEmpty ? null : localizations.first;
  }

  factory GameNewsEntry.fromJson(Map<String, dynamic> json) => GameNewsEntry(
    id: json['id'] as String,
    entryType: (json['entryType'] as String?) ?? 'NEWS',
    publishedAtUtc: json['publishedAtUtc'] as String?,
    updatedAtUtc: json['updatedAtUtc'] as String?,
    isRead: json['isRead'] as bool? ?? false,
    localizations: ((json['localizations'] as List<dynamic>?) ?? const [])
        .map((l) => GameNewsLocalization.fromJson(l as Map<String, dynamic>))
        .toList(),
  );
}

class GameNewsFeed {
  const GameNewsFeed({required this.unreadCount, required this.items});

  final int unreadCount;
  final List<GameNewsEntry> items;

  factory GameNewsFeed.fromJson(Map<String, dynamic> json) => GameNewsFeed(
    unreadCount: (json['unreadCount'] as num?)?.toInt() ?? 0,
    items: ((json['items'] as List<dynamic>?) ?? const [])
        .map((e) => GameNewsEntry.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}
