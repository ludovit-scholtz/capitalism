import 'package:capitalism_app/features/buildings/building_sourcing_models.dart';
import 'package:capitalism_app/features/buildings/building_sourcing_service.dart';

class FakeBuildingSourcingService implements BuildingSourcingService {
  FakeBuildingSourcingService({this.offers = const [], this.preview, this.candidates = const []});

  final List<GlobalExchangeOffer> offers;
  final ProcurementPreview? preview;
  final List<SourcingCandidate> candidates;

  final List<String> calls = [];

  @override
  Future<List<GlobalExchangeOffer>> fetchGlobalExchangeOffers({required String destinationCityId, String? resourceTypeId}) async {
    calls.add('fetchGlobalExchangeOffers');
    return offers;
  }

  @override
  Future<ProcurementPreview?> fetchProcurementPreview(String unitId) async {
    calls.add('fetchProcurementPreview');
    return preview;
  }

  @override
  Future<List<SourcingCandidate>> fetchSourcingCandidates(String unitId) async {
    calls.add('fetchSourcingCandidates');
    return candidates;
  }
}
