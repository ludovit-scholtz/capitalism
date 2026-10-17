# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

Everything previously tracked here (onboarding simplification, copy/paste on mobile, news pagination, Azure e-mail + weekly report, account deletion, the 2026 security hardening batch, and every screen of the Flutter app through Operations) is delivered and was spot-checked against the code on 2026-10-15. History lives in `CHANGELOG.csv` and git; per-screen trims are documented in the top-of-file comment of each Flutter file. This file now lists only open and proposed work. Product requirements live in `PRODUCT-DEFINITION.md`.

## 1. Carry-over: known gaps from delivered work

These were explicitly trimmed or left open in items that were closed. They are the only real leftovers.

### Flutter app

- [ ] **Real in-game Chat.** The Chat panel (`lib/features/chat/chat_panel.dart`) and the dashboard Chat tab are still placeholders. The web has chat and the product definition requires it, so a phone player currently cannot chat or see the Discord bridge. Wire the existing chat GraphQL operations (history, send, subscription or polling), admin-invisible mode, and unread badge.
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

- [ ] **Burn down the 500-line rule violations.** The hard rule is not met today. Worst offenders: `composables/useBuildingDetail.ts` (5,639), `views/OnboardingView.vue` (2,147), `CityLotDetailPanel.vue` (1,381), `StockMarketListingRow.vue` (1,339), `AppHeader.vue` (1,181), `BuildingReadonlySidebar.vue` (1,171), `BuyBuildingSteps.vue` (1,016), `LedgerMainContent.vue` (1,002); backend: `Mutation.BuildingMarket.cs` (891), `MasterApi/Types/Query.cs` (842), `MasterApi/Program.cs` (813), `ApiKeyScopeMiddleware.cs` (785), `Api/Program.cs` (739). Add a CI check that fails on any new file over 500 lines (with an allow-list that only shrinks) so the debt cannot grow.
- [ ] **Make the CI audit jobs required checks.** The branch-protection API returns 404 for `main` (no protection rules visible), so `frontend-dependency-audit.yml`, the audit-board gate and the API/frontend builds are not enforced; a repository admin has to enable required status checks.
- [ ] (80%) **Security audit cadence.** The weekly reminder workflow runs (issues #569-#588 were opened and closed on schedule); the missing piece was the reports themselves. `audits/security-audit-2026-W42.md` closes the gap since July, `audits/README.md` documents cadence and naming, and the reminder text now points at the real file name. Remaining: nothing automatically authors the weekly report, so a recurring auditor run is still needed.

## 2. Proposed: make the product match its own definition

Places where `PRODUCT-DEFINITION.md` is stale, contradictory, or promises something with no automated proof.

- [ ] **Clean up the product definition.** Known contradictions: Pro is "$10/month" under Monetization but "$20 equivalent, 0.137 g gold" under Gold token; Startup pack is "$20" vs "$40 / 0.274 g"; the tutorial lists five milestones but the code has seven (plus `FIRST_MILLION`); seasonal quarters say Q4 = "Oct–Nov"; the "Unit price" section ends mid-sentence ("Labor costs are paid"); "Banking deposit rate management (planned)" is already built. The source of truth for prices is `MasterApi/Types/Mutation.cs`; make the document follow it.
- [ ] **Automate the tick performance requirement** (1,000 concurrent users, 20,000 buildings, 500,000 units in under 1 s). Add a seeded tick benchmark (BenchmarkDotNet or a timed integration test) with a CI budget and a per-phase timing breakdown in the operations dashboard, so regressions in the 40+ tick phases are visible before players feel them.
  - Status (~60%): per-phase tick timings are recorded and shown in Operations Overview (admin-only `tickPhaseTimings`), a deterministic world generator and a CI-budgeted benchmark (1/50 slice) exist, and NpcCompany queries are batched. Remaining (#608): full-scale nightly run on PostgreSQL, optimising the slowest phases toward the 1 s target.
- [ ] **Multi-account abuse detection** is specified for the admin dashboard. Verify what `Query.Admin.cs` actually flags, and add concrete signals: shared IP/device clusters, one-way money funnels between accounts (stock trades, B2B sales, loans at off-market rates), repeated referral loops. Output: a ranked suspicion list with evidence links and an audit-logged "reviewed" state.
- [ ] **Property/regression test for "no exploit path".** Add an invariant suite run after N simulated ticks with random player/NPC actions: total money across bank accounts equals minted minus burned, no negative balances, no unmatched ledger entries, no building with two owners. This turns the roadmap's headline promise (fair mechanics without exploits) into a gate.
  - Status (~70%): seeded `EconomyInvariantTests` gate negative balances, unmatched ledger rows, building/lot ownership and per-company ledger-vs-balance conservation over transfers, supply contracts and ticks; two defects found and fixed. Remaining: loans, stock trades and building purchases in the generator; global minted/burned accounting incl. player and government accounts.

## 3. Proposed: new gameplay and product features

Ordered by expected player impact against effort. Each builds on systems that already exist (stock exchange, banks, B2B contracts, media houses, R&D, trade routes, global events, NPCs).

### Retention and onboarding

- [ ] **Guided "first 7 days" campaign.** Build on the tutorial: daily goals with in-game rewards and master-ranking points, a proactive advisor card ("your shop has been out of stock for 12 ticks; add a purchase unit"), and an end-of-session summary ("while you were away: +$18k, 2 loans paid, shop out of stock"). Largest lever on early drop-off.
- [ ] **Sandbox / replay mode.** A private shard-in-a-browser (or time-boxed practice shard) where guests can fast-forward ticks, so price elasticity, saturation and quality effects can be learned risk-free before real onboarding.
- [ ] **Seasons and shard rotation.** The endgame already makes a shard read-only when someone beats the top billionaire. Add automatic provisioning of the next shard, a cross-shard Hall of Fame on the master site, and small cosmetic carry-overs (badges, titles) so a win starts the next round rather than ending the game.

### Economic depth (Capitalism II feel)

- [ ] **Labor market.** Per-city labor pool and unemployment affect salaries, public-sales demand and unit staffing; salary policy (already set per city) then has real trade-offs, and "Good employer" bounty becomes meaningful. Include optional strikes when pay trails the city average.
  - Status (~70%, slices A+B of #603): `LaborMarketCalculator` (Shared), `CityLaborMarket` + migration, end-of-tick `LaborMarketPhase`, wage pressure (0.90x..1.50x by labor-pool utilization) on labor costs, per-building staffing factor (city shortage x pay vs city average, weighted by market tightness) applied as an efficiency factor to production and cost phases, deterministic strikes (probability from pay ratio, FNV roll of tick+building, 6-11 ticks, end early when pay is fixed; no production and no wages while striking), read-only `cityLaborMarkets(companyId)`. Remaining: city-panel UI and strike warnings (#617), public-sales demand effect, "Good employer" bounty on the real pay ratio, real PostgreSQL upgrade-path validation.
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
