import 'package:capitalism_app/features/tutorial/tutorial_models.dart';
import 'package:capitalism_app/features/tutorial/tutorial_service.dart';

class FakeTutorialService implements TutorialService {
  FakeTutorialService({this.statuses = const [], this.fetchError});

  final List<TutorialMilestoneStatus> statuses;
  final Object? fetchError;

  final List<String> calls = [];

  @override
  Future<List<TutorialMilestoneStatus>> fetchProgress() async {
    calls.add('fetchProgress');
    if (fetchError != null) throw fetchError!;
    return statuses;
  }
}
