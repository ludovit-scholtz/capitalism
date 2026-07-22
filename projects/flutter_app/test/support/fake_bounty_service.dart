import 'package:capitalism_app/features/bounties/bounty_models.dart';
import 'package:capitalism_app/features/bounties/bounty_service.dart';

class FakeBountyService implements BountyService {
  FakeBountyService({this.bounties = const [], this.error});

  final List<CompletedBounty> bounties;
  final Object? error;

  int fetchCallCount = 0;

  @override
  Future<List<CompletedBounty>> fetchCompletedBounties() async {
    fetchCallCount++;
    if (error != null) throw error!;
    return bounties;
  }
}
