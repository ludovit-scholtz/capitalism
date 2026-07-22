import 'package:capitalism_app/features/buildings/buy_building_models.dart';
import 'package:capitalism_app/features/cities/cities_models.dart';
import 'package:capitalism_app/features/city/city_tab_models.dart';
import 'package:capitalism_app/features/city/city_tab_service.dart';

class FakeCityTabService implements CityTabService {
  FakeCityTabService({
    this.city,
    this.lots = const [],
    this.competitors = const [],
    this.contracts = const [],
    this.eligibility,
    this.myCompanies = const [],
    this.cityError,
    this.lotsError,
    this.competitorsError,
    this.contractsError,
    this.bidError,
  });

  final City? city;
  final List<CityLot> lots;
  final List<CityCompetitor> competitors;
  final List<GovernmentContractCard> contracts;
  final ContractEligibility? eligibility;
  final List<Map<String, String>> myCompanies;
  final Object? cityError;
  final Object? lotsError;
  final Object? competitorsError;
  final Object? contractsError;
  final Object? bidError;

  final List<String> calls = [];
  Map<String, dynamic>? lastBidArgs;

  @override
  Future<City?> fetchCity(String cityId) async {
    calls.add('fetchCity');
    if (cityError != null) throw cityError!;
    return city;
  }

  @override
  Future<List<CityLot>> fetchLots(String cityId) async {
    calls.add('fetchLots');
    if (lotsError != null) throw lotsError!;
    return lots;
  }

  @override
  Future<List<CityCompetitor>> fetchCompetitors(String cityId, {int lastNTicks = 10}) async {
    calls.add('fetchCompetitors');
    if (competitorsError != null) throw competitorsError!;
    return competitors;
  }

  @override
  Future<List<GovernmentContractCard>> fetchOpenContracts(String cityId) async {
    calls.add('fetchOpenContracts');
    if (contractsError != null) throw contractsError!;
    return contracts;
  }

  @override
  Future<ContractEligibility?> fetchEligibility({required String contractId, required String companyId}) async {
    calls.add('fetchEligibility');
    return eligibility;
  }

  @override
  Future<List<Map<String, String>>> fetchMyCompanies() async {
    calls.add('fetchMyCompanies');
    return myCompanies;
  }

  @override
  Future<void> submitBid({
    required String contractId,
    required String companyId,
    required double bidPricePerUnit,
    required int estimatedDeliveryTick,
  }) async {
    calls.add('submitBid');
    if (bidError != null) throw bidError!;
    lastBidArgs = {'contractId': contractId, 'companyId': companyId, 'bidPricePerUnit': bidPricePerUnit};
  }
}
