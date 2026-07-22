// Ported from `projects/frontend/src/views/TutorialView.vue`. Milestone
// text is hardcoded English (matching `MILESTONE_DEFS`' fallback content)
// rather than routed through i18n keys, since this app's i18n setup only
// covers a handful of screens so far — a reasonable simplification, not a
// functional trim (the milestone list, progress, and resume links are all
// real). Marking a milestone complete (`markTutorialMilestoneComplete`) is
// server-driven by real gameplay actions elsewhere, not a button on this
// screen either on the web or here.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'tutorial_models.dart';
import 'tutorial_service.dart';

class TutorialScreen extends StatefulWidget {
  const TutorialScreen({super.key, GraphQlService? graphQlService, TutorialService? tutorialService})
    : _injectedGraphQlService = graphQlService,
      _injectedTutorialService = tutorialService;

  final GraphQlService? _injectedGraphQlService;
  final TutorialService? _injectedTutorialService;

  @override
  State<TutorialScreen> createState() => _TutorialScreenState();
}

class _TutorialScreenState extends State<TutorialScreen> {
  late final TutorialService _service;
  late final bool _isAuthenticated;

  bool _loading = true;
  String? _error;
  List<TutorialMilestoneStatus> _statuses = const [];

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    _isAuthenticated = auth.isAuthenticated;
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedTutorialService ?? TutorialService(graphQlService);
    if (_isAuthenticated) {
      _load();
    } else {
      _loading = false;
    }
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final statuses = await _service.fetchProgress();
      if (!mounted) return;
      setState(() {
        _statuses = statuses;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load your tutorial progress. Please try again.';
        _loading = false;
      });
    }
  }

  bool _isCompleted(String milestoneId) {
    for (final status in _statuses) {
      if (status.milestone == milestoneId) return status.isCompleted || status.bountyAwarded;
    }
    return false;
  }

  bool _isBountyAwarded(String milestoneId) {
    for (final status in _statuses) {
      if (status.milestone == milestoneId) return status.bountyAwarded;
    }
    return false;
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [Text(_error!), const SizedBox(height: 12), OutlinedButton(onPressed: _load, child: const Text('Try again'))],
          ),
        ),
      );
    }

    final completedCount = tutorialMilestoneDefs.where((def) => _isCompleted(def.id)).length;
    final theme = Theme.of(context);

    return RefreshIndicator(
      onRefresh: _isAuthenticated ? _load : () async {},
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('🎓 Tutorial', style: theme.textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text('Complete milestones to learn the game and earn bounty points.', style: theme.textTheme.bodyMedium),
          const SizedBox(height: 12),
          LinearProgressIndicator(value: tutorialMilestoneDefs.isEmpty ? 0 : completedCount / tutorialMilestoneDefs.length),
          const SizedBox(height: 4),
          Text('$completedCount / ${tutorialMilestoneDefs.length} complete'),
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('🏆 Endgame Goal', style: theme.textTheme.titleMedium),
                  const SizedBox(height: 4),
                  const Text('Grow your net worth to surpass the real world\'s wealthiest.'),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          for (final def in tutorialMilestoneDefs)
            Card(
              key: ValueKey('milestone-${def.id}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                leading: Text(def.icon, style: const TextStyle(fontSize: 22)),
                title: Text(def.title),
                subtitle: Text(def.description),
                trailing: _isCompleted(def.id)
                    ? Chip(label: Text(_isBountyAwarded(def.id) ? 'Bounty earned' : 'Done'))
                    : (_isAuthenticated
                          ? FilledButton(onPressed: () => context.go(def.resumeRoute), child: const Text('Resume'))
                          : Text('+${def.bountyPoints} pts')),
              ),
            ),
          if (!_isAuthenticated) ...[
            const SizedBox(height: 16),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  children: [
                    const Text('Sign in to track your tutorial progress and earn bounty points.'),
                    const SizedBox(height: 8),
                    FilledButton(onPressed: () => context.go('/login'), child: const Text('Sign in')),
                  ],
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
