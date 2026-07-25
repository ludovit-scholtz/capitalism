import '../../core/graphql/graphql_service.dart';
import 'tutorial_models.dart';

const _progressQuery = r'''
  query TutorialProgress {
    tutorialProgress { milestone isCompleted completedAtUtc bountyAwarded bountyAwardedAtUtc bountyPoints }
  }
''';

/// Mirrors `markTutorialMilestoneComplete` from
/// `useTutorialContext.ts` — used by the Building Detail screen's
/// first-time-user tooltips (ROADMAP 138) in addition to the Tutorial
/// screen's own checklist.
const _markCompleteMutation = r'''
  mutation MarkTutorialMilestoneComplete($input: MarkTutorialMilestoneCompleteInput!) {
    markTutorialMilestoneComplete(input: $input) {
      milestone isCompleted completedAtUtc bountyAwarded bountyAwardedAtUtc bountyPoints
    }
  }
''';

/// GraphQL calls for the Tutorial screen, matching
/// `projects/frontend/src/composables/useTutorialContext.ts`'s
/// `tutorialProgress` query.
class TutorialService {
  const TutorialService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<TutorialMilestoneStatus>> fetchProgress() async {
    final result = await _graphQlService.request(_progressQuery);
    final list = result['tutorialProgress'] as List<dynamic>? ?? const [];
    return list.map((e) => TutorialMilestoneStatus.fromJson(e as Map<String, dynamic>)).toList();
  }

  /// Best-effort — callers should swallow failures the same way the web's
  /// `useFirstTimeUserGates.ts` does, since a failed milestone mark should
  /// never block dismissing a tooltip.
  Future<void> markComplete(String milestone) {
    return _graphQlService.request(
      _markCompleteMutation,
      variables: {
        'input': {'milestone': milestone},
      },
    );
  }
}
