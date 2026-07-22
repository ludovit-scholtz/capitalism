import 'package:capitalism_app/features/banking/banking_models.dart';

const bankFixture = BankListing(
  bankBuildingId: 'bank-1',
  bankBuildingName: 'First National',
  cityName: 'Metropolis',
  depositInterestRatePercent: 2.5,
  lendingInterestRatePercent: 6.0,
  availableLendingCapacity: 100000,
  baseCapitalDeposited: true,
  lenderCompanyId: 'company-owner',
);

const loanFixture = LoanSummary(
  id: 'loan-1',
  bankBuildingId: 'bank-1',
  bankBuildingName: 'First National',
  loanCurrencyCode: 'EUR',
  originalPrincipal: 10000,
  remainingPrincipal: 8000,
  annualInterestRatePercent: 6,
  nextPaymentTick: 100,
  paymentAmount: 500,
  status: 'ACTIVE',
  missedPayments: 0,
);

const overdueLoanFixture = LoanSummary(
  id: 'loan-2',
  bankBuildingId: 'bank-1',
  bankBuildingName: 'First National',
  loanCurrencyCode: 'EUR',
  originalPrincipal: 10000,
  remainingPrincipal: 9000,
  annualInterestRatePercent: 6,
  nextPaymentTick: 50,
  paymentAmount: 500,
  status: 'OVERDUE',
  missedPayments: 2,
);

const bankInfoOwnerFixture = BankInfo(
  bankBuildingId: 'bank-1',
  bankBuildingName: 'First National',
  cityCurrencyCode: 'EUR',
  lenderCompanyId: 'company-1',
  depositInterestRatePercent: 2.5,
  lendingInterestRatePercent: 6.0,
  totalDeposits: 50000,
  availableLendingCapacity: 100000,
  baseCapitalDeposited: true,
  baseCapitalRequirement: 20000,
  liquidityStatus: 'HEALTHY',
  centralBankDebt: 0,
  centralBankInterestRatePercent: 4,
  reserveRequirement: 10000,
  availableCash: 15000,
  reserveShortfall: 0,
);

const bankInfoCustomerFixture = BankInfo(
  bankBuildingId: 'bank-1',
  bankBuildingName: 'First National',
  cityCurrencyCode: 'EUR',
  lenderCompanyId: 'someone-else',
  depositInterestRatePercent: 2.5,
  lendingInterestRatePercent: 6.0,
  totalDeposits: 50000,
  availableLendingCapacity: 100000,
  baseCapitalDeposited: true,
  baseCapitalRequirement: 20000,
  liquidityStatus: 'HEALTHY',
);

const statementFixture = BankStatementResult(
  companyName: 'Acme Corp',
  currencyCode: 'EUR',
  currentBalance: 5000,
  totalEntries: 1,
  rows: [BankStatementRow(id: 'row-1', description: 'Salary payment', category: 'LABOR', amount: -200, runningBalance: 4800)],
);
