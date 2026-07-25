import 'package:capitalism_app/features/tutorial/tutorial_models.dart';
import 'package:capitalism_app/features/tutorial/tutorial_service.dart';

class FakeTutorialService implements TutorialService {
  FakeTutorialService({this.statuses = const [], this.fetchError, this.markCompleteError});

  final List<TutorialMilestoneStatus> statuses;
  final Object? fetchError;
  final Object? markCompleteError;

  final List<String> calls = [];
  final List<String> markedComplete = [];

  @override
  Future<List<TutorialMilestoneStatus>> fetchProgress() async {
    calls.add('fetchProgress');
    if (fetchError != null) throw fetchError!;
    return statuses;
  }

  @override
  Future<void> markComplete(String milestone) async {
    calls.add('markComplete');
    markedComplete.add(milestone);
    if (markCompleteError != null) throw markCompleteError!;
  }
}
