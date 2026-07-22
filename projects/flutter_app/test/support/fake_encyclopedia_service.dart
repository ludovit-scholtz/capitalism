import 'package:capitalism_app/features/encyclopedia/encyclopedia_models.dart';
import 'package:capitalism_app/features/encyclopedia/encyclopedia_service.dart';

class FakeEncyclopediaService implements EncyclopediaService {
  FakeEncyclopediaService({this.entries = const [], this.detailBySlug = const {}, this.entriesError, this.detailError});

  final List<EncyclopediaEntry> entries;
  final Map<String, EncyclopediaResourceDetail> detailBySlug;
  final Object? entriesError;
  final Object? detailError;

  final List<String> calls = [];

  @override
  Future<List<EncyclopediaEntry>> fetchAllEntries() async {
    calls.add('fetchAllEntries');
    if (entriesError != null) throw entriesError!;
    return entries;
  }

  @override
  Future<EncyclopediaResourceDetail?> fetchResourceDetail(String slug) async {
    calls.add('fetchResourceDetail');
    if (detailError != null) throw detailError!;
    return detailBySlug[slug];
  }
}
