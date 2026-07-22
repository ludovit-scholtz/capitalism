import '../../core/graphql/graphql_service.dart';
import 'tutorial_models.dart';

const _progressQuery = r'''
  query TutorialProgress {
    tutorialProgress { milestone isCompleted completedAtUtc bountyAwarded bountyAwardedAtUtc bountyPoints }
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
}
