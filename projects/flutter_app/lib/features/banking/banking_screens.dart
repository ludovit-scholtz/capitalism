import 'package:flutter/material.dart';

import '../../core/widgets/placeholder_screen.dart';

class LoanMarketplaceScreen extends StatelessWidget {
  const LoanMarketplaceScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Banking', sourceView: 'LoanMarketplaceView.vue');
}

class BankManagementScreen extends StatelessWidget {
  const BankManagementScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Bank Management', sourceView: 'BankManagementView.vue');
}

class BankLoanRequestScreen extends StatelessWidget {
  const BankLoanRequestScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Request Loan', sourceView: 'BankLoanRequestView.vue');
}

class BankStatementScreen extends StatelessWidget {
  const BankStatementScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Bank Statement', sourceView: 'BankStatementView.vue');
}
