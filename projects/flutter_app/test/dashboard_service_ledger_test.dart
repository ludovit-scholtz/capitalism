import 'dart:convert';

import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/config/app_config.dart';
import 'package:capitalism_app/core/graphql/graphql_service.dart';
import 'package:capitalism_app/features/dashboard/dashboard_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'support/in_memory_token_storage.dart';

DashboardService _serviceReturning(Map<String, dynamic> data) {
  AppConfig.setGraphqlUrl('https://example.test/graphql');
  final client = MockClient(
    (request) async => http.Response(jsonEncode({'data': data}), 200, headers: {'content-type': 'application/json'}),
  );
  return DashboardService(GraphQlService(AuthState(storage: InMemoryTokenStorage()), client: client));
}

void main() {
  test('a null companyLedger (company missing or not owned) reads as "no ledger", not as an error', () async {
    final service = _serviceReturning({'companyLedger': null});

    expect(await service.fetchCompanyOverviewLedger('gone'), isNull);
  });

  test('a returned companyLedger is parsed', () async {
    final service = _serviceReturning({
      'companyLedger': {
        'companyName': 'Acme',
        'gameYear': 2001,
        'currentCash': 10,
        'primaryCurrencyCode': 'EUR',
        'totalRevenue': 1234.5,
        'totalPurchasingCosts': 0,
        'totalShippingCosts': 0,
        'totalLaborCosts': 0,
        'totalEnergyCosts': 0,
        'totalMarketingCosts': 0,
        'totalTaxPaid': 0,
        'totalOtherCosts': 0,
        'netIncome': 1234.5,
        'totalAssets': 99,
      },
    });

    final ledger = await service.fetchCompanyOverviewLedger('acme');

    expect(ledger, isNotNull);
    expect(ledger!.totalRevenue, 1234.5);
  });
}
