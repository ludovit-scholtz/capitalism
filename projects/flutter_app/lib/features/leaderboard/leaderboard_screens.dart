// Ported from `projects/frontend/src/views/LeaderboardView.vue`.
//
// Player Profile (`PlayerProfileView.vue` +
// `components/profile/PlayerProfileTabsContent.vue`) lives in its own
// `player_profile_screen.dart` — large enough (plus bio/display-name
// editing and the session-security panel) to warrant the same
// dedicated-file treatment as other split screens.
//
// The signed-in player's own row is now highlighted (background tint +
// "You" chip, matching the web's `isActivePlayer` styling) — the player id
// comes from `PlayerProfileService.fetchMyPlayerId()` (`me { id }`, reused
// from the Player Profile screen rather than duplicating the query).
//
// Tab and page are persisted to the URL (`?tab=&page=`, 1-indexed to match
// the web) via `context.go` on every change, and read back via the
// `initialTab`/`initialPage` constructor params the router passes from
// `state.uri.queryParameters` — matching `LeaderboardView.vue`'s
// `getInitialTab`/`parsePageQuery`, including auto-jumping to the page
// containing the player's own rank when no explicit `page` param is given
// (`calculateRankPage`). Trimmed: no external "View master ranking" link.

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import 'leaderboard_format.dart';
import 'leaderboard_models.dart';
import 'leaderboard_service.dart';
import 'player_profile_service.dart';

export 'leaderboard_format.dart' show formatCompactWealth;
export 'player_profile_screen.dart' show PlayerProfileScreen;

const _pageSize = 10;

class LeaderboardScreen extends StatefulWidget {
  const LeaderboardScreen({
    super.key,
    this.initialTab,
    this.initialPage,
    GraphQlService? graphQlService,
    LeaderboardService? leaderboardService,
    PlayerProfileService? playerProfileService,
  }) : _injectedGraphQlService = graphQlService,
       _injectedLeaderboardService = leaderboardService,
       _injectedPlayerProfileService = playerProfileService;

  /// `'players'` or `'companies'` — from the URL's `?tab=` query param.
  final String? initialTab;

  /// 1-indexed, matching the web — from the URL's `?page=` query param.
  final int? initialPage;

  final GraphQlService? _injectedGraphQlService;
  final LeaderboardService? _injectedLeaderboardService;
  final PlayerProfileService? _injectedPlayerProfileService;

  @override
  State<LeaderboardScreen> createState() => _LeaderboardScreenState();
}

class _LeaderboardScreenState extends State<LeaderboardScreen> {
  late final LeaderboardService _service;
  late final PlayerProfileService _profileService;

  late String _tab;
  late int _page;
  bool _hasExplicitPage = false;
  String? _myPlayerId;

  bool _playerLoading = true;
  String? _playerError;
  List<PlayerRanking> _players = const [];

  bool _companyLoading = false;
  bool _companyLoaded = false;
  String? _companyError;
  List<CompanyRanking> _companies = const [];

  EndgameStatus? _endgame;

  @override
  void initState() {
    super.initState();
    _tab = widget.initialTab == 'companies' ? 'companies' : 'players';
    _hasExplicitPage = widget.initialPage != null;
    _page = ((widget.initialPage ?? 1) - 1).clamp(0, 1 << 30);
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedLeaderboardService ?? LeaderboardService(graphQlService);
    _profileService = widget._injectedPlayerProfileService ?? PlayerProfileService(graphQlService, auth);
    _loadPlayers();
    _loadEndgame();
    _loadMyPlayerId();
    if (_tab == 'companies') _loadCompanies();
  }

  Future<void> _loadMyPlayerId() async {
    final id = await _profileService.fetchMyPlayerId();
    if (mounted) setState(() => _myPlayerId = id);
  }

  Future<void> _loadPlayers() async {
    setState(() {
      _playerLoading = true;
      _playerError = null;
    });
    try {
      final players = await _service.fetchPlayerRankings();
      if (!mounted) return;
      setState(() {
        _players = players;
        _playerLoading = false;
      });
      _maybeJumpToOwnPage();
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _playerError = 'Could not load the leaderboard. Please try again.';
        _playerLoading = false;
      });
    }
  }

  Future<void> _loadCompanies() async {
    setState(() {
      _companyLoading = true;
      _companyError = null;
    });
    try {
      final companies = await _service.fetchCompanyRankings();
      if (!mounted) return;
      setState(() {
        _companies = companies;
        _companyLoaded = true;
        _companyLoading = false;
      });
      _maybeJumpToOwnPage();
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _companyError = 'Could not load the leaderboard. Please try again.';
        _companyLoading = false;
      });
    }
  }

  Future<void> _loadEndgame() async {
    final endgame = await _service.fetchEndgameStatus();
    if (mounted) setState(() => _endgame = endgame);
  }

  /// Matches `calculateRankPage`/the web's "jump to my own row" behavior on
  /// first load — only when the URL didn't already specify a page.
  void _maybeJumpToOwnPage() {
    final myPlayerId = _myPlayerId;
    if (_hasExplicitPage || myPlayerId == null) return;
    final rows = _tab == 'companies' ? _companies.map((c) => c.playerId) : _players.map((p) => p.playerId);
    final index = rows.toList().indexOf(myPlayerId);
    if (index < 0) return;
    setState(() => _page = index ~/ _pageSize);
  }

  void _selectTab(String tab) {
    setState(() {
      _tab = tab;
      _page = 0;
      _hasExplicitPage = false;
    });
    if (tab == 'companies' && !_companyLoaded && !_companyLoading) {
      _loadCompanies();
    } else {
      _maybeJumpToOwnPage();
    }
    _syncUrl();
  }

  void _selectPage(int page) {
    setState(() {
      _page = page;
      _hasExplicitPage = true;
    });
    _syncUrl();
  }

  void _syncUrl() {
    final uri = Uri(path: '/leaderboard', queryParameters: {'tab': _tab, 'page': '${_page + 1}'});
    context.go(uri.toString());
  }

  int get _totalRows => _tab == 'companies' ? _companies.length : _players.length;
  int get _totalPages => (_totalRows / _pageSize).ceil().clamp(1, 1 << 30);

  @override
  Widget build(BuildContext context) {
    final page = _page.clamp(0, _totalPages - 1);

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Leaderboard', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text('See how you stack up against the rest of the world.', style: Theme.of(context).textTheme.bodyMedium),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(
              child: ChoiceChip(
                label: const Text('Players'),
                selected: _tab == 'players',
                onSelected: (_) => _selectTab('players'),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: ChoiceChip(
                label: const Text('Companies'),
                selected: _tab == 'companies',
                onSelected: (_) => _selectTab('companies'),
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        if (_endgame != null) _EndgameBenchmarkCard(endgame: _endgame!, topPlayer: _players.isNotEmpty ? _players.first : null),
        if (_endgame != null) const SizedBox(height: 16),
        if (_tab == 'players') ..._buildPlayersTab(page) else ..._buildCompaniesTab(page),
      ],
    );
  }

  List<Widget> _buildPlayersTab(int page) {
    if (_playerLoading) {
      return const [Padding(padding: EdgeInsets.symmetric(vertical: 48), child: Center(child: CircularProgressIndicator()))];
    }
    if (_playerError != null) {
      return [
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 24),
          child: Column(
            children: [
              Text(_playerError!),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _loadPlayers, child: const Text('Try again')),
            ],
          ),
        ),
      ];
    }
    if (_players.isEmpty) {
      return const [Padding(padding: EdgeInsets.symmetric(vertical: 24), child: Text('No players on the leaderboard yet.'))];
    }
    final pageItems = _players.skip(page * _pageSize).take(_pageSize).toList();
    return [
      for (var i = 0; i < pageItems.length; i++)
        _PlayerRankCard(rank: page * _pageSize + i + 1, player: pageItems[i], isOwnRow: pageItems[i].playerId == _myPlayerId),
      _buildPager(page),
    ];
  }

  List<Widget> _buildCompaniesTab(int page) {
    if (_companyLoading) {
      return const [Padding(padding: EdgeInsets.symmetric(vertical: 48), child: Center(child: CircularProgressIndicator()))];
    }
    if (_companyError != null) {
      return [
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 24),
          child: Column(
            children: [
              Text(_companyError!),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _loadCompanies, child: const Text('Try again')),
            ],
          ),
        ),
      ];
    }
    if (_companies.isEmpty) {
      return const [Padding(padding: EdgeInsets.symmetric(vertical: 24), child: Text('No companies on the leaderboard yet.'))];
    }
    final pageItems = _companies.skip(page * _pageSize).take(_pageSize).toList();
    return [
      for (var i = 0; i < pageItems.length; i++)
        _CompanyRankCard(rank: page * _pageSize + i + 1, company: pageItems[i], isOwnRow: pageItems[i].playerId == _myPlayerId),
      _buildPager(page),
    ];
  }

  Widget _buildPager(int page) {
    if (_totalPages <= 1) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          IconButton(onPressed: page > 0 ? () => _selectPage(page - 1) : null, icon: const FaIcon(AppIcons.chevronLeft, size: 16)),
          Text('${page + 1} / $_totalPages'),
          IconButton(
            onPressed: page < _totalPages - 1 ? () => _selectPage(page + 1) : null,
            icon: const FaIcon(AppIcons.chevronRight, size: 16),
          ),
        ],
      ),
    );
  }
}

class _EndgameBenchmarkCard extends StatelessWidget {
  const _EndgameBenchmarkCard({required this.endgame, required this.topPlayer});

  final EndgameStatus endgame;
  final PlayerRanking? topPlayer;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('🏆 Race to the top', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Text('Target: ${formatCompactWealth(context, endgame.winningThresholdUsd)}'),
            if (topPlayer != null) ...[
              const SizedBox(height: 8),
              Text('${topPlayer!.alias} is leading with ${formatCompactWealth(context, topPlayer!.totalWealthUsd)}'),
            ],
          ],
        ),
      ),
    );
  }
}

class _PlayerRankCard extends StatelessWidget {
  const _PlayerRankCard({required this.rank, required this.player, this.isOwnRow = false});

  final int rank;
  final PlayerRanking player;
  final bool isOwnRow;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      key: ValueKey('player-rank-${player.playerId}'),
      margin: const EdgeInsets.only(bottom: 8),
      color: isOwnRow ? theme.colorScheme.primaryContainer : null,
      shape: isOwnRow ? RoundedRectangleBorder(side: BorderSide(color: theme.colorScheme.primary, width: 2), borderRadius: BorderRadius.circular(12)) : null,
      child: ListTile(
        onTap: () => context.go('/player/${player.playerId}'),
        leading: SizedBox(
          width: 40,
          child: Center(child: Text(rankBadge(rank), style: theme.textTheme.titleMedium)),
        ),
        title: Row(
          children: [
            Flexible(child: Text(player.alias, overflow: TextOverflow.ellipsis)),
            for (final badge in player.badgeTypes.take(3)) Padding(padding: const EdgeInsets.only(left: 4), child: Text(profileBadgeIcon(badge))),
            if (isOwnRow)
              const Padding(
                padding: EdgeInsets.only(left: 6),
                child: Chip(
                  key: ValueKey('own-row-you-chip'),
                  label: Text('You', style: TextStyle(fontSize: 11)),
                  visualDensity: VisualDensity.compact,
                  materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                ),
              ),
          ],
        ),
        subtitle: Text('${player.companyCount} companies'),
        trailing: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Text(formatCompactWealth(context, player.totalWealthUsd), style: theme.textTheme.titleSmall),
            Text(
              '💵 ${formatCompactWealth(context, player.personalCash)} · 📈 ${formatCompactWealth(context, player.sharesValue)}',
              style: theme.textTheme.bodySmall,
            ),
          ],
        ),
      ),
    );
  }
}

class _CompanyRankCard extends StatelessWidget {
  const _CompanyRankCard({required this.rank, required this.company, this.isOwnRow = false});

  final int rank;
  final CompanyRanking company;
  final bool isOwnRow;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      key: ValueKey('company-rank-${company.companyId}'),
      margin: const EdgeInsets.only(bottom: 8),
      color: isOwnRow ? theme.colorScheme.primaryContainer : null,
      shape: isOwnRow ? RoundedRectangleBorder(side: BorderSide(color: theme.colorScheme.primary, width: 2), borderRadius: BorderRadius.circular(12)) : null,
      child: ListTile(
        onTap: () => context.go('/player/${company.playerId}'),
        leading: SizedBox(
          width: 40,
          child: Center(child: Text(rankBadge(rank), style: theme.textTheme.titleMedium)),
        ),
        title: Row(
          children: [
            Flexible(child: Text(company.companyName, overflow: TextOverflow.ellipsis)),
            if (isOwnRow)
              const Padding(
                padding: EdgeInsets.only(left: 6),
                child: Chip(
                  key: ValueKey('own-row-you-chip'),
                  label: Text('You', style: TextStyle(fontSize: 11)),
                  visualDensity: VisualDensity.compact,
                  materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                ),
              ),
          ],
        ),
        subtitle: Text('Owned by ${company.ownerAlias} · ${company.buildingCount} buildings'),
        trailing: Text(formatCompactWealth(context, company.totalWealthUsd), style: theme.textTheme.titleSmall),
      ),
    );
  }
}
