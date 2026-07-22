import 'package:capitalism_app/features/cities/cities_models.dart';
import 'package:capitalism_app/features/cities/cities_service.dart';

class FakeCitiesService implements CitiesService {
  FakeCitiesService({
    this.cities = const [],
    this.expansionCities = const [],
    this.firstCompanyId,
    this.citiesError,
    this.expansionError,
  });

  final List<City> cities;
  final List<ExpansionCity> expansionCities;
  final String? firstCompanyId;
  final Object? citiesError;
  final Object? expansionError;

  final List<String> calls = [];

  @override
  Future<List<City>> fetchCities() async {
    calls.add('fetchCities');
    if (citiesError != null) throw citiesError!;
    return cities;
  }

  @override
  Future<List<ExpansionCity>> fetchExpansionCities() async {
    calls.add('fetchExpansionCities');
    if (expansionError != null) throw expansionError!;
    return expansionCities;
  }

  @override
  Future<String?> fetchMyFirstCompanyId() async {
    calls.add('fetchMyFirstCompanyId');
    return firstCompanyId;
  }
}
