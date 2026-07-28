// Quick-stats row and Overview/Achievements/Rank History tab bodies for
// the Player Profile screen — factored out of `player_profile_screen.dart`
// to keep that file under the 500-line budget (mechanical move, not a
// behavior change).

import 'package:flutter/material.dart';

import '../../core/widgets/game_tick_time.dart';
import 'leaderboard_format.dart';
import 'leaderboard_models.dart';

class QuickStatsRow extends StatelessWidget {
  const QuickStatsRow({super.key, required this.profile});

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
        stat(formatCompactWealth(context, profile.totalWealthUsd), 'Total wealth'),
        const SizedBox(width: 8),
        stat('${profile.companyCount}', 'Companies'),
        const SizedBox(width: 8),
        stat('${profile.citiesWithBuildings}', 'Cities'),
      ],
    );
  }
}

class OverviewTab extends StatelessWidget {
  const OverviewTab({super.key, required this.profile});

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
                Text('Company equity: ${formatCompactWealth(context, profile.totalCompanyEquityUsd)}'),
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
                Text(
                  'Highest single-tick revenue: ${hof.highestSingleTickRevenue > 0 ? formatCompactWealth(context, hof.highestSingleTickRevenue) : '—'}',
                ),
                Text(
                  'Largest acquisition: ${hof.largestBuildingAcquisitionPrice > 0 ? formatCompactWealth(context, hof.largestBuildingAcquisitionPrice) : '—'}',
                ),
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

class AchievementsTab extends StatelessWidget {
  const AchievementsTab({super.key, required this.loading, required this.badges});

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

class RankHistoryTab extends StatelessWidget {
  const RankHistoryTab({super.key, required this.loading, required this.snapshots});

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
            title: Text('Rank #${snapshot.leaderboardRank} · ${formatCompactWealth(context, snapshot.wealthUsd)}'),
            subtitle: GameTickTime(snapshot.snapshotTick),
            trailing: snapshot.positionChange == null
                ? null
                : Text(snapshot.positionChange! > 0 ? '▲${snapshot.positionChange}' : (snapshot.positionChange! < 0 ? '▼${-snapshot.positionChange!}' : '–')),
          ),
      ],
    );
  }
}
