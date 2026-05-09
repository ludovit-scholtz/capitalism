# 2026-W19 Security Audit

Date: 2026-05-09  
Scope: `projects/Api`, `projects/MasterApi`, `projects/frontend`, auth boundaries, ranking telemetry, bank/loan flows, exchange flows, and building ownership controls.

## Audit question

Can one player gain an unfair advantage over another player by executing an API call or exploiting unfair game mechanics?

## Summary

- Reviewed GraphQL query/mutation authorization boundaries in `projects/Api/Types/` and `projects/MasterApi/Types/`.
- Verified representative ownership protections already exist on cross-player mutations such as building sale, loan collateral, bank rate updates, building configuration, public sales configuration, and trade routes.
- Implemented three immediate hardenings in this audit:
  1. `loanOffers` now requires authentication.
  2. `companyShareholders` now requires authentication.
  3. `ingestRankingEvent` now requires a non-empty `serverKey`.

## Risk register

### 1) Unauthenticated loan-offer intelligence leak

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Types/Query.Lending.cs` → `loanOffers`
- **Risk description:** Before this audit, unauthenticated callers could enumerate active bank loan offers, lender company names, rates, and remaining capacity without logging in. That exposed competitive financing intelligence to scrapers and rival analysts without any player session.
- **Recommended fix:** Require authenticated access for `loanOffers` and keep borrower-specific drill-downs behind owned/company-scoped queries.
- **Status:** Resolved

### 2) Unauthenticated shareholder-graph leak

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Types/Query.StockExchange.cs` → `companyShareholders`
- **Risk description:** Before this audit, anyone could query shareholder lists for a company, including holder names, holder types, ownership percentages, and linked player/company IDs. That made it too easy to map ownership networks and identify acquisition targets without authentication.
- **Recommended fix:** Require authentication for shareholder breakdown access and continue reviewing whether future tightening should limit the data to owners, shareholders, or admins only.
- **Status:** Resolved

### 3) Ranking telemetry accepted events without shard identity

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/MasterApi/Types/Mutation.Ranking.cs` → `ingestRankingEvent`
- **Risk description:** Ranking event ingestion previously required the shared registration key but did not require a `serverKey`. That weakened shard attribution and made spoofed or misrouted telemetry easier if the registration secret leaked.
- **Recommended fix:** Require a non-empty `serverKey` for ranking ingestion and keep extending server-side shard validation.
- **Status:** Resolved

### 4) Player API keys still have full-account scope

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Security/ApiKeyAuthMiddleware.cs`, `projects/Api/Types/Mutation.ApiKey.cs`
- **Risk description:** API keys currently inherit the full effective player identity. A leaked bot/API key can still operate every company and personal action the player can perform, rather than being restricted to a narrow automation scope.
- **Recommended fix:** Add per-key scopes (read-only, company-bound, bot-only, trading-only), expose them in issuance/revocation UI, and enforce them in middleware plus sensitive mutations.
- **Status:** Open

### 5) FX execution fairness depends on stale-quote handling

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Types/Mutation.Forex.cs`, `projects/Api/Types/Query.Forex.cs`
- **Risk description:** If quote freshness, slippage tolerance, or replay resistance is too loose, players can race stale quotes or script around UI latency to gain better-than-intended execution.
- **Recommended fix:** Keep all settlement server-priced, require quote timestamps/slippage bounds on execution inputs, and add explicit replay/race-condition tests around concurrent swaps.
- **Status:** Open

### 6) Building secondary-market race conditions need continuous review

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Types/Mutation.BuildingMarket.cs`, `projects/Api/Types/Mutation.RealEstate.cs`
- **Risk description:** A concurrent buy/offer/cancel sequence on the same asset can create unfair outcomes if listing state, offer state, or collateral state is not rechecked atomically at commit time.
- **Recommended fix:** Preserve optimistic/concurrency checks on listing acceptance paths and add more multi-request integration tests for simultaneous buyers.
- **Status:** In-Progress

### 7) Loan collateral and missed-payment foreclosure remain exploit-sensitive

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Types/Mutation.Lending.cs`, `projects/Api/Types/Mutation.RealEstate.cs`, loan/tax phases
- **Risk description:** Collateral state, foreclosure listing locks, and missed-payment handling are economically sensitive. Any stale ownership check or missing balance revalidation could let borrowers shield assets or escape penalties.
- **Recommended fix:** Keep collateral checks on every listing/destroy/refinance path and expand regression coverage for overdue/defaulted transitions.
- **Status:** In-Progress

### 8) Master-vs-game token boundary confusion would be privilege-critical

- **Severity:** Critical
- **Affected endpoint or mechanic:** shared auth claims, effective-player mapping, MasterApi admin access
- **Risk description:** Any future regression that treats any effective-player claim as impersonation or accepts the wrong issuer/audience would let users cross privilege boundaries between the game shard and the master admin surface.
- **Recommended fix:** Keep issuer/audience separation strict, verify effective-vs-actor claim comparisons in tests, and reject master-only privilege on game tokens unless explicitly mapped.
- **Status:** Open

### 9) Bot automation can still overreach if company boundaries drift

- **Severity:** High
- **Affected endpoint or mechanic:** API keys, bot automation flows, company/account mutations
- **Risk description:** Bots are valuable automation tools but become unfair if a bot key can execute swaps, building changes, or company actions outside its intended owner or account context.
- **Recommended fix:** Add negative tests for foreign-company access on all bot-eligible flows and bind future bot keys to explicit company/account scopes.
- **Status:** Open

### 10) Leaderboard and ranking manipulation risk remains proof-sensitive

- **Severity:** Medium
- **Affected endpoint or mechanic:** `projects/MasterApi/Types/Mutation.Ranking.cs`, ranking proof moderation, telemetry service
- **Risk description:** Competitive ranking rewards can still be distorted if event uniqueness keys, proof references, or moderation workflows are too permissive.
- **Recommended fix:** Continue strengthening uniqueness rules, require shard identity, and add moderation dashboards/tests for suspicious duplicate patterns.
- **Status:** In-Progress

### 11) Frontend trust boundaries must stay non-authoritative

- **Severity:** High
- **Affected endpoint or mechanic:** Vue forms for prices, layouts, account selection, and bank/FX/stock actions
- **Risk description:** If the backend ever starts trusting client-supplied prices, ownership IDs, or upgrade timing fields, a player could bypass economic rules from browser devtools or scripted calls.
- **Recommended fix:** Keep all derived prices, timing, balances, ownership, and progression state server-controlled and continue rejecting client attempts to override them.
- **Status:** Open

### 12) Error-message detail must stay minimally revealing

- **Severity:** Medium
- **Affected endpoint or mechanic:** GraphQL error responses across banking, lending, building, and admin flows
- **Risk description:** Overly specific authorization failures can help attackers confirm whether a foreign company, building, or bank exists even when they cannot modify it.
- **Recommended fix:** Prefer generic “not found or not owned” style errors for object-level authorization failures and avoid leaking extra foreign IDs or ledger details.
- **Status:** In-Progress

## Verified mutation ownership review

The following mutation areas were spot-checked during this audit for player/company/building ownership enforcement:

- `Mutation.BuildingConfiguration.cs`
- `Mutation.BuildingMarket.cs`
- `Mutation.Company.cs`
- `Mutation.Lending.cs`
- `Mutation.RealEstate.cs`
- `Mutation.PublicSales.cs`
- `Mutation.TradeRoutes.cs`
- `Mutation.PowerPlant.cs`
- `Mutation.BankDepositRate.cs`
- `Mutation.BuildingBankAccount.cs`

Representative automated coverage already exists for cross-player mutation protection, including:

- `projects/Api.Tests/BuildingSecondaryMarketTests.cs` → non-owner building sale rejection
- `projects/Api.Tests/GraphQlIntegrationTests.cs` → foreign collateral/building/bank mutation rejection paths

## Manual checklist for next weekly audit

- [ ] Review every new `Query.*.cs` and `Mutation.*.cs` file added since the last audit.
- [ ] Confirm every player/company/building/bank mutation resolves ownership from server-side data, not client input.
- [ ] Re-run unauthenticated requests against sensitive finance/shareholder/admin queries.
- [ ] Review API key issuance and confirm no new mutations bypass scope or ownership checks.
- [ ] Review FX, loan, stock, and building-market execution paths for concurrency or stale-quote issues.
- [ ] Review MasterApi service-input mutations for registration key and server key validation.
- [ ] Review shared-auth claim handling for issuer/audience/effective-player regressions.
- [ ] Check error messages for sensitive-object enumeration leaks.
- [ ] Convert newly discovered High/Critical findings into GitHub issues before closing the audit.
- [ ] Add the next weekly report under `/audits/` and update statuses of prior findings.
