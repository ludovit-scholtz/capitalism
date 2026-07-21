import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class LedgerScreen extends StatelessWidget {
  const LedgerScreen({super.key});

  @override
  Widget build(BuildContext context) => const PlaceholderScreen(title: 'Ledger', sourceView: 'LedgerView.vue');
}

class CompanyContractsScreen extends StatelessWidget {
  const CompanyContractsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Company Contracts', sourceView: 'CompanyContractsView.vue');
}

class CompanySettingsScreen extends StatelessWidget {
  const CompanySettingsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Company Settings', sourceView: 'CompanySettingsView.vue');
}

class CompanyResearchScreen extends StatelessWidget {
  const CompanyResearchScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Company Research', sourceView: 'CompanyResearchView.vue');
}

class PersonalLedgerScreen extends StatelessWidget {
  const PersonalLedgerScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Personal Ledger', sourceView: 'PersonalLedgerView.vue');
}
