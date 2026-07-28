// Income statement / balance sheet / cash flow cards for the Ledger screen,
// ported from the `statements-grid` section of
// `projects/frontend/src/components/ledger/LedgerMainContent.vue`. Each
// drillable row's ▼/▲ button toggles `LedgerDrillPanel` below.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';
import 'company_models.dart';

typedef DrillToggle = void Function(String category);

class LedgerStatementRow extends StatelessWidget {
  const LedgerStatementRow({
    super.key,
    required this.label,
    required this.amount,
    this.currencyCode,
    this.category,
    this.activeCategory,
    this.onDrillToggle,
    this.bold = false,
    this.showSign = true,
  });

  final String label;
  final double amount;
  final String? currencyCode;
  final String? category;
  final String? activeCategory;
  final DrillToggle? onDrillToggle;
  final bool bold;
  final bool showSign;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final languageCode = context.watch<LocaleState>().languageCode;
    final style = bold ? theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold) : theme.textTheme.bodyMedium;
    final displayAmount = showSign ? amount : amount.abs();
    final color = amount == 0
        ? null
        : (amount >= 0 ? Colors.green.shade600 : Colors.red.shade600);
    final active = category != null && category == activeCategory;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          Expanded(child: Text(label, style: style)),
          Text(
            AppNumberFormat.money(displayAmount, currencyCode: currencyCode ?? 'EUR', languageCode: languageCode),
            style: style?.copyWith(color: bold ? null : color),
          ),
          if (category != null)
            IconButton(
              key: ValueKey('drill-$category'),
              icon: Icon(active ? Icons.expand_less : Icons.expand_more, size: 18),
              padding: EdgeInsets.zero,
              constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
              tooltip: 'Drill down: $label',
              onPressed: () => onDrillToggle?.call(category!),
            ),
        ],
      ),
    );
  }
}

class LedgerStatementsGrid extends StatelessWidget {
  const LedgerStatementsGrid({super.key, required this.ledger, this.activeCategory, this.onDrillToggle});

  final CompanyLedger ledger;
  final String? activeCategory;
  final DrillToggle? onDrillToggle;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final code = ledger.primaryCurrencyCode;

    return Wrap(
      spacing: 16,
      runSpacing: 16,
      children: [
        _card(theme, '📈 Income Statement', [
          LedgerStatementRow(label: 'Revenue', amount: ledger.totalRevenue, currencyCode: code, category: 'REVENUE', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalGovernmentContractRevenue > 0)
            LedgerStatementRow(label: '🏛️ Government contracts', amount: ledger.totalGovernmentContractRevenue, currencyCode: code, category: 'GOVERNMENT_CONTRACT_REVENUE', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalMediaHouseIncome > 0)
            LedgerStatementRow(label: '📺 Media house income', amount: ledger.totalMediaHouseIncome, currencyCode: code, category: 'MEDIA_HOUSE_INCOME', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalRentIncome > 0)
            LedgerStatementRow(label: '🏠 Rental income', amount: ledger.totalRentIncome, currencyCode: code, category: 'RENT_INCOME', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalDepositInterestReceived > 0)
            LedgerStatementRow(label: 'Deposit interest received', amount: ledger.totalDepositInterestReceived, currencyCode: code, category: 'DEPOSIT_INTEREST_RECEIVED', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalLoanInterestIncome > 0)
            LedgerStatementRow(label: 'Loan interest income', amount: ledger.totalLoanInterestIncome, currencyCode: code, category: 'LOAN_INTEREST_INCOME', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          LedgerStatementRow(label: 'Purchasing costs', amount: -ledger.totalPurchasingCosts, currencyCode: code, category: 'PURCHASING_COST', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalShippingCosts > 0)
            LedgerStatementRow(label: 'Shipping costs', amount: -ledger.totalShippingCosts, currencyCode: code, category: 'SHIPPING_COST', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalLaborCosts > 0)
            LedgerStatementRow(label: 'Labor costs', amount: -ledger.totalLaborCosts, currencyCode: code, category: 'LABOR_COST', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalEnergyCosts > 0)
            LedgerStatementRow(label: 'Energy costs', amount: -ledger.totalEnergyCosts, currencyCode: code, category: 'ENERGY_COST', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalMarketingCosts > 0)
            LedgerStatementRow(label: 'Marketing costs', amount: -ledger.totalMarketingCosts, currencyCode: code, category: 'MARKETING', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalPropertyMaintenance > 0)
            LedgerStatementRow(label: '🔧 Property maintenance', amount: -ledger.totalPropertyMaintenance, currencyCode: code, category: 'PROPERTY_MAINTENANCE', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalDepositInterestPaid > 0)
            LedgerStatementRow(label: 'Deposit interest paid', amount: -ledger.totalDepositInterestPaid, currencyCode: code, category: 'DEPOSIT_INTEREST_PAID', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalLoanInterestExpense > 0)
            LedgerStatementRow(label: 'Loan interest expense', amount: -ledger.totalLoanInterestExpense, currencyCode: code, category: 'LOAN_INTEREST_EXPENSE', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalTaxPaid > 0)
            LedgerStatementRow(label: 'Tax paid', amount: -ledger.totalTaxPaid, currencyCode: code, category: 'TAX', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          const Divider(),
          LedgerStatementRow(label: 'Net income', amount: ledger.netIncome, currencyCode: code, bold: true),
        ]),
        _card(theme, '📊 Balance Sheet', [
          LedgerStatementRow(label: 'Cash', amount: ledger.currentCash, currencyCode: code, showSign: false),
          LedgerStatementRow(label: 'Property value', amount: ledger.propertyValue, currencyCode: code, category: 'PROPERTY_PURCHASE', activeCategory: activeCategory, onDrillToggle: onDrillToggle, showSign: false),
          LedgerStatementRow(label: 'Property appreciation', amount: ledger.propertyAppreciation, currencyCode: code),
          LedgerStatementRow(label: 'Building value', amount: ledger.buildingValue, currencyCode: code, category: 'BUILDING_VALUE', activeCategory: activeCategory, onDrillToggle: onDrillToggle, showSign: false),
          LedgerStatementRow(label: 'Inventory value', amount: ledger.inventoryValue, currencyCode: code, category: 'INVENTORY_VALUE', activeCategory: activeCategory, onDrillToggle: onDrillToggle, showSign: false),
          if (ledger.totalDepositsPlaced > 0)
            LedgerStatementRow(label: 'Deposits placed', amount: ledger.totalDepositsPlaced, currencyCode: code, category: 'DEPOSIT_MADE', activeCategory: activeCategory, onDrillToggle: onDrillToggle, showSign: false),
          const Divider(),
          LedgerStatementRow(label: 'Total assets', amount: ledger.totalAssets, currencyCode: code, bold: true, showSign: false),
        ]),
        _card(theme, '💵 Cash Flow', [
          LedgerStatementRow(label: 'From operations', amount: ledger.cashFromOperations, currencyCode: code),
          LedgerStatementRow(label: 'From investments', amount: ledger.cashFromInvestments, currencyCode: code),
          if (ledger.cashFromBanking != 0)
            LedgerStatementRow(label: 'From banking', amount: ledger.cashFromBanking, currencyCode: code, category: 'DEPOSIT_MADE', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalStockPurchaseCashOut > 0)
            LedgerStatementRow(label: 'Stock purchases', amount: -ledger.totalStockPurchaseCashOut, currencyCode: code, category: 'STOCK_PURCHASE', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
          if (ledger.totalStockSaleCashIn > 0)
            LedgerStatementRow(label: 'Stock sales', amount: ledger.totalStockSaleCashIn, currencyCode: code, category: 'STOCK_SALE', activeCategory: activeCategory, onDrillToggle: onDrillToggle),
        ]),
      ],
    );
  }

  Widget _card(ThemeData theme, String title, List<Widget> rows) {
    return SizedBox(
      width: 340,
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(title, style: theme.textTheme.titleMedium), const SizedBox(height: 8), ...rows]),
        ),
      ),
    );
  }
}
