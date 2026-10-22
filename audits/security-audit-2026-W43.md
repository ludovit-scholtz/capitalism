# 2026-W43 Security Audit

Date: 2026-10-22
Revision audited: `e1461ec6` (`main`, after PRs #648, #651 and #653)
Auditor: Claude (Team A), as part of issue #652.
Scope: the changes merged since `security-audit-2026-W42.md` (first-week campaign, object-access denial viewer, city-unlock cadence, Flutter chat), status of the W42 findings, and dependency verification. This is not a full re-test of every economy path; the last deep fairness pass remains `security-audit-2026-06-20T204234.md`. Findings are by code reading plus the test runs named below; no exploit was executed against a running server.

## Audit question

Can one player gain an unfair advantage over another player by executing an API call or exploiting unfair game mechanics, and is the shipped configuration still hardened?

## Summary

- No High or Critical findings.
- One new Medium finding: the city-unlock net worth ignores loan liabilities, so borrowing can open a gated city (finding 1, follow-up #654).
- Both W42 findings are resolved or accepted (see below); nothing carried over as Open.
- `npm audit --omit=dev` reports 0 vulnerabilities in both frontends; `dotnet list package --vulnerable --include-transitive` is clean for Api, MasterApi, Shared and Api.Tests.
- The four items the Product Owner asked to look at (campaign claim, denial viewer, unlock cadence, Flutter chat) are covered below; only the unlock review produced a finding, and it predates the cadence change.

## Changes since W42

| Area | Result |
|------|--------|
| `firstWeekCampaign` / `claimCampaignReward` (#651) | Pass. Both are `[Authorize]`; the query has no id argument (always the caller's own campaign). The mutation resolves an explicit `companyId` through `ObjectAuthorizationService.RequireOwnedAsync`, so a foreign and a missing company are the same `FORBIDDEN` and are recorded as a denial. Completion and unlock are recomputed server-side from existing data. The unique `(PlayerId, GoalCode)` index is the only guard against a replayed claim, and it is enough: the claim row, credit and ledger entry are one `SaveChanges`, and the loser of a race gets `Claimed = false`. `FirstWeekCampaignConcurrencyPostgresTests` (same-goal 4-way race: one winner, paid once; different goals on one account: all pay) was re-run on PostgreSQL 16: 2 of 2 passed. |
| `objectAccessDenials` (#392, `74dbcde9`) | Pass. `[Authorize]` plus `RequireAdminDashboardAccessAsync` before any read; limit clamped to 500. The recorded event holds actor id, API key id, object type, object id and reason, never object contents, and the data is process-local memory (a restart or another replica starts a fresh window, documented on the resolver). |
| CityExpansionUnlock every 6 ticks (#653) | Pass, no timing window. The purchase gate does not read the persisted unlock row alone: `CityUnlockService.GetStatusForCityAsync` recomputes `currentNetWorthUsd >= thresholdUsd` live on every `buyLot`, so the cadence only delays the stored `CompanyCityUnlock` row and the notification by up to 5 in-game hours. Nobody can buy earlier or later because of it. The review did surface finding 1, which is independent of the cadence. |
| Flutter in-game chat (#648) | Pass. The app uses the same `sendChatMessage` mutation as the web: `[Authorize]`, Bearer JWT of the signed-in player (no extra scope is granted to the mobile token), content trimmed, empty rejected, 500-character cap, and `ChatRateLimitService` (5 messages per 10 seconds per player, counted before the insert so concurrent sends are counted atomically). `chatMessages` and the `chatMessageSent` subscription are `[Authorize]`. `postBridgedChatMessage` is unauthenticated by JWT by design and gated by the master registration key plus the server key; it truncates instead of rejecting and has no rate limit (finding 3). |

## Verification of W42 findings

| # | Finding | Result |
|---|---------|--------|
| 1 | Master portal inline analytics blocked by CSP | Fixed in W42 (#597), unchanged. |
| 2 | Object-authorization responses not uniform everywhere (#605) | Resolved. Issue #605 is closed. The 11 inline `COMPANY_NOT_FOUND` throws still present were each read: stock-exchange sites look up a public target company (no ownership to leak), `Mutation.RealEstate.Rent.cs`, `Mutation.Onboarding.Finish.cs` and `Mutation.ApiKey.cs` filter by owner and return one uniform message for missing and foreign. The only residue is that those three emit no denial audit event, which is a visibility gap, not an enumeration channel (finding 4). |
| 3 | Lint-toolchain advisories (#611) | Accepted risk, issue closed. `npm view braces version` is still `3.0.3`, still inside the vulnerable range, so the dev-only exposure is unchanged. Keep re-checking weekly; do not run `npm audit fix --force`. |

## Risk register

### 1) City unlock net worth ignores loan liabilities

- **Severity:** Medium (progression gate; no direct money creation)
- **Affected endpoint or mechanic:** `CityUnlockService.ComputeCompanyNetWorthUsd`, used by the live gate in `GetStatusForCityAsync` (lot purchase, onboarding helpers, `cityUnlockStatus`) and by `CityExpansionUnlockPhase`.
- **Risk description:** Net worth is bank balances plus building, lot and inventory value, with no deduction for outstanding loans, while collateral lending credits the borrower's account in full. A company just under a city's threshold can borrow against a building, pass the live check and buy a lot there. Owning that lot adds the city to the owned-city set, which counts as unlocked, so the access outlives the loan. A player who stays honest about net worth is therefore at a disadvantage to one who borrows to cross the line.
- **Recommended fix:** Subtract the remaining principal of active loans (in USD) in a shared helper used by both callers, and stop treating a lot bought after the change as proof of unlock. Tests: just-below-threshold company that takes a loan is rejected on `buyLot`; repaying changes nothing; already-unlocked companies keep access.
- **Status:** Open <!-- issue: #654 -->

### 2) First-week campaign rewards scale with account count

- **Severity:** Low
- **Affected endpoint or mechanic:** `claimCampaignReward` (the week pays $56,000 per account).
- **Risk description:** Several goals complete from actions a second account can perform for the first (a first B2B trade, observing a competitor, a loan), and each new account is paid its own $56,000, roughly 28% of the $200,000 starting capital. The reward is credited to the new account's own company and cannot be transferred out except through normal trading, so the exposure is bounded and already limited by account creation and onboarding cost, but it is not zero.
- **Recommended fix:** None needed now. If farming shows up, require the goal's counterpart to be a different player with a minimum account age, and watch `CAMPAIGN_REWARD` volume per player on the Operations dashboard.
- **Status:** Accepted

### 3) Bridged chat endpoint compares keys with ordinary string equality and is unthrottled

- **Severity:** Low
- **Affected endpoint or mechanic:** `postBridgedChatMessage` (`Mutation.ChatBridge.cs`).
- **Risk description:** The registration and server keys are compared with `string.Equals(..., Ordinal)`, which is not constant time, and the mutation has no rate limit. Both keys are long random server secrets and the endpoint is reachable only with them, so practical impact is low; a leaked key would allow chat spam as any player resolved by email.
- **Recommended fix:** Use `CryptographicOperations.FixedTimeEquals` on the UTF-8 bytes and reuse `IChatRateLimitService` keyed by the resolved player.
- **Status:** Open

### 4) Denial audit events missing for three owner-filtered lookups

- **Severity:** Low
- **Affected endpoint or mechanic:** `Mutation.RealEstate.Rent.cs`, `Mutation.Onboarding.Finish.cs`, `Mutation.ApiKey.cs` (allowed company ids).
- **Risk description:** Responses are uniform, so there is no enumeration, but a probing player does not show up in `objectAccessDenials` for these three paths.
- **Recommended fix:** Route them through `ObjectAuthorizationService.RequireOwnedAsync` with parity tests, as in #604 and #605.
- **Status:** Open

### 5) Lint-toolchain advisories in both frontends

- **Severity:** Low (development only)
- **Affected endpoint or mechanic:** devDependencies of `projects/frontend` and `projects/master-frontend` (`braces` 3.0.3, GHSA-vfj7-8cjw-p6xm).
- **Risk description:** Unchanged from W42; reachable only when linting developer-controlled files, never shipped.
- **Recommended fix:** Re-check `npm view braces version` weekly; adopt the first release outside `<=3.0.3`.
- **Status:** Accepted
