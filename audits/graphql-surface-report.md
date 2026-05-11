# GraphQL Surface Inventory Report

> Generated at `2026-05-11T12:54:59.2956695Z`

- Total operations: **213**
- Sensitive operations: **95**
- Newly added sensitive operations missing required coverage: **61**

## Gate status

❌ Missing coverage detected for newly added sensitive operations:

- `mutation adminSetPlayerGoldBalance` (admin) — missing both auth-negative and owner-success tests
- `mutation assignGlobalGameAdminRole` (admin) — missing both auth-negative and owner-success tests
- `mutation createCompanyBankAccount` (finance) — missing unauthenticated/wrong-owner test
- `mutation createPersonalBankAccount` (finance) — missing unauthenticated/wrong-owner test
- `mutation markAllGameNewsRead` (admin) — missing unauthenticated/wrong-owner test
- `mutation markGameNewsRead` (admin) — missing unauthenticated/wrong-owner test
- `mutation placeLimitOrder` (shareholder) — missing unauthenticated/wrong-owner test
- `mutation proposeDividend` (shareholder) — missing owner-success test
- `mutation removeGlobalGameAdminRole` (admin) — missing both auth-negative and owner-success tests
- `mutation repayLoanDebt` (lending) — missing owner-success test
- `mutation setBankAccountAlertThreshold` (finance) — missing unauthenticated/wrong-owner test
- `mutation setLocalGameAdminRole` (admin) — missing both auth-negative and owner-success tests
- `mutation setPlayerInvisibleInChat` (admin) — missing unauthenticated/wrong-owner test
- `mutation startAdminImpersonation` (admin) — missing both auth-negative and owner-success tests
- `mutation stopAdminImpersonation` (admin) — missing both auth-negative and owner-success tests
- `mutation switchAccountContext` (finance) — missing unauthenticated/wrong-owner test
- `mutation topUpDeposit` (finance) — missing unauthenticated/wrong-owner test
- `mutation updateRealWorldBillionaire` (admin) — missing unauthenticated/wrong-owner test
- `mutation upsertGameNewsEntry` (admin) — missing unauthenticated/wrong-owner test
- `query additionalCompanyPrerequisites` (ranking) — missing unauthenticated/wrong-owner test
- `query adminApiKeyAuditLog` (admin) — missing both auth-negative and owner-success tests
- `query adminApiKeys` (admin) — missing both auth-negative and owner-success tests
- `query adminProductAnalytics` (admin) — missing unauthenticated/wrong-owner test
- `query allBanks` (finance) — missing unauthenticated/wrong-owner test
- `query bankDepositRateHistory` (finance) — missing both auth-negative and owner-success tests
- `query bankDeposits` (finance) — missing both auth-negative and owner-success tests
- `query bankLoans` (lending) — missing unauthenticated/wrong-owner test
- `query cityLots` (finance) — missing unauthenticated/wrong-owner test
- `query companyBankAccounts` (finance) — missing unauthenticated/wrong-owner test
- `query companyCityFinancialBreakdown` (finance) — missing unauthenticated/wrong-owner test
- `query companyRankings` (ranking) — missing unauthenticated/wrong-owner test
- `query companyShareholders` (shareholder) — missing unauthenticated/wrong-owner test
- `query dividendProposals` (shareholder) — missing both auth-negative and owner-success tests
- `query eurFxRates` (finance) — missing unauthenticated/wrong-owner test
- `query forexQuote` (finance) — missing unauthenticated/wrong-owner test
- `query forexTradeHistory` (finance) — missing unauthenticated/wrong-owner test
- `query fxRateHistory` (finance) — missing unauthenticated/wrong-owner test
- `query gameAdminSession` (admin) — missing both auth-negative and owner-success tests
- `query gameState` (ranking) — missing unauthenticated/wrong-owner test
- `query getCities` (finance) — missing unauthenticated/wrong-owner test
- `query globalExchangeOffers` (finance) — missing unauthenticated/wrong-owner test
- `query globalExchangeProductListings` (finance) — missing unauthenticated/wrong-owner test
- `query loanOffers` (lending) — missing unauthenticated/wrong-owner test
- `query myBankAccounts` (finance) — missing unauthenticated/wrong-owner test
- `query myCollateralBuildings` (lending) — missing unauthenticated/wrong-owner test
- `query myDeposits` (finance) — missing both auth-negative and owner-success tests
- `query myDividendVotes` (shareholder) — missing both auth-negative and owner-success tests
- `query myLoanOffers` (lending) — missing both auth-negative and owner-success tests
- `query myLoans` (lending) — missing unauthenticated/wrong-owner test
- `query myOpenDividendProposalCount` (shareholder) — missing both auth-negative and owner-success tests
- `query myOpenOrders` (shareholder) — missing unauthenticated/wrong-owner test
- `query operationsStatistics` (admin) — missing unauthenticated/wrong-owner test
- `query orderBook` (shareholder) — missing both auth-negative and owner-success tests
- `query playerCurrencyBalances` (finance) — missing unauthenticated/wrong-owner test
- `query playerRankHistory` (ranking) — missing unauthenticated/wrong-owner test
- `query rankHistory` (ranking) — missing unauthenticated/wrong-owner test
- `query rankings` (ranking) — missing unauthenticated/wrong-owner test
- `query starterIndustries` (ranking) — missing unauthenticated/wrong-owner test
- `query stockExchangePriceHistory` (shareholder) — missing unauthenticated/wrong-owner test
- `query stockTradeHistory` (shareholder) — missing unauthenticated/wrong-owner test
- `query unitUpgradeInfo` (lending) — missing unauthenticated/wrong-owner test

## Sensitive operation inventory

| Domain | Kind | GraphQL operation | Explicit [Authorize] | Negative coverage | Positive coverage | Source |
|---|---|---|---|---|---|---|
| admin | mutation | `adminSetPlayerGoldBalance` | Yes | No | No | `Mutation.GoldAmm.Swap.cs` |
| admin | mutation | `assignGlobalGameAdminRole` | Yes | No | No | `Mutation.Admin.cs` |
| admin | mutation | `markAllGameNewsRead` | Yes | No | Yes | `Mutation.Admin.cs` |
| admin | mutation | `markGameNewsRead` | Yes | No | Yes | `Mutation.Admin.cs` |
| admin | mutation | `removeGlobalGameAdminRole` | Yes | No | No | `Mutation.Admin.cs` |
| admin | mutation | `setLocalGameAdminRole` | Yes | No | No | `Mutation.Admin.cs` |
| admin | mutation | `setPlayerInvisibleInChat` | Yes | No | Yes | `Mutation.Admin.cs` |
| admin | mutation | `startAdminImpersonation` | Yes | No | No | `Mutation.Auth.cs` |
| admin | mutation | `stopAdminImpersonation` | Yes | No | No | `Mutation.Auth.cs` |
| admin | mutation | `updateRealWorldBillionaire` | Yes | No | Yes | `Mutation.Admin.cs` |
| admin | mutation | `upsertGameNewsEntry` | Yes | No | Yes | `Mutation.Admin.cs` |
| admin | query | `adminApiKeyAuditLog` | Yes | No | No | `Query.ApiKey.cs` |
| admin | query | `adminApiKeys` | Yes | No | No | `Query.ApiKey.cs` |
| admin | query | `adminProductAnalytics` | Yes | No | Yes | `Query.OperationsAdmin.cs` |
| admin | query | `gameAdminDashboard` | Yes | Yes | Yes | `Query.Admin.cs` |
| admin | query | `gameAdminSession` | Yes | No | No | `Query.Admin.cs` |
| admin | query | `operationsStatistics` | Yes | No | Yes | `Query.OperationsAdmin.cs` |
| finance | mutation | `addGoldAmmLiquidity` | Yes | Yes | Yes | `Mutation.GoldAmm.cs` |
| finance | mutation | `assignBuildingBankAccount` | Yes | Yes | Yes | `Mutation.BuildingBankAccount.cs` |
| finance | mutation | `buyFromExchange` | Yes | Yes | Yes | `Mutation.Exchange.cs` |
| finance | mutation | `closeBankAccount` | Yes | Yes | Yes | `Mutation.Banking.cs` |
| finance | mutation | `closeCompanyBankAccount` | Yes | Yes | Yes | `Mutation.BuildingBankAccount.Accounts.cs` |
| finance | mutation | `createCompanyBankAccount` | Yes | No | Yes | `Mutation.BuildingBankAccount.cs` |
| finance | mutation | `createPersonalBankAccount` | Yes | No | Yes | `Mutation.BuildingBankAccount.Accounts.cs` |
| finance | mutation | `executeForexSwap` | Yes | Yes | Yes | `Mutation.Forex.cs` |
| finance | mutation | `fundBuildingBankAccount` | Yes | Yes | Yes | `Mutation.BuildingBankAccount.cs` |
| finance | mutation | `initiateBaseDeposit` | Yes | Yes | Yes | `Mutation.BankDeposits.cs` |
| finance | mutation | `openBankAccount` | Yes | Yes | Yes | `Mutation.Banking.cs` |
| finance | mutation | `removeGoldAmmLiquidity` | Yes | Yes | Yes | `Mutation.GoldAmm.cs` |
| finance | mutation | `sellToExchange` | Yes | Yes | Yes | `Mutation.Exchange.cs` |
| finance | mutation | `setBankAccountAlertThreshold` | Yes | No | Yes | `Mutation.Notifications.cs` |
| finance | mutation | `setBankRates` | Yes | Yes | Yes | `Mutation.BankDeposits.cs` |
| finance | mutation | `switchAccountContext` | Yes | No | Yes | `Mutation.Company.cs` |
| finance | mutation | `topUpDeposit` | Yes | No | Yes | `Mutation.BankDeposits.cs` |
| finance | mutation | `transferFunds` | Yes | Yes | Yes | `Mutation.BankAccountTransfer.cs` |
| finance | mutation | `updateBankDepositRate` | Yes | Yes | Yes | `Mutation.BankDepositRate.cs` |
| finance | query | `allBanks` | No | No | Yes | `Query.Banking.cs` |
| finance | query | `bankDepositRateHistory` | Yes | No | No | `Query.Banking.cs` |
| finance | query | `bankDeposits` | Yes | No | No | `Query.Banking.cs` |
| finance | query | `bankInfo` | No | Yes | Yes | `Query.Banking.cs` |
| finance | query | `bankStatement` | Yes | Yes | Yes | `Query.BankStatement.cs` |
| finance | query | `buildingBankAccount` | Yes | Yes | Yes | `Query.BuildingBankAccount.cs` |
| finance | query | `cityLots` | No | No | Yes | `Query.Exchange.cs` |
| finance | query | `companyBankAccounts` | Yes | No | Yes | `Query.BuildingBankAccount.cs` |
| finance | query | `companyCityFinancialBreakdown` | Yes | No | Yes | `Query.Ledger.cs` |
| finance | query | `companyLedger` | Yes | Yes | Yes | `Query.Ledger.cs` |
| finance | query | `eurFxRates` | No | No | Yes | `Query.Forex.cs` |
| finance | query | `forexQuote` | Yes | No | Yes | `Query.Forex.cs` |
| finance | query | `forexTradeHistory` | Yes | No | Yes | `Query.Forex.cs` |
| finance | query | `fxRateHistory` | No | No | Yes | `Query.Forex.cs` |
| finance | query | `getCities` | No | No | Yes | `Query.MultiCityExpansion.cs` |
| finance | query | `globalExchangeOffers` | No | No | Yes | `Query.Exchange.cs` |
| finance | query | `globalExchangeProductListings` | No | No | Yes | `Query.Exchange.cs` |
| finance | query | `ledgerDrillDown` | Yes | Yes | Yes | `Query.Ledger.cs` |
| finance | query | `lot` | No | Yes | Yes | `Query.Exchange.cs` |
| finance | query | `myBankAccounts` | Yes | No | Yes | `Query.BuildingBankAccount.cs` |
| finance | query | `myDeposits` | Yes | No | No | `Query.Banking.cs` |
| finance | query | `personAccount` | Yes | Yes | Yes | `Query.Auth.cs` |
| finance | query | `playerCurrencyBalances` | Yes | No | Yes | `Query.Forex.cs` |
| lending | mutation | `acceptLoan` | Yes | Yes | Yes | `Mutation.Lending.cs` |
| lending | mutation | `repayLoanDebt` | Yes | Yes | No | `Mutation.Lending.cs` |
| lending | query | `bankLoans` | Yes | No | Yes | `Query.Lending.cs` |
| lending | query | `loanOffers` | Yes | No | Yes | `Query.Lending.cs` |
| lending | query | `myCollateralBuildings` | Yes | No | Yes | `Query.Lending.cs` |
| lending | query | `myLoanOffers` | Yes | No | No | `Query.Lending.cs` |
| lending | query | `myLoans` | Yes | No | Yes | `Query.Lending.cs` |
| lending | query | `procurementPreview` | Yes | Yes | Yes | `Query.Lending.cs` |
| lending | query | `sourcingCandidates` | Yes | Yes | Yes | `Query.Lending.cs` |
| lending | query | `unitUpgradeInfo` | Yes | No | Yes | `Query.Lending.cs` |
| ranking | query | `additionalCompanyPrerequisites` | Yes | No | Yes | `Query.Rankings.Performance.cs` |
| ranking | query | `companyBrands` | Yes | Yes | Yes | `Query.Rankings.Performance.cs` |
| ranking | query | `companyRankings` | No | No | Yes | `Query.Rankings.cs` |
| ranking | query | `companySettings` | Yes | Yes | Yes | `Query.Rankings.Performance.cs` |
| ranking | query | `gameState` | No | No | Yes | `Query.Rankings.Performance.cs` |
| ranking | query | `myCompanies` | Yes | Yes | Yes | `Query.Rankings.Performance.cs` |
| ranking | query | `playerRankHistory` | No | No | Yes | `Query.PlayerProfile.cs` |
| ranking | query | `rankHistory` | No | No | Yes | `Query.PlayerProfile.cs` |
| ranking | query | `rankedProductTypes` | Yes | Yes | Yes | `Query.World.cs` |
| ranking | query | `rankings` | No | No | Yes | `Query.Rankings.cs` |
| ranking | query | `starterIndustries` | No | No | Yes | `Query.Rankings.Performance.cs` |
| shareholder | mutation | `buyShares` | Yes | Yes | Yes | `Mutation.StockExchange.cs` |
| shareholder | mutation | `cancelLimitOrder` | Yes | Yes | Yes | `Mutation.StockExchange.LimitOrders.cs` |
| shareholder | mutation | `placeLimitOrder` | Yes | No | Yes | `Mutation.StockExchange.LimitOrders.cs` |
| shareholder | mutation | `proposeDividend` | Yes | Yes | No | `Mutation.StockExchange.DividendGovernance.cs` |
| shareholder | mutation | `sellShares` | Yes | Yes | Yes | `Mutation.StockExchange.cs` |
| shareholder | mutation | `voteDividendProposal` | Yes | Yes | Yes | `Mutation.StockExchange.DividendGovernance.cs` |
| shareholder | query | `companyShareholders` | Yes | No | Yes | `Query.StockExchange.cs` |
| shareholder | query | `dividendProposals` | Yes | No | No | `Query.StockExchange.DividendGovernance.cs` |
| shareholder | query | `myDividendVotes` | Yes | No | No | `Query.StockExchange.DividendGovernance.cs` |
| shareholder | query | `myOpenDividendProposalCount` | Yes | No | No | `Query.StockExchange.DividendGovernance.cs` |
| shareholder | query | `myOpenOrders` | Yes | No | Yes | `Query.StockExchange.cs` |
| shareholder | query | `orderBook` | No | No | No | `Query.StockExchange.cs` |
| shareholder | query | `stockExchangeListings` | No | Yes | Yes | `Query.StockExchange.cs` |
| shareholder | query | `stockExchangePriceHistory` | No | No | Yes | `Query.StockExchange.cs` |
| shareholder | query | `stockTradeHistory` | No | No | Yes | `Query.StockExchange.cs` |
