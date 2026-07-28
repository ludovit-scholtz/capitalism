// Ported from `projects/frontend/src/views/PlayerProfileView.vue` (+
// `components/profile/PlayerProfileTabsContent.vue`) — factored out of
// `leaderboard_screens.dart` to keep that file under the 500-line budget.
// Now includes bio/display-name editing and the session-security panel
// (list active sessions, log out other devices) for the signed-in
// player's own profile — previously trimmed, documented in the prior
// top-of-file comment in `leaderboard_screens.dart`.
//
// Trimmed: no `GenderPicker`/regenerate-random-name flow for the display
// name (plain text editing only); Rank History tab renders the same
// snapshot data as a simple list instead of `RankHistoryChart.vue`'s SVG
// chart; no CSV/PDF stats export — desktop file-download/print flows with
// no direct mobile equivalent.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'leaderboard_models.dart';
import 'leaderboard_service.dart';
import 'player_profile_service.dart';
import 'player_profile_session_panel.dart';
import 'player_profile_tabs.dart';

class PlayerProfileScreen extends StatefulWidget {
  const PlayerProfileScreen({
    super.key,
    required this.playerId,
    GraphQlService? graphQlService,
    LeaderboardService? leaderboardService,
    PlayerProfileService? playerProfileService,
  }) : _injectedGraphQlService = graphQlService,
       _injectedLeaderboardService = leaderboardService,
       _injectedPlayerProfileService = playerProfileService;

  final String playerId;
  final GraphQlService? _injectedGraphQlService;
  final LeaderboardService? _injectedLeaderboardService;
  final PlayerProfileService? _injectedPlayerProfileService;

  @override
  State<PlayerProfileScreen> createState() => _PlayerProfileScreenState();
}

class _PlayerProfileScreenState extends State<PlayerProfileScreen> {
  late final LeaderboardService _service;
  late final PlayerProfileService _profileService;

  bool _loading = true;
  String? _error;
  PlayerProfile? _profile;
  bool _isOwnProfile = false;

  String _tab = 'overview';
  bool _badgesLoading = false;
  bool _badgesLoaded = false;
  List<PlayerBadge> _badges = const [];
  bool _rankHistoryLoading = false;
  bool _rankHistoryLoaded = false;
  List<PlayerRankSnapshot> _rankHistory = const [];

  bool _editingBio = false;
  final _bioController = TextEditingController();
  bool _bioSaving = false;
  String? _bioError;

  bool _editingDisplayName = false;
  final _displayNameController = TextEditingController();
  bool _displayNameSaving = false;
  String? _displayNameError;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedLeaderboardService ?? LeaderboardService(graphQlService);
    _profileService = widget._injectedPlayerProfileService ?? PlayerProfileService(graphQlService, auth);
    _load();
  }

  @override
  void dispose() {
    _bioController.dispose();
    _displayNameController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([_service.fetchPlayerProfile(widget.playerId), _profileService.fetchMyPlayerId()]);
      if (!mounted) return;
      final profile = results[0] as PlayerProfile?;
      final myPlayerId = results[1] as String?;
      setState(() {
        _profile = profile;
        _isOwnProfile = myPlayerId != null && myPlayerId == widget.playerId;
        _bioController.text = profile?.bio ?? '';
        _displayNameController.text = profile?.displayName ?? '';
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

  Future<void> _saveBio() async {
    setState(() {
      _bioSaving = true;
      _bioError = null;
    });
    try {
      final bio = await _profileService.updateBio(_bioController.text.trim().isEmpty ? null : _bioController.text.trim());
      if (!mounted) return;
      setState(() {
        _profile = _profile == null ? null : _ProfileBioCopy.withBio(_profile!, bio);
        _editingBio = false;
        _bioSaving = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _bioError = 'Could not save your bio. Please try again.';
        _bioSaving = false;
      });
    }
  }

  Future<void> _saveDisplayName() async {
    final trimmed = _displayNameController.text.trim();
    if (trimmed.isEmpty) return;
    setState(() {
      _displayNameSaving = true;
      _displayNameError = null;
    });
    try {
      final displayName = await _profileService.updateDisplayName(trimmed);
      if (!mounted) return;
      setState(() {
        _profile = _profile == null ? null : _ProfileBioCopy.withDisplayName(_profile!, displayName);
        _editingDisplayName = false;
        _displayNameSaving = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Display name updated.')));
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _displayNameError = 'Could not save your display name. Please try again.';
        _displayNameSaving = false;
      });
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
        if (_isOwnProfile) ...[const SizedBox(height: 12), _displayNameSection(theme)],
        const SizedBox(height: 8),
        _bioSection(theme, profile),
        if (_isOwnProfile) ...[
          const SizedBox(height: 16),
          PlayerProfileSessionPanel(service: _profileService),
        ],
        const SizedBox(height: 20),
        QuickStatsRow(profile: profile),
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
        if (_tab == 'overview') OverviewTab(profile: profile),
        if (_tab == 'achievements') AchievementsTab(loading: _badgesLoading, badges: _badges),
        if (_tab == 'rank-history') RankHistoryTab(loading: _rankHistoryLoading, snapshots: _rankHistory),
        const SizedBox(height: 20),
        Center(
          child: TextButton(onPressed: () => context.go('/leaderboard'), child: const Text('← Back to leaderboard')),
        ),
      ],
    );
  }

  Widget _displayNameSection(ThemeData theme) {
    if (!_editingDisplayName) {
      return Center(
        child: TextButton(
          key: const ValueKey('edit-display-name-button'),
          onPressed: () => setState(() => _editingDisplayName = true),
          child: const Text('Edit display name'),
        ),
      );
    }
    return Column(
      children: [
        TextField(
          key: const ValueKey('display-name-field'),
          controller: _displayNameController,
          maxLength: 40,
          decoration: const InputDecoration(labelText: 'Display name'),
        ),
        const Text(
          'Do not use your real name — pick a fictional alias.',
          style: TextStyle(fontSize: 11, color: Colors.amber),
        ),
        if (_displayNameError != null) Text(_displayNameError!, style: TextStyle(color: theme.colorScheme.error)),
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            FilledButton(
              onPressed: _displayNameSaving ? null : _saveDisplayName,
              child: Text(_displayNameSaving ? 'Saving…' : 'Save'),
            ),
            const SizedBox(width: 8),
            OutlinedButton(
              onPressed: () => setState(() {
                _editingDisplayName = false;
                _displayNameController.text = _profile?.displayName ?? '';
                _displayNameError = null;
              }),
              child: const Text('Cancel'),
            ),
          ],
        ),
      ],
    );
  }

  Widget _bioSection(ThemeData theme, PlayerProfile profile) {
    if (!_editingBio) {
      return Center(
        child: Wrap(
          alignment: WrapAlignment.center,
          crossAxisAlignment: WrapCrossAlignment.center,
          spacing: 8,
          children: [
            if (profile.bio != null && profile.bio!.isNotEmpty)
              Text('"${profile.bio}"', style: theme.textTheme.bodyMedium!.copyWith(fontStyle: FontStyle.italic))
            else if (_isOwnProfile)
              const Text('No bio yet.'),
            if (_isOwnProfile)
              TextButton(
                key: const ValueKey('edit-bio-button'),
                onPressed: () => setState(() => _editingBio = true),
                child: const Text('Edit bio'),
              ),
          ],
        ),
      );
    }
    return Column(
      children: [
        TextField(
          key: const ValueKey('bio-field'),
          controller: _bioController,
          maxLength: 160,
          maxLines: 2,
          decoration: const InputDecoration(labelText: 'Bio'),
        ),
        if (_bioError != null) Text(_bioError!, style: TextStyle(color: theme.colorScheme.error)),
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            FilledButton(onPressed: _bioSaving ? null : _saveBio, child: Text(_bioSaving ? 'Saving…' : 'Save')),
            const SizedBox(width: 8),
            OutlinedButton(
              onPressed: () => setState(() {
                _editingBio = false;
                _bioController.text = _profile?.bio ?? '';
                _bioError = null;
              }),
              child: const Text('Cancel'),
            ),
          ],
        ),
      ],
    );
  }
}

/// `PlayerProfile` has no `copyWith` — these two small helpers avoid adding
/// one for just two call sites.
class _ProfileBioCopy {
  static PlayerProfile withBio(PlayerProfile profile, String? bio) => PlayerProfile(
    playerId: profile.playerId,
    displayName: profile.displayName,
    bio: bio,
    createdAtUtc: profile.createdAtUtc,
    joinGameYear: profile.joinGameYear,
    hasProSubscription: profile.hasProSubscription,
    totalWealthUsd: profile.totalWealthUsd,
    totalCompanyEquityUsd: profile.totalCompanyEquityUsd,
    companyCount: profile.companyCount,
    leaderboardRank: profile.leaderboardRank,
    activeBuildingTypes: profile.activeBuildingTypes,
    citiesWithBuildings: profile.citiesWithBuildings,
    totalProductsSold: profile.totalProductsSold,
    hallOfFame: profile.hallOfFame,
  );

  static PlayerProfile withDisplayName(PlayerProfile profile, String displayName) => PlayerProfile(
    playerId: profile.playerId,
    displayName: displayName,
    bio: profile.bio,
    createdAtUtc: profile.createdAtUtc,
    joinGameYear: profile.joinGameYear,
    hasProSubscription: profile.hasProSubscription,
    totalWealthUsd: profile.totalWealthUsd,
    totalCompanyEquityUsd: profile.totalCompanyEquityUsd,
    companyCount: profile.companyCount,
    leaderboardRank: profile.leaderboardRank,
    activeBuildingTypes: profile.activeBuildingTypes,
    citiesWithBuildings: profile.citiesWithBuildings,
    totalProductsSold: profile.totalProductsSold,
    hallOfFame: profile.hallOfFame,
  );
}
