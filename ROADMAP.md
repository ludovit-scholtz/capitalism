# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

Everything previously tracked here (onboarding simplification, copy/paste on mobile, news pagination, Azure e-mail + weekly report, account deletion, the 2026 security hardening batch, and every screen of the Flutter app through Operations) is delivered and was spot-checked against the code on 2026-10-15. History lives in `CHANGELOG.csv` and git; per-screen trims are documented in the top-of-file comment of each Flutter file. This file now lists only open and proposed work. Product requirements live in `PRODUCT-DEFINITION.md`.

## 1. Carry-over: known gaps from delivered work

These were explicitly trimmed or left open in items that were closed. They are the only real leftovers.

### Flutter app

- [x] (100%) **Real in-game Chat (Flutter).** `lib/features/chat/` now has `ChatService` (the same `chatMessages` / `sendChatMessage` operations as the web), `ChatState` (history, send with RATE_LIMITED / MESSAGE_TOO_LONG mapping, global and selected-city channels, polling every 3 s while open and every 20 s in the background, persisted last-seen marker and unread count) and `ChatView`, shared by the Chat bottom sheet and the dashboard Chat tab. The drawer Chat item shows the unread badge; admin-hidden authors appear as "Message removed" to others and with a note to the author; Discord-bridged messages arrive through the same query. Strings are in the en/sk/de ARB files. Tests: models, state (unread, send, channels, sign-out, polling) and widgets. Not included: a push channel (the web polls too) and push notifications (separate item).
- [ ] **Live-server verification pass** against stage (read-only): login via real Biatec IdP, onboarding resume, dashboard, one tick of a building. Auth was only validated with fakes. Also exercise macOS/Linux OIDC loopback, which was never run.
- [ ] **Localize the whole app.** `en/sk/de` plumbing exists, but only a few screens use it (Tutorial milestone copy is hard-coded English, and so are many Operations and Building strings). Move all user-visible strings to ARB files and add a test that fails on new hard-coded `Text('...')` literals.
- [ ] **Split files over the 500-line budget:** `building_detail_screen.dart` (843), `app_router.dart` (639), `onboarding_screen.dart` (638), `auth_screens.dart` (631), `buy_building_screen.dart` (574).
- [ ] **Remaining functional trims**, in priority order:
  - Building Detail: media-house brand-impact analytics, power-plant per-tick P&L chart, seasonal-outlook quarter forecast and full demand-drivers list.
  - Player Profile: rank-history chart (currently a list), `GenderPicker`/regenerate-name flow.
  - News: render rich HTML (currently stripped to text).
  - Operations: product-analytics CSV export, `OFFER_VERSION_CONFLICT`-style conflict UX where still plain errors.
  - Dashboard: currency-aware cash on Buildings-tab tiles.
  - Scattered per-screen company pickers (`city_tab_screens.dart` bid dialog, `market_screens.dart`, `contracts_screen.dart`) should default from `AccountContextState` like Loan Request and Building Market already do.
  - Country flags as images (`country_flags` or bundled assets).
- [ ] **Push notifications** (FCM/APNs) for the events the server already creates as `PlayerNotification` rows: loan nearing maturity, construction/upgrade done, mine depleted/replenished, share-price move, competitor in my city. Needs a device-token mutation, a server-side sender behind the existing alert-threshold settings, and an opt-out per type.

### Web / platform

- [ ] **Burn down the 500-line rule violations.** The hard rule is not met today. Worst offenders: `composables/useBuildingDetail.ts` (5,639), `views/OnboardingView.vue` (2,147), `CityLotDetailPanel.vue` (1,381), `StockMarketListingRow.vue` (1,339), `AppHeader.vue` (1,181), `BuildingReadonlySidebar.vue` (1,171), `BuyBuildingSteps.vue` (1,016), `LedgerMainContent.vue` (1,002); backend: `Mutation.BuildingMarket.cs` (897), `MasterApi/Types/Query.cs` (842), `MasterApi/Program.cs` (813), `ApiKeyScopeMiddleware.cs` (785), `Api/Program.cs` (741). Add a CI check that fails on any new file over 500 lines (with an allow-list that only shrinks) so the debt cannot grow.
  - Status (~15%): the CI guard is live (`.github/workflows/file-size-guard.yml`, `.github/scripts/file-size-guard.mjs`, allow-list `.github/file-size-allowlist.json` that can only shrink; 152 files listed at the start). Remaining: actually splitting the allow-listed files, starting with `useBuildingDetail.ts`, `OnboardingView.vue` and the backend files over the limit.
- [ ] **Make the CI audit jobs required checks.** The branch-protection API returns 404 for `main` (no protection rules visible), so `frontend-dependency-audit.yml`, the audit-board gate and the API/frontend builds are not enforced; a repository admin has to enable required status checks.
- [ ] (80%) **Security audit cadence.** The weekly reminder workflow runs (issues #569-#588 were opened and closed on schedule); the missing piece was the reports themselves. `audits/security-audit-2026-W42.md` closes the gap since July, `audits/README.md` documents cadence and naming, and the reminder text now points at the real file name. Remaining: nothing automatically authors the weekly report, so a recurring auditor run is still needed.

## 2. Proposed: make the product match its own definition

Places where `PRODUCT-DEFINITION.md` is stale, contradictory, or promises something with no automated proof.

- [x] (100%) **Clean up the product definition.** Prices now follow `MasterApi/Types/Mutation.cs` (Pro `$20` equiv. = 0.137 g/month, Startup pack `$40` equiv. = 0.274 g, one claim, 3 months of Pro, no in-game currency); the tutorial section lists the eight real milestones (`FIRST_MILLION` is an achievement badge, not a milestone); Q4 is Oct-Dec; the Unit price section is complete; deposit-rate management is no longer marked planned; a verification date sits at the top. `.github/scripts/check-product-definition-prices.mjs` (CI workflow `product-definition-guard`) fails when the document's gold prices drift from the code. The unused game-frontend `startupPack` locale block with the wrong prices was removed.
- [ ] **Automate the tick performance requirement** (1,000 concurrent users, 20,000 buildings, 500,000 units in under 1 s). Add a seeded tick benchmark (BenchmarkDotNet or a timed integration test) with a CI budget and a per-phase timing breakdown in the operations dashboard, so regressions in the 40+ tick phases are visible before players feel them.
  - Status (~80%): per-phase tick timings (including `(context load)` and `(save changes)`) are recorded and shown in Operations Overview, a deterministic world generator and a CI-budgeted benchmark exist (1/50 slice on InMemory; a PostgreSQL benchmark runs in `postgres-validation.yml`; `TICK_BENCH_SCALE=medium|full` locally), NpcCompany queries are batched and the quadratic asset-value recalculation is gone. Profiling on PostgreSQL showed `SaveChanges` dominating because ~99% of written rows were per-unit LABOR/ENERGY ledger entries; operating costs are now booked as one labor and one energy row per building (R&D units keep per-unit rows), which also fixed a ledger-vs-balance mismatch while a global cost event was active (#630). **Measured on PostgreSQL 16 at 2,000 buildings / 50,000 units: 10.7 s -> 2.9 s per tick (save 7.8 s -> 1.1 s, operating-cost phase 391 -> 33 ms).** Remaining for the 1 s target at 20,000 buildings: the other phases (Purchasing, PublicSales, NpcCompany, context load, ~1.5 s at 2,000 buildings) scale linearly, so they need batching/caching and the remaining ledger rows need the same treatment where they are per-unit.
- [ ] **Multi-account abuse detection** is specified for the admin dashboard. Verify what `Query.Admin.cs` actually flags, and add concrete signals: shared IP/device clusters, one-way money funnels between accounts (stock trades, B2B sales, loans at off-market rates), repeated referral loops. Output: a ranked suspicion list with evidence links and an audit-logged "reviewed" state.
- [ ] **Property/regression test for "no exploit path".** Add an invariant suite run after N simulated ticks with random player/NPC actions: total money across bank accounts equals minted minus burned, no negative balances, no unmatched ledger entries, no building with two owners. This turns the roadmap's headline promise (fair mechanics without exploits) into a gate.
  - Status (~90%): seeded `EconomyInvariantTests` gate negative balances, unmatched ledger rows, building/lot ownership and per-company ledger-vs-balance conservation over transfers, supply contracts (incl. the cancellation-fee path), forex swaps, building sales, direct bank loans through repayment and ticks (seeded NPC companies run every tick and are covered by the per-company check); three defects found and fixed by it (#606, #621, loan penalty ledger). Remaining: stock trades in the generator; global minted/burned accounting incl. player and government accounts.

## 3. Proposed: new gameplay and product features

Ordered by expected player impact against effort. Each builds on systems that already exist (stock exchange, banks, B2B contracts, media houses, R&D, trade routes, global events, NPCs).

### Retention and onboarding

- [ ] (~25%) **Guided "first 7 days" campaign.** Build on the tutorial: daily goals with in-game rewards and master-ranking points, a proactive advisor card ("your shop has been out of stock for 12 ticks; add a purchase unit"), and an end-of-session summary ("while you were away: +$18k, 2 loans paid, shop out of stock"). Largest lever on early drop-off. **Status:** the server slice for the daily goals is in (`firstWeekCampaign` query, `claimCampaignReward` mutation; seven goals, one per day since onboarding, computed from tutorial milestones and owned buildings; rewards of day x $2,000 paid to the company USD account with a `CAMPAIGN_REWARD` ledger entry; idempotent via a unique (player, goal) index, proven with a 4-way simultaneous-claim test on PostgreSQL). Master-ranking points are not part of the reward (the existing tutorial bounties already award them). Still open: advisor rules engine and card, away summary, and all frontend (campaign card, advisor card, summary modal).
- [ ] **Sandbox / replay mode.** A private shard-in-a-browser (or time-boxed practice shard) where guests can fast-forward ticks, so price elasticity, saturation and quality effects can be learned risk-free before real onboarding.
- [ ] **Seasons and shard rotation.** The endgame already makes a shard read-only when someone beats the top billionaire. Add automatic provisioning of the next shard, a cross-shard Hall of Fame on the master site, and small cosmetic carry-overs (badges, titles) so a win starts the next round rather than ending the game.

### Economic depth (Capitalism II feel)

- [ ] **Labor market.** Per-city labor pool and unemployment affect salaries, public-sales demand and unit staffing; salary policy (already set per city) then has real trade-offs, and "Good employer" bounty becomes meaningful. Include optional strikes when pay trails the city average.
  - Status (~95%, #603): calculator (Shared), `CityLaborMarket` + staffing/strike columns with migrations, end-of-tick `LaborMarketPhase`, wage pressure on labor costs, staffing factor and deterministic strikes in the production/cost phases, read-only `cityLaborMarkets(companyId)` and the city Economy tab section. "Good employer" now requires a real employer (building with units, not on strike) paying at least 1.05x the city reference, so a salary setting in a city without employees no longer earns it. Public-sales demand already follows labor: city demand uses the blended recent labor spending (`RecentSalaryByCity`), which the wage index and staffing factor now move, so a separate unemployment factor would double count. Remaining: validate the migrations against a real PostgreSQL (none available on the dev machine).
- [ ] **Patents and licensing.** Let a company that leads a product's research license its quality to others for a per-unit royalty, creating a market for R&D and a way to monetize a won research race.
- [ ] **Corporate bonds and futures.** Companies issue bonds on the stock exchange (alternative to bank loans); commodity forwards/futures on exchange resources let manufacturers hedge price swings that Global Events and economic cycles already create.
- [ ] **Insurance.** Optional policies against global events, power outages and forced-sale risk, priced from city/event history and sold by player-owned insurers (a new building type reusing the bank model). Gives mid-game players a sink and a new profession.
- [ ] **Regulation and fair competition.** Antitrust signals when one company exceeds a market-share threshold in a city/product (price-cap warnings, fines), and a city "council" where large local shareholders vote on tax rate within bounds. Adds a counterweight to snowballing and uses the city economic-health data.
- [ ] **Franchise / brand licensing.** Allow brand owners to license a brand to other companies' shops for a fee, using the existing branding and marketing-efficiency mechanics.

### Social

- [ ] **Alliances (guilds).** Shared chat channel, optional shared B2B pricing group (the `GROUP` sale visibility exists), alliance leaderboard, and a master-ranking bounty for alliance activity.
- [ ] **Friend list, direct messages, and player-to-player trade offers** outside the public exchange, with bank-to-bank settlement and ledger visibility as with every other money movement.

### Platform and ecosystem

- [ ] **Bot developer kit.** API keys, scopes, audit trail and a reference `NPCBot` exist. Publish a bot guide (rate limits, key scopes, example GraphQL operations, a minimal sample bot in C# and TypeScript), plus a "bots" leaderboard filter so human and bot rankings are comparable.
- [ ] **Public read-only stats API / embeddable widgets** for rankings, endgame race and shard status, so the community can build Discord widgets and dashboards; reuse the existing Discord bot for `/cap5-rank` and `/cap5-shard` commands.
- [ ] **Accessibility and localization breadth.** Keyboard and screen-reader pass over the grid editor and map (core loop, currently pointer-first), colour-blind-safe status palettes, and one or two more locales after Flutter localization lands (candidates: Czech, Polish, matching the launch cities).
- [ ] **Observability for operators.** Dashboards and alerts for tick duration, queue depth, failed phases, per-shard error rate and backup freshness (the daily backup workflow exists; nothing verifies a restore). Add a monthly automated restore drill into a scratch namespace using the existing `scripts/recovery` tooling.

## Suggested order

1. Section 1 housekeeping that unblocks everything else: 500-line CI guard, esbuild closure, security-audit cadence, Flutter chat.
2. Section 2 trust items (invariant suite, tick benchmark, product-definition cleanup) before adding new money-moving features, because every feature in section 3 adds new ways to create or destroy value.
3. Section 3 in this order: first-7-days campaign, labor market, seasons/shard rotation, patents, alliances, then bonds/insurance/regulation.
