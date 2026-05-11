# 2026-W20 Security Audit

Date: 2026-05-11  
Scope: `projects/Api`, `projects/MasterApi`, `projects/frontend`, new GoldAmm / LimitOrder / DividendGovernance surfaces, bot/API-key ownership guard coverage, and dependency advisories.

## Audit question

Can one player gain an unfair advantage over another player by executing an API call or exploiting unfair game mechanics?

## Summary

- Reviewed all new `Query.*.cs` and `Mutation.*.cs` files added since the W19 audit: `Mutation.GoldAmm.cs`, `Mutation.GoldAmm.Swap.cs`, `Query.GoldAmm.cs`, `Mutation.StockExchange.LimitOrders.cs`, `Mutation.StockExchange.DividendGovernance.cs`, `Query.StockExchange.DividendGovernance.cs`, `Mutation.MediaHouse.cs`, `Mutation.MultiCityExpansion.cs`, `Query.Referrals.cs`, `Query.Notifications.cs`, `Mutation.UnitUpgrade.cs`.
- Found that the `BotOwnershipGuard` switch statement does not cover the new GoldAmm financial mutations (`createGoldAmmPool`, `addGoldAmmLiquidity`, `removeGoldAmmLiquidity`, `executeGoldAmmSwap`) or the LimitOrders mutations (`placeLimitOrder`, `cancelLimitOrder`). A compromised bot/API-key can therefore invoke these financial operations against the owner's gold and fiat reserves without the deny-by-default ownership layer.
- Confirmed that the MasterApi news trust boundary (Critical, W19) and frontend npm dependency advisories (High, W19) remain unresolved.
- Partial progress on object-authorization error standardization: PR #393 resolved stock/lending paths; game-wide coverage is still in progress.
- No new vulnerable NuGet packages found in `projects/Api` or `projects/MasterApi` from the local dependency scan.
- Frontend npm audit continues to report 14 vulnerabilities (7 high, 7 moderate) including `undici`, `yaml`, `path-to-regexp`, `minimatch`, `postcss`, and `dompurify`.

## Risk register

### 1) GoldAmm financial mutations unguarded in BotOwnershipGuard

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Types/Mutation.GoldAmm.cs`, `Mutation.GoldAmm.Swap.cs` → `createGoldAmmPool`, `addGoldAmmLiquidity`, `removeGoldAmmLiquidity`, `executeGoldAmmSwap`
- **Risk description:** The `BotOwnershipGuard.EnsureMutationOwnershipAsync` switch statement covers forex swaps, share trades, loans, building-market, and a handful of other high-value mutations, but it does not enumerate any of the new gold AMM operations. A bot API key that leaks can therefore call `executeGoldAmmSwap` to drain the player's fiat or gold reserves into an adversarially-controlled pool, add or remove the player's liquidity positions, and create new pools using the player's capital — all without any ownership gate. The per-mutation `[Authorize]` attribute enforces that the caller is authenticated but does not prevent lateral use of the authenticated identity by a non-owning bot.
- **Recommended fix:** Add `createGoldAmmPool`, `addGoldAmmLiquidity`, `removeGoldAmmLiquidity`, and `executeGoldAmmSwap` to the `BotOwnershipGuard` switch. For swap operations, guard the implied `currencyCode`/`direction`/`amount` combination against the caller's owned balances (or assert personal gold and fiat account ownership). For liquidity removal, verify the `positionId` belongs to the calling player.
- **Status:** Open <!-- issue: #394 -->

### 2) Limit-order and dividend-governance mutations unguarded in BotOwnershipGuard

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Types/Mutation.StockExchange.LimitOrders.cs` → `placeLimitOrder`, `cancelLimitOrder`; `Mutation.StockExchange.DividendGovernance.cs` → `proposeDividend`, `voteDividend`
- **Risk description:** `placeLimitOrder` reserves settlement-account balance immediately upon submission. `cancelLimitOrder` releases the reserved balance back to the settlement account. Neither is registered in `BotOwnershipGuard`. A stolen API key can therefore place large buy orders that drain a player's USD settlement account without their knowledge, or cancel outstanding sell orders to manipulate open market positions. Similarly, `proposeDividend` allows any authenticated caller (including bots) to submit governance proposals for companies they control — and because BotOwnershipGuard is silent on these operations, a leaked key can submit or vote on proposals for any company without triggering the ownership audit log.
- **Recommended fix:** Add `placeLimitOrder` and `cancelLimitOrder` to `BotOwnershipGuard` with bank-account and stock-symbol / ownership validation matching the interactive mutation checks. Add `proposeDividend` and `voteDividend` with company ownership validation.
- **Status:** Open <!-- issue: #394 -->

### 3) MasterApi news trust boundary: draft reads and upserts accept spoofable requester identity (carried from W19 Critical)

- **Severity:** Critical
- **Affected endpoint or mechanic:** `projects/MasterApi/Types/Query.cs` → `GetGameNewsFeed`; `projects/MasterApi/Types/Mutation.News.cs` → `UpsertGameNewsEntry`
- **Risk description:** Both endpoints call `EnsureServiceAccess(input, masterServerOptions, false, false)` which disables both registration-key and server-key enforcement. `GetGameNewsFeed(includeDrafts: true)` requires only a non-empty `RequesterEmail` string before returning unpublished drafts to any unauthenticated caller. `UpsertGameNewsEntry` derives the authorization level from `BuildGameAdministrationAccessAsync(..., requesterEmail)` where `requesterEmail` comes entirely from the mutation input — so any caller who knows a privileged administrator's email can publish global or server-scoped news entries. This is an active content-integrity risk against the player-facing news and changelog system. No regression tests existed at the time of the W19 audit and none have been added since.
- **Recommended fix:** Require validated game-server credentials (registration key + non-empty server key) for all `UpsertGameNewsEntry` calls. Require either the same trusted credentials or an authenticated JWT root/global-admin claim for `GetGameNewsFeed(includeDrafts: true)`. Stop accepting caller-supplied email as the sole authorization signal. Add focused regression tests in `projects/MasterApi.Tests` for anonymous draft-read rejection and spoofed-email rejection.
- **Status:** Open <!-- issue: #395 -->

### 4) Frontend npm dependency advisories remain unresolved (carried from W19)

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/frontend/package.json` — `undici`, `path-to-regexp`, `yaml`, `minimatch`, `postcss`, `dompurify` transitive chains
- **Risk description:** `npm audit --audit-level=moderate --omit=dev` for `projects/frontend` continues to report 14 vulnerabilities (7 high, 7 moderate). The high-severity advisories are concentrated in the `undici` transitive chain (`@vercel/node` dependency) and `path-to-regexp`. `DOMPurify` is on a reachable HTML-rendering surface in `NewsView.vue` (`v-html` with `DOMPurify.sanitize`). `yaml` is exposed via `yaml@2.0.0–2.8.2` (stack-overflow via deeply nested collections). No `npm audit fix` has been applied since the W19 audit.
- **Recommended fix:** Run `npm audit fix` for the no-breaking-change advisories (`yaml`, `minimatch`, `postcss`). Evaluate whether `@vercel/node` belongs in shipped frontend production dependencies; if it is only needed for API serverless functions, remove it from `dependencies` and add it to `devDependencies` or remove it entirely. Update `dompurify` to a non-advisory version. Add an `npm audit --audit-level=high --omit=dev` gate to the frontend CI workflow so new production advisories are caught before release.
- **Status:** Open <!-- issue: #395 -->

### 5) Object-level authorization response standardization still in progress (carried from W19)

- **Severity:** Medium
- **Affected endpoint or mechanic:** `projects/Api/Types/Mutation.BuildingMarket.cs`, `Mutation.BankAccountTransfer.cs`, `Mutation.Exchange.cs`, and other economy-sensitive mutations
- **Risk description:** PR #393 standardized stock-exchange and lending paths to return `NOT_FOUND_OR_NOT_OWNED` instead of object-revealing responses. However, the building market (`COMPANY_NOT_FOUND`, `OFFER_NOT_FOUND`, `BUILDING_NOT_FOR_SALE`, `BUILDING_NOT_FOUND`) and bank-transfer paths still return distinct error codes that allow callers to determine whether a foreign company, offer, or building exists. Precise `Available: ...` balance disclosures remain in some mutation error messages, enabling competitive-intelligence scraping against rival account states.
- **Recommended fix:** Extend the `NOT_FOUND_OR_NOT_OWNED` pattern to building-market, bank-transfer, and exchange paths. Suppress precise balance amounts from error messages and log them server-side only.
- **Status:** In-Progress <!-- issue: #393 -->

### 6) Player API keys still have full-account scope (carried from W19)

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Security/ApiKeyAuthMiddleware.cs`, `projects/Api/Types/Mutation.ApiKey.cs`
- **Risk description:** API keys currently inherit the full effective player identity. A leaked bot/API key can still operate every company and personal action the player can perform rather than being restricted to a narrow automation scope. The `BotOwnershipGuard` mitigates exploitation paths already registered in its switch statement, but newly added financial mutations (GoldAmm, LimitOrders) are not yet registered, widening the blast radius for leaked keys.
- **Recommended fix:** Add per-key scopes (read-only, company-bound, bot-only, trading-only), expose them in issuance/revocation UI, and enforce them in middleware and sensitive mutations.
- **Status:** Open <!-- issue: #391 -->

### 7) FX execution fairness depends on stale-quote handling (carried from W19)

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Types/Mutation.Forex.cs`, `projects/Api/Types/Query.Forex.cs`
- **Risk description:** If quote freshness, slippage tolerance, or replay resistance is too loose, players can race stale quotes or script around UI latency to gain better-than-intended execution.
- **Recommended fix:** Keep all settlement server-priced, require quote timestamps/slippage bounds on execution inputs, and add explicit replay/race-condition tests around concurrent swaps.
- **Status:** Open <!-- issue: #391 -->

### 8) Building secondary-market race conditions need continuous review (carried from W19)

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Types/Mutation.BuildingMarket.cs`, `projects/Api/Types/Mutation.RealEstate.cs`
- **Risk description:** A concurrent buy/offer/cancel sequence on the same asset can create unfair outcomes if listing state, offer state, or collateral state is not rechecked atomically at commit time.
- **Recommended fix:** Preserve optimistic/concurrency checks on listing acceptance paths and add more multi-request integration tests for simultaneous buyers.
- **Status:** In-Progress <!-- issue: #389 -->

### 9) Loan collateral and missed-payment foreclosure remain exploit-sensitive (carried from W19)

- **Severity:** High
- **Affected endpoint or mechanic:** `projects/Api/Types/Mutation.Lending.cs`, `projects/Api/Types/Mutation.RealEstate.cs`, loan/tax phases
- **Risk description:** Collateral state, foreclosure listing locks, and missed-payment handling are economically sensitive. Any stale ownership check or missing balance revalidation could let borrowers shield assets or escape penalties.
- **Recommended fix:** Keep collateral checks on every listing/destroy/refinance path and expand regression coverage for overdue/defaulted transitions.
- **Status:** In-Progress <!-- issue: #379 -->

### 10) Master-vs-game token boundary confusion would be privilege-critical (carried from W19)

- **Severity:** Critical
- **Affected endpoint or mechanic:** shared auth claims, effective-player mapping, MasterApi admin access
- **Risk description:** Any future regression that treats any effective-player claim as impersonation or accepts the wrong issuer/audience would let users cross privilege boundaries between the game shard and the master admin surface.
- **Recommended fix:** Keep issuer/audience separation strict, verify effective-vs-actor claim comparisons in tests, and reject master-only privilege on game tokens unless explicitly mapped.
- **Status:** Open <!-- issue: #313 -->

### 11) Bot automation can still overreach if company boundaries drift (carried from W19)

- **Severity:** High
- **Affected endpoint or mechanic:** API keys, bot automation flows, company/account mutations
- **Risk description:** Bots are valuable automation tools but become unfair if a bot key can execute swaps, building changes, or company actions outside its intended owner or account context. The BotOwnershipGuard gap in GoldAmm/LimitOrders (findings #1 and #2 above) is a direct manifestation of this risk.
- **Recommended fix:** Add negative tests for foreign-company access on all bot-eligible flows and bind future bot keys to explicit company/account scopes.
- **Status:** Open <!-- issue: #389 -->

### 12) Frontend trust boundaries must stay non-authoritative (carried from W19)

- **Severity:** High
- **Affected endpoint or mechanic:** Vue forms for prices, layouts, account selection, and bank/FX/stock actions
- **Risk description:** If the backend ever starts trusting client-supplied prices, ownership IDs, or upgrade timing fields, a player could bypass economic rules from browser devtools or scripted calls.
- **Recommended fix:** Keep all derived prices, timing, balances, ownership, and progression state server-controlled and continue rejecting client attempts to override them.
- **Status:** Open <!-- issue: #391 -->

### 13) Leaderboard and ranking manipulation risk remains proof-sensitive (carried from W19)

- **Severity:** Medium
- **Affected endpoint or mechanic:** `projects/MasterApi/Types/Mutation.Ranking.cs`, ranking proof moderation, telemetry service
- **Risk description:** Competitive ranking rewards can still be distorted if event uniqueness keys, proof references, or moderation workflows are too permissive.
- **Recommended fix:** Continue strengthening uniqueness rules, require shard identity, and add moderation dashboards/tests for suspicious duplicate patterns.
- **Status:** In-Progress

## Verified mutation ownership review

The following mutation areas were spot-checked during this audit for player/company/building/unit ownership enforcement:

- `Mutation.GoldAmm.cs` / `Mutation.GoldAmm.Swap.cs` — pool operations are behind `[Authorize]` but not registered in `BotOwnershipGuard` (see finding #1)
- `Mutation.StockExchange.LimitOrders.cs` — `cancelLimitOrder` uses `objectAuthorization.RequireOwnedAsync` correctly; `placeLimitOrder` resolves the active trading account from the authenticated principal; neither is in `BotOwnershipGuard` (see finding #2)
- `Mutation.StockExchange.DividendGovernance.cs` — `proposeDividend` and `voteDividend` are behind `[Authorize]` and resolve the active trading account from the principal; shareholder eligibility is verified server-side before a vote is recorded; not in `BotOwnershipGuard`
- `Mutation.MediaHouse.cs` → `setMediaHouseContentBudget` — verifies `building.Company.PlayerId == userId`; government-owned buildings are blocked
- `Mutation.MultiCityExpansion.cs` → `unlockCity` — verifies city existence and player ownership of at least one building; read-only check, no write risk
- `Mutation.UnitUpgrade.cs` → `scheduleUnitUpgrade` — verifies `unit.Building.Company.PlayerId == userId`
- `Query.Referrals.cs` → `getMyReferralProgram` — scoped to `userId` from principal
- `Query.Notifications.cs` → `playerNotificationInbox`, `playerNotificationUnreadCount` — both scoped to `playerId` from principal

Representative automated ownership coverage:

- `projects/Api.Tests/BotOwnershipGuardTests.cs` — covers forex, shares, loan, building-market, building-destroy/sale paths
- `projects/Api.Tests/BuildingSecondaryMarketTests.cs` — concurrent offer acceptance
- `projects/Api.Tests/GraphQlIntegrationTests.cs` — cross-player mutation rejection paths

## Prior audit status updates

- W19 finding #3 (Object-level authorization response standardization): PR #393 merged, standardizing stock/lending paths — status updated to **In-Progress** for remaining mutation surfaces.
- W19 findings #1–#3 (loanOffers auth, companyShareholders auth, ingestRankingEvent serverKey): all confirmed **Resolved** in code.

## Manual checklist for next weekly audit

- [ ] Verify `BotOwnershipGuard` covers `createGoldAmmPool`, `addGoldAmmLiquidity`, `removeGoldAmmLiquidity`, `executeGoldAmmSwap`, `placeLimitOrder`, `cancelLimitOrder`, `proposeDividend`, and `voteDividend`.
- [ ] Confirm MasterApi news trust boundary is remediated: run an unauthenticated `GetGameNewsFeed(includeDrafts: true)` call against a staging instance and confirm it is rejected.
- [ ] Confirm frontend npm advisory count has decreased; at minimum verify `@vercel/node` is removed from shipped production dependencies.
- [ ] Review every new `Query.*.cs` and `Mutation.*.cs` file added since this audit.
- [ ] Confirm every player/company/building/bank mutation resolves ownership from server-side data, not client input.
- [ ] Re-run unauthenticated requests against sensitive finance/shareholder/admin queries.
- [ ] Review API key issuance and confirm no new mutations bypass scope or ownership checks.
- [ ] Review FX, loan, stock, and building-market execution paths for concurrency or stale-quote issues.
- [ ] Review MasterApi service-input mutations for registration key and server key validation.
- [ ] Review shared-auth claim handling for issuer/audience/effective-player regressions.
- [ ] Check error messages for sensitive-object enumeration leaks.
- [ ] Convert newly discovered High/Critical findings into GitHub issues before closing the audit.
- [ ] Add the next weekly report under `/audits/` and update statuses of prior findings.
