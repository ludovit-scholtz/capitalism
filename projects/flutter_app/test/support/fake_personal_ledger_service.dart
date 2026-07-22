import 'package:capitalism_app/features/company/personal_ledger_models.dart';
import 'package:capitalism_app/features/company/personal_ledger_service.dart';

class FakePersonalLedgerService implements PersonalLedgerService {
  FakePersonalLedgerService({this.account, this.fetchError});

  final PersonAccount? account;
  final Object? fetchError;

  final List<String> calls = [];

  @override
  Future<PersonAccount?> fetchPersonAccount() async {
    calls.add('fetchPersonAccount');
    if (fetchError != null) throw fetchError!;
    return account;
  }
}
