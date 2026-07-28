// Shared base widget/state for the City tab screens (Overview, Economy,
// Buildings, Market, Contracts, Competitors) — factored out of
// `city_tab_screens.dart` so each tab can live in its own file without
// duplicating the `CityTabService` bootstrap and loading/error boilerplate.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'city_tab_service.dart';

abstract class CityTabScreen extends StatefulWidget {
  const CityTabScreen({super.key, required this.cityId, this.graphQlService, this.cityTabService});

  final String cityId;
  final GraphQlService? graphQlService;
  final CityTabService? cityTabService;
}

abstract class CityTabScreenState<T extends CityTabScreen> extends State<T> {
  late final CityTabService service;

  /// Exposed (not just consumed internally) so subclasses that need an
  /// additional service built from the same underlying connection — e.g.
  /// `CityEconomyScreen`'s `CityEconomyService` — don't have to re-derive
  /// it from `AuthState` themselves.
  late final GraphQlService graphQlService;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    graphQlService = widget.graphQlService ?? GraphQlService(auth);
    service = widget.cityTabService ?? CityTabService(graphQlService);
  }

  Widget buildLoading() => const Center(child: CircularProgressIndicator());

  Widget buildError(String message, VoidCallback onRetry) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [Text(message), const SizedBox(height: 12), OutlinedButton(onPressed: onRetry, child: const Text('Try again'))],
      ),
    ),
  );
}
