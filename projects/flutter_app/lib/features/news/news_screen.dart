// Ported from `projects/frontend/src/views/NewsView.vue`. Renders entry
// bodies as plain text (see `stripHtmlToText`'s doc comment) rather than
// full rich HTML — a documented, deliberate simplification, not an
// oversight.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import '../../core/utils/html_text.dart';
import 'news_models.dart';
import 'news_service.dart';

const _pageSize = 10;
const _filters = ['ALL', 'NEWS', 'CHANGELOG', 'MARKET_REPORT'];

class NewsScreen extends StatefulWidget {
  const NewsScreen({super.key, GraphQlService? graphQlService, NewsService? newsService})
    : _injectedGraphQlService = graphQlService,
      _injectedNewsService = newsService;

  final GraphQlService? _injectedGraphQlService;
  final NewsService? _injectedNewsService;

  @override
  State<NewsScreen> createState() => _NewsScreenState();
}

class _NewsScreenState extends State<NewsScreen> {
  late final NewsService _service;
  late final bool _isAuthenticated;

  bool _loading = true;
  String? _error;
  GameNewsFeed? _feed;
  Set<String> _initiallyUnreadIds = {};
  String _filter = 'ALL';
  int _page = 0;
  bool _markingAllRead = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    _isAuthenticated = auth.isAuthenticated;
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedNewsService ?? NewsService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final feed = await _service.fetchFeed();
      if (!mounted) return;
      setState(() {
        _feed = feed;
        _initiallyUnreadIds = feed.items.where((e) => !e.isRead).map((e) => e.id).toSet();
        _loading = false;
      });

      final hasUnreadPublished = feed.items.any((e) => !e.isRead);
      if (_isAuthenticated && hasUnreadPublished) {
        // Fire-and-forget background mark-all-read, mirroring the web's
        // auto-mark-on-visit — failures are swallowed, matching the web.
        unawaited(_service.markAllRead());
      }
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the news feed. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _confirmMarkAllRead() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Mark all as read?'),
        content: const Text('This will mark every news entry as read.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(context).pop(true), child: const Text('Mark all read')),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _markingAllRead = true);
    try {
      await _service.markAllRead();
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(const SnackBar(content: Text('Could not mark all as read.')));
      }
    } finally {
      if (mounted) setState(() => _markingAllRead = false);
    }
  }

  List<GameNewsEntry> get _filteredEntries {
    final items = _feed?.items ?? const [];
    final filtered = _filter == 'ALL' ? items : items.where((e) => e.entryType == _filter).toList();
    return filtered;
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _load, child: const Text('Try again')),
            ],
          ),
        ),
      );
    }

    final entries = _filteredEntries;
    final pageCount = (entries.length / _pageSize).ceil().clamp(1, 1 << 30);
    final page = _page.clamp(0, pageCount - 1);
    final pageItems = entries.skip(page * _pageSize).take(_pageSize).toList();
    final locale = Localizations.localeOf(context).languageCode;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Row(
            children: [
              Expanded(child: Text('News', style: Theme.of(context).textTheme.headlineSmall)),
              OutlinedButton(
                onPressed: (!_isAuthenticated || (_feed?.unreadCount ?? 0) == 0 || _markingAllRead)
                    ? null
                    : _confirmMarkAllRead,
                child: const Text('Mark all read'),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            children: [
              for (final filter in _filters)
                ChoiceChip(
                  label: Text(filter),
                  selected: _filter == filter,
                  onSelected: (_) => setState(() {
                    _filter = filter;
                    _page = 0;
                  }),
                ),
            ],
          ),
          const SizedBox(height: 16),
          if (pageItems.isEmpty)
            Text(
              _filter == 'MARKET_REPORT' ? 'No market reports yet.' : 'No news entries yet.',
              style: Theme.of(context).textTheme.bodyMedium,
            )
          else
            for (final entry in pageItems) _NewsEntryCard(entry: entry, locale: locale, unread: _initiallyUnreadIds.contains(entry.id)),
          if (pageCount > 1) ...[
            const SizedBox(height: 16),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                IconButton(
                  onPressed: page > 0 ? () => setState(() => _page = page - 1) : null,
                  icon: const FaIcon(AppIcons.chevronLeft, size: 16),
                ),
                Text('${page + 1} / $pageCount'),
                IconButton(
                  onPressed: page < pageCount - 1 ? () => setState(() => _page = page + 1) : null,
                  icon: const FaIcon(AppIcons.chevronRight, size: 16),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _NewsEntryCard extends StatelessWidget {
  const _NewsEntryCard({required this.entry, required this.locale, required this.unread});

  final GameNewsEntry entry;
  final String locale;
  final bool unread;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final localization = entry.localizationFor(locale);
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Chip(label: Text(entry.entryType)),
                if (unread) ...[
                  const SizedBox(width: 8),
                  const Chip(label: Text('Unread')),
                ],
                const Spacer(),
                Text(
                  entry.publishedAtUtc ?? entry.updatedAtUtc ?? '',
                  style: theme.textTheme.bodySmall,
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(localization?.title ?? '(untitled)', style: theme.textTheme.titleMedium),
            const SizedBox(height: 4),
            Text(stripHtmlToText(localization?.htmlContent ?? ''), style: theme.textTheme.bodyMedium),
          ],
        ),
      ),
    );
  }
}
