// Ported from `projects/frontend/src/views/LeaderboardView.vue` and
// `PlayerProfileView.vue` (+ `components/profile/PlayerProfileTabsContent.vue`).
//
// Deliberately trimmed from the web (all documented, not oversights):
// - No "active player" highlighting / "You" badge on leaderboard rows — the
//   web compares against `auth.player.id`, but `AuthState` here only stores
//   the bearer token, not the decoded player id (nothing else needed it yet).
// - No external "View master ranking" link and no page-query-param
//   persistence (`?tab=&page=`) — mobile navigation doesn't share URLs the
//   same way.
// - Player Profile: bio/display-name editing and the session-security panel
//   (list active sessions, log out all devices) are read-only-profile web
//   features that write via the Master API and REST session endpoints — not
//   ported; this screen only ever renders someone else's or your own public
//   profile, matching the "view" half of the web screen.
// - Rank History tab renders the same snapshot data as a simple list instead
//   of `RankHistoryChart.vue`'s SVG chart (no charting dependency added).
// - CSV/PDF stats export (`exportCsv`/`exportPdf`) isn't ported — desktop
//   file-download/print flows with no direct mobile equivalent.

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import 'leaderboard_models.dart';
import 'leaderboard_service.dart';

String _formatCompact(double value) {
  final absValue = value.abs();
  if (absValue >= 1e9) return '\$${(value / 1e9).toStringAsFixed(2)}B';
  if (absValue >= 1e6) return '\$${(value / 1e6).toStringAsFixed(2)}M';
  if (absValue >= 1e3) return '\$${(value / 1e3).toStringAsFixed(1)}K';
  return '\$${value.toStringAsFixed(0)}';
}

const _pageSize = 10;

class LeaderboardScreen extends StatefulWidget {
  const LeaderboardScreen({super.key, GraphQlService? graphQlService, LeaderboardService? leaderboardService})
    : _injectedGraphQlService = graphQlService,
      _injectedLeaderboardService = leaderboardService;

  final GraphQlService? _injectedGraphQlService;
  final LeaderboardService? _injectedLeaderboardService;

  @override
  State<LeaderboardScreen> createState() => _LeaderboardScreenState();
}

class _LeaderboardScreenState extends State<LeaderboardScreen> {
  late final LeaderboardService _service;

  String _tab = 'players';
  int _page = 0;

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
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedLeaderboardService ?? LeaderboardService(graphQlService);
    _loadPlayers();
    _loadEndgame();
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

  void _selectTab(String tab) {
    setState(() {
      _tab = tab;
      _page = 0;
    });
    if (tab == 'companies' && !_companyLoaded && !_companyLoading) {
      _loadCompanies();
    }
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
        _PlayerRankCard(rank: page * _pageSize + i + 1, player: pageItems[i]),
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
        _CompanyRankCard(rank: page * _pageSize + i + 1, company: pageItems[i]),
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
          IconButton(onPressed: page > 0 ? () => setState(() => _page = page - 1) : null, icon: const FaIcon(AppIcons.chevronLeft, size: 16)),
          Text('${page + 1} / $_totalPages'),
          IconButton(
            onPressed: page < _totalPages - 1 ? () => setState(() => _page = page + 1) : null,
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
            Text('Target: ${_formatCompact(endgame.winningThresholdUsd)}'),
            if (topPlayer != null) ...[
              const SizedBox(height: 8),
              Text('${topPlayer!.alias} is leading with ${_formatCompact(topPlayer!.totalWealthUsd)}'),
            ],
          ],
        ),
      ),
    );
  }
}

class _PlayerRankCard extends StatelessWidget {
  const _PlayerRankCard({required this.rank, required this.player});

  final int rank;
  final PlayerRanking player;

  @override
  Widget build(BuildContext context) {
    return Card(
      key: ValueKey('player-rank-${player.playerId}'),
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        onTap: () => context.go('/player/${player.playerId}'),
        leading: SizedBox(
          width: 40,
          child: Center(child: Text(rankBadge(rank), style: Theme.of(context).textTheme.titleMedium)),
        ),
        title: Row(
          children: [
            Flexible(child: Text(player.alias, overflow: TextOverflow.ellipsis)),
            for (final badge in player.badgeTypes.take(3)) Padding(padding: const EdgeInsets.only(left: 4), child: Text(profileBadgeIcon(badge))),
          ],
        ),
        subtitle: Text('${player.companyCount} companies'),
        trailing: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Text(_formatCompact(player.totalWealthUsd), style: Theme.of(context).textTheme.titleSmall),
            Text('💵 ${_formatCompact(player.personalCash)} · 📈 ${_formatCompact(player.sharesValue)}', style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}

class _CompanyRankCard extends StatelessWidget {
  const _CompanyRankCard({required this.rank, required this.company});

  final int rank;
  final CompanyRanking company;

  @override
  Widget build(BuildContext context) {
    return Card(
      key: ValueKey('company-rank-${company.companyId}'),
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        onTap: () => context.go('/player/${company.playerId}'),
        leading: SizedBox(
          width: 40,
          child: Center(child: Text(rankBadge(rank), style: Theme.of(context).textTheme.titleMedium)),
        ),
        title: Text(company.companyName, overflow: TextOverflow.ellipsis),
        subtitle: Text('Owned by ${company.ownerAlias} · ${company.buildingCount} buildings'),
        trailing: Text(_formatCompact(company.totalWealthUsd), style: Theme.of(context).textTheme.titleSmall),
      ),
    );
  }
}

class PlayerProfileScreen extends StatefulWidget {
  const PlayerProfileScreen({
    super.key,
    required this.playerId,
    GraphQlService? graphQlService,
    LeaderboardService? leaderboardService,
  }) : _injectedGraphQlService = graphQlService,
       _injectedLeaderboardService = leaderboardService;

  final String playerId;
  final GraphQlService? _injectedGraphQlService;
  final LeaderboardService? _injectedLeaderboardService;

  @override
  State<PlayerProfileScreen> createState() => _PlayerProfileScreenState();
}

class _PlayerProfileScreenState extends State<PlayerProfileScreen> {
  late final LeaderboardService _service;

  bool _loading = true;
  String? _error;
  PlayerProfile? _profile;

  String _tab = 'overview';
  bool _badgesLoading = false;
  bool _badgesLoaded = false;
  List<PlayerBadge> _badges = const [];
  bool _rankHistoryLoading = false;
  bool _rankHistoryLoaded = false;
  List<PlayerRankSnapshot> _rankHistory = const [];

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedLeaderboardService ?? LeaderboardService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final profile = await _service.fetchPlayerProfile(widget.playerId);
      if (!mounted) return;
      setState(() {
        _profile = profile;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load this profile. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _selectTab(String tab) async {
    setState(() => _tab = tab);
    if (tab == 'achievements' && !_badgesLoaded && !_badgesLoading) {
      setState(() => _badgesLoading = true);
      try {
        final badges = await _service.fetchPlayerBadges(widget.playerId);
        if (mounted) setState(() => _badges = badges);
      } catch (_) {
        // Swallowed, matching the web's silent badges-empty fallback.
      } finally {
        if (mounted) {
          setState(() {
            _badgesLoaded = true;
            _badgesLoading = false;
          });
        }
      }
    } else if (tab == 'rank-history' && !_rankHistoryLoaded && !_rankHistoryLoading) {
      setState(() => _rankHistoryLoading = true);
      try {
        final history = await _service.fetchRankHistory(widget.playerId);
        if (mounted) setState(() => _rankHistory = history);
      } catch (_) {
        // Swallowed, matching the web's silent rank-history-empty fallback.
      } finally {
        if (mounted) {
          setState(() {
            _rankHistoryLoaded = true;
            _rankHistoryLoading = false;
          });
        }
      }
    }
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
    final profile = _profile;
    if (profile == null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('Player not found.'),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: () => context.go('/leaderboard'), child: const Text('Back to leaderboard')),
            ],
          ),
        ),
      );
    }

    final theme = Theme.of(context);
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Text(profile.displayName, style: theme.textTheme.headlineSmall),
            ),
            if (profile.hasProSubscription) const Chip(label: Text('⭐ Pro')),
          ],
        ),
        Text('Joined ${profile.joinGameYear}', style: theme.textTheme.bodySmall),
        if (profile.leaderboardRank > 0) ...[
          const SizedBox(height: 8),
          Text(rankBadge(profile.leaderboardRank), style: theme.textTheme.titleLarge),
        ],
        if (profile.bio != null && profile.bio!.isNotEmpty) ...[
          const SizedBox(height: 8),
          Text('"${profile.bio}"', style: theme.textTheme.bodyMedium!.copyWith(fontStyle: FontStyle.italic)),
        ],
        const SizedBox(height: 20),
        _QuickStatsRow(profile: profile),
        const SizedBox(height: 20),
        Row(
          children: [
            for (final entry in const {'overview': '📊 Overview', 'achievements': '🏅 Achievements', 'rank-history': '📈 Rank History'}.entries)
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 2),
                  child: ChoiceChip(label: Text(entry.value), selected: _tab == entry.key, onSelected: (_) => _selectTab(entry.key)),
                ),
              ),
          ],
        ),
        const SizedBox(height: 16),
        if (_tab == 'overview') _OverviewTab(profile: profile),
        if (_tab == 'achievements') _AchievementsTab(loading: _badgesLoading, badges: _badges),
        if (_tab == 'rank-history') _RankHistoryTab(loading: _rankHistoryLoading, snapshots: _rankHistory),
        const SizedBox(height: 20),
        Center(
          child: TextButton(onPressed: () => context.go('/leaderboard'), child: const Text('← Back to leaderboard')),
        ),
      ],
    );
  }
}

class _QuickStatsRow extends StatelessWidget {
  const _QuickStatsRow({required this.profile});

  final PlayerProfile profile;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    Widget stat(String value, String label) => Expanded(
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            children: [
              Text(value, style: theme.textTheme.titleMedium),
              Text(label, style: theme.textTheme.labelSmall),
            ],
          ),
        ),
      ),
    );
    return Row(
      children: [
        stat(profile.leaderboardRank > 0 ? rankBadge(profile.leaderboardRank) : '—', 'Global rank'),
        const SizedBox(width: 8),
        stat(_formatCompact(profile.totalWealthUsd), 'Total wealth'),
        const SizedBox(width: 8),
        stat('${profile.companyCount}', 'Companies'),
        const SizedBox(width: 8),
        stat('${profile.citiesWithBuildings}', 'Cities'),
      ],
    );
  }
}

class _OverviewTab extends StatelessWidget {
  const _OverviewTab({required this.profile});

  final PlayerProfile profile;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final hof = profile.hallOfFame;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('🏭 INDUSTRIES', style: theme.textTheme.labelLarge),
                const SizedBox(height: 8),
                if (profile.activeBuildingTypes.isEmpty)
                  const Text('No active industries yet.')
                else
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [for (final type in profile.activeBuildingTypes) Chip(label: Text(type))],
                  ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('📦 SALES STATS', style: theme.textTheme.labelLarge),
                const SizedBox(height: 8),
                Text('Total products sold: ${profile.totalProductsSold.toStringAsFixed(0)}'),
                Text('Company equity: ${_formatCompact(profile.totalCompanyEquityUsd)}'),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('🏆 HALL OF FAME', style: theme.textTheme.labelLarge),
                const SizedBox(height: 8),
                Text('Highest single-tick revenue: ${hof.highestSingleTickRevenue > 0 ? _formatCompact(hof.highestSingleTickRevenue) : '—'}'),
                Text('Largest acquisition: ${hof.largestBuildingAcquisitionPrice > 0 ? _formatCompact(hof.largestBuildingAcquisitionPrice) : '—'}'),
                Text('Highest brand quality: ${hof.highestBrandQuality > 0 ? '${(hof.highestBrandQuality * 100).round()}%' : '—'}'),
                Text('Account age: ${hof.accountAgeTicks} ticks'),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _AchievementsTab extends StatelessWidget {
  const _AchievementsTab({required this.loading, required this.badges});

  final bool loading;
  final List<PlayerBadge> badges;

  @override
  Widget build(BuildContext context) {
    if (loading) return const Center(child: CircularProgressIndicator());
    if (badges.isEmpty) return const Text('No badges unlocked yet.');
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        for (final badge in badges)
          Chip(avatar: Text(profileBadgeIcon(badge.badgeType)), label: Text(badge.badgeType)),
      ],
    );
  }
}

class _RankHistoryTab extends StatelessWidget {
  const _RankHistoryTab({required this.loading, required this.snapshots});

  final bool loading;
  final List<PlayerRankSnapshot> snapshots;

  @override
  Widget build(BuildContext context) {
    if (loading) return const Center(child: CircularProgressIndicator());
    if (snapshots.isEmpty) return const Text('No rank history yet.');
    return Column(
      children: [
        for (final snapshot in snapshots.reversed)
          ListTile(
            dense: true,
            title: Text('Rank #${snapshot.leaderboardRank} · ${_formatCompact(snapshot.wealthUsd)}'),
            subtitle: Text('Tick ${snapshot.snapshotTick}'),
            trailing: snapshot.positionChange == null
                ? null
                : Text(snapshot.positionChange! > 0 ? '▲${snapshot.positionChange}' : (snapshot.positionChange! < 0 ? '▼${-snapshot.positionChange!}' : '–')),
          ),
      ],
    );
  }
}
