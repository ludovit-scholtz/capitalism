# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Master Website & Multiple Game Server Hub

- [x] (100%) Master Website & Multiple Game Server Hub is live: the master portal at `projects/master-frontend` markets the game with a hero section and feature highlights (Economic Simulation, Stock Exchange, Power Grid, R&D), lists active game servers with player counts and tick numbers, shows authenticated players their Pro subscription status with tier badge and expiry date, provides a Pro Renew/Upgrade flow, displays gold token balance with transaction history, and includes a full three-topic Documentation section (Getting Started, Buildings Guide, Economy Overview). All strings use i18n keys in English, Slovak, and German. E2E tests cover the docs view topic switching, feature highlights rendering, subscription panel (Free tier upgrade prompt, Active Pro badge/expiry), and nav link presence.

### Game Server Registry & Heartbeat System

- [x] (100%) Game Server Registry & Heartbeat System is live: game servers self-register via the `registerGameServer` GraphQL mutation (with registration-key auth), which upserts a `GameServerNode` row and resets the liveness timer. A `GameServerEvictionHostedService` background service runs every configurable interval and marks servers inactive when their `lastHeartbeatAtUtc` falls outside the `ActiveThresholdSeconds` window. The master-frontend `/game-servers` page shows all registered servers with online/offline status badges, player counts, tick numbers, and "Play on server" links; the page auto-refreshes every 30 seconds via `setInterval`/`clearInterval` with cleanup on unmount. The home page (`/`) shows an "Active Servers" teaser section with the top 3 online servers and a "View all servers →" CTA link. All strings use i18n keys in English, Slovak, and German. Backend integration tests cover: empty list, valid registration, invalid key, heartbeat stats update, stale-server offline detection, and eviction-service marking inactive. E2E tests cover: home teaser empty state, top-3 online servers displayed, teaser play link, "View all" navigation, auto-refresh interval lifecycle, game-servers empty state, server card rendering, offline badge, connection error, and refresh button.

### City view

- [x] (100%) Redisign the city view to tab page layout. Make sure the tabs are in the routing /city/:cityId/:tab
- [x] (100%) Move the `Economy cycle` from /dashboard to tab in a city. Make sure every city has different economy cycle.

### Map selection at /buy-building/

- [x] (100%) In /buy-building/ make sure the map is visible when player chooses the land to buy. He should optimize the distance to his own other buildings. Make it similar as it is in the onboarding now.

### Building Unit Grid Configuration System (Core Production Chain)

- [x] (100%) Move Energy Settings to building editation tab
- [x] (100%) When building is in editation mode, make sure the tabs are properly used in routing
- [x] (100%) Unit configuration - the performance tab is not styled properly. Make sure to use the best practices, use tailwind and redisign the whole tab.
- [x] (100%) Unit configuration - the maintanance tab is not styled properly. Make sure to use the best practices, use tailwind and redisign the whole tab.

### Products definition

- [x] (100%) Make sure every product and resource has different picture. Added distinct high-quality photographic WebP images and frontend slug mapping for every seeded resource and product.

### Onboarding

- [x] (100%) In onboarding and in the personal account name configuration in game frontend and also in the master frontend add an icons to select a gender - male or female (using icons). When user clicks on the icon generate either female or generate male make sure to generate friendly personal account name in specified gender.

### Stock exchange

- [x] (100%) For trading specific company create a full page layout. Make sure the routing is correctly setup - /stock/trade/:companyId. 
- [x] (100%) Move the `Limit-order book` component to the full page layout for trading one company and remove the stock selection in the limit order book (should be selected by the companyId in the routing).

### Economy integrity hardening

- [ ] Enforce the minimum sale floor on accepted building offers, not only on the public asking price.
- [ ] Make defaulted-collateral sales lender-safe: do not allow a cheap friendly repurchase to clear the lien while leaving residual principal unsecured.
- [ ] Make every loan origination, scheduled repayment, and manual debt repayment path currency-scoped to the loan currency.
- [ ] Block closing the scheduled repayment account for any unpaid loan state, including `Defaulted`, and remove cross-currency fallback repayment behavior.
- [ ] Freeze pledged-building layout edits, unit upgrades, and other value-changing configuration actions unless the collateral is re-appraised and revalidated.
- [ ] Reserve or escrow pending building-offer funds so one company cannot spam multiple offers with the same money.
- [ ] Count defaulted unpaid principal in lending-capacity calculations until the loss is actually absorbed.
- [ ] Add backend regression coverage for below-floor accepted offers, defaulted-collateral under-recovery sales, mixed-currency lender funding, mixed-currency lender repayment credit, repayment-account closure on defaulted loans, and pledged-building edit denial.

### Buildings & Land Map System (Core Gameplay Loop)

- [x] (100%) Buildings & Land Map System is fully live: players can browse available land parcels on a real-world GPS map (Leaflet.js, OpenStreetMap tiles), purchase lots, construct all 10 building types (Mine, Factory, SalesShop, ResearchDevelopment, Apartment, Commercial, MediaHouse, Bank, Exchange, PowerPlant), and manage their property portfolio via the "My Properties" sidebar.
  - Real-world GPS coordinates per city with population-index-weighted lot pricing.
  - Auto-generation guard (`EnsureMinimumAvailableLotsAsync`) ensures ≥10 available lots per resource type per city at all times — triggered on each `cityLots` query.
  - Mine lots are constrained to resource-deposit plots; a purchased mine lot auto-creates a replacement mine lot so the minimum availability guarantee holds.
  - `purchaseLot` mutation deducts from the company's local-currency bank account, supports all three seeded cities (Bratislava EUR, Prague CZK, Vienna EUR), and returns structured errors: `INSUFFICIENT_FUNDS`, `LAND_NOT_AVAILABLE`, `INVALID_BUILDING_TYPE_FOR_LAND`, `MINE_REQUIRES_RESOURCE_DEPOSIT`, `BUILDING_ALREADY_ON_LOT`.
  - `setBuildingForSale` / `makeOfferOnBuilding` / `acceptBuildingOffer` mutations enable a player-to-player building secondary market with optimistic-concurrency conflict detection.
  - Building demolition refunds and sell-building minimum valuations now use exact recorded build value (`recorded lot purchase amount + current shell construction cost + active-unit replacement cost including upgrade steps`), closing destroy-and-refund arbitrage.
  - Game-engine tick consumes `rawMaterialQuantity` on each mine extraction tick with a diminishing-return factor; `getMineExtractionHistory` and `getMineDepletionForecast` queries give players forward visibility on remaining deposits.
  - `/city/:id` city-map frontend route with colour-coded lot markers (green = available, blue = yours, grey = other owner), lot detail panel, purchase confirmation, and strategic recommendation labels driven by `populationIndex` and resource data.

### Onboarding

- [x] (100%) Personal account name is generated in the onboarding process before user signs in. The game server now resolves public player labels from the stored player profile across rankings, chat, account ownership labels, and player GraphQL surfaces instead of exposing JWT auth names.

### Bot API Authorization

- [x] (100%) Bot API Authorization is live: players can generate/revoke personal API keys, list active keys in dashboard settings with shown-once key warnings, authenticate scripts via `Authorization: ApiKey <key>`, and admins can review/revoke keys through API-key audit and admin GraphQL surfaces while ownership and scope enforcement remain active.

### Economy & Markets

- [x] (100%) In-Game Notifications & Activity Feed is live: authenticated players now have a navbar bell badge backed by `notificationCount`, a slide-out grouped activity panel with severity colouring (INFO/WARNING/CRITICAL), deep-link navigation to related entities, mark-read/mark-all-read actions, and backend-generated alerts for production halts, loan due/default events, market price spikes, oversupply warnings, takeover signals, building offers, and mine depletion.
- [x] (100%) FX Exchange with Gold Token AMM and liquidity pools is live: players can quote/execute currency swaps, create/add/remove Gold AMM liquidity positions, receive proportional 1% swap-fee accrual, and use the Forex UI route for trading and pool management.
- [x] (100%) Stock Exchange is live: players can browse listed companies on `/stocks` and `/stock-exchange`, trade shares from personal or company USD settlement accounts, review shareholder pie charts and portfolio holdings, execute hostile takeovers with the explicit `replaceCEO` flow at 50% combined ownership, merge companies at 90% combined ownership, and track Race to the Top progress from the personal ledger.
- [x] (100%) Bank Buildings & Loan System is live: players can activate Bank buildings with the required base capital deposit, publish deposit/lending rates, browse banks on `/banking` and `/loans`, open deposit accounts, request collateralised loans, track active loans and deposits, and manage owner-side liquidity/central-bank exposure from the bank dashboard.
- [x] Secondary Market and Loan Currency Integrity Hardening (100%): closed the 2026-05-14 business-logic audit gaps by enforcing the building-sale floor at accepted-offer settlement, preventing lender-stripping defaulted-collateral sales, scoping all loan debits and credits to the loan currency, preventing repayment-account closure for unpaid loans, blocking pledged-building edits without collateral revalidation, reserving pending offer funds, and keeping defaulted unpaid principal in lending-capacity accounting.
- [x] (100%) Power Plant System & Energy Economics is complete: every building tracks configurable power priority (1–10), city load shedding keeps high-priority buildings online first, the tick engine applies `POWERED`/`CONSTRAINED`/`OFFLINE` operational states, and the full frontend experience is live — building detail headers display colour-coded energy status badges (`Powered` / `Reduced Output` / `Offline — No Power`), the Energy Settings panel lets players set power priority and max spot-market bid price for any building, power plant buildings have a dedicated P&L analytics panel with fuel reserve bar, dispatch gauge, revenue metrics, and spot-market listing management (create/cancel energy listings), the city map shows `CityPowerPlanningSection` with live generation capacity, load balance, and weather forecast cards, and the `/energy` Energy Market route lists all active energy listings for the city. All i18n keys present in English, Slovak, and German.
- [x] (100%) Media House System is live: media houses now accumulate and decay content each tick from configurable spending budgets, city/category content ranking drives marketing effectiveness, marketing units can pick media houses with live quality scores, and the GraphQL surface includes `cityMediaHouses`, `mediaHouseDetail`, and `setMediaHouseSpendingLevel` for ranking, detail, and budget control workflows.
- [x] (100%) Apartment & Commercial Building Rental Income System is live: players can set rent prices (per m²) on Apartment and Commercial buildings, occupancy is calculated each tick against the city average rent, company bank accounts are credited with `occupancy × sizeM2 × rentPerSqm`, a 24-tick pending-change delay is enforced with a visible countdown, `RentalIncomeRecord` entries drive the revenue sparkline in the building detail panel, `RENTAL_INCOME` appears in the Ledger Income Statement with per-building drilldown, and `cityRentalMarket` / `apartmentBuildingDetail` GraphQL queries expose occupancy and revenue history. 19 backend integration tests and 6 Playwright E2E tests cover happy-path, price elasticity, pending-change delay, non-owner rejection, and ledger integration.
- [x] (100%) Company Settings, salary-level governance, and shareholder dividend voting are live: owners can manage per-city salary settings and overhead visibility on `/company/:id/settings`, CEOs can propose dividend-policy changes, shareholders vote with weighted ownership, and approved policies update annual dividend distributions.
- [x] (100%) Accounting Ledger: Income Statement, Cash Flow Statement, and Balance Sheet with interactive drilldown navigation are live: players can view `/ledger/:companyId` (also accessible at `/company/:id/ledger`), inspect Income Statement revenue/expenses with drilldown to individual entries, review Balance Sheet assets and liabilities, navigate historical game years, view Income Tax Schedule with next-payment tick, see Buildings Performance by building, and track logistics shipments and city financial breakdown. A Race to the Top panel links to the Personal Ledger for endgame progress. All `ledger.*` i18n keys added in English, Slovak, and German. 12 full-journey E2E tests cover income statement, balance sheet, cash flow, drilldown, history, Race to the Top panel, route alias, and error states.
- [x] (100%) Transit Costs & Logistics are active across sourcing flows: purchase sourcing and B2B/exchange logistics include non-zero transit pricing, company ledgers track `SHIPPING_COST`, admin operations expose aggregate shipping totals with company breakdowns, and authenticated `shippingCostQuote` now provides point-to-point route distance, weight, and local-currency cost estimates.
- [x] (100%) NPC AI-Controlled Company Competitors are live: `NpcCompanies`/`NpcDecisionLogs` persistence, tick-driven `NpcCompanyPhase` archetype behavior (expansion, pricing, shop participation), admin pause/resume + decision-log GraphQL operations, seeded NPC competitors per city, city map orange NPC ownership markers, `/city/:cityId/competitors` intelligence panel, and Market Dashboard top-competitor badges with multilingual UI coverage and focused backend/E2E regression tests.

### Consumer Demand & Price Elasticity Engine (Core Economic Simulation)

- [x] (100%) Consumer Demand & Price Elasticity Engine is active: NPC consumers purchase from public sales units each tick based on dynamic price elasticity; company bank accounts are credited; `marketPrice`, `marketPriceHistory`, `cityDemandSummary`, and `marketOverview` GraphQL queries expose live clearing prices and satisfaction rates.
  - `PublicSalesPhase` computes price-elastic demand per unit each tick, deducts sold quantity from storage inventory, and credits the company bank account with sales revenue.
  - `marketPrice(cityId, productTypeId, lastNTicks)` query returns weighted-average clearing price, total volume, seller count, and currency code from recent `PublicSalesRecord` rows.
  - `marketPriceHistory(cityId, productTypeId, lastNTicks)` query returns per-tick aggregated clearing price and volume data ordered ascending for charting.
  - `cityDemandSummary(cityId, topN, lastNTicks)` query returns top-N demanded products with satisfaction rate, average clearing price, total demand vs total sold, and seller count.
  - `marketOverview(topN, lastNTicks)` query returns market summary across all seeded cities in one request; used by the Market Dashboard for the Bloomberg-terminal overview.
  - Backend integration tests: `marketPrice` weighted-average (single seller, multi-seller, no-sales null), `marketPriceHistory` per-tick ordering and empty, `cityDemandSummary` top-product sorting and unknown-city null, `marketOverview` all-cities coverage, `publicSalesAnalytics` clearing price field, Vienna city demand — 14 tests total.
  - Market Dashboard frontend route `/market` with city tabs, product-grid table showing avg. clearing price, demand, sold quantity, satisfaction rate badges and fill bars, and seller count — colour-coded Green/Amber/Red.
  - City Demand Panel (`CityDemandPanel.vue`) embedded in `CityMapView` showing top 5 demanded products with satisfaction rates for the active city.
  - Price Recommendation Widget (`price-recommendation-badge`) in Quick Actions tab of unit detail panel: green (≤ market), amber (10–30% above), red (>30% above), no-data state when market clearing price is unavailable.
  - Navigation link added to `AppHeader` with `chart-pie` icon.
  - All i18n keys added in English, Slovak, and German (`marketDashboard.*`).
  - E2E tests: Market Dashboard (12 tests) covering city tabs, product grid, sorting, satisfaction badges, Vienna city tab, error state, mobile viewport; Price Recommendation Badge (4 tests) covering all colour states and no-data state.

### R&D / Product Quality System

- [x] (100%) R&D Phase Tick Engine & Product Quality Economics is live: the `ResearchPhase` tick processor runs each tick, accumulating research budgets per company per product type and computing quality levels (0–1 scale, equivalent to 0–10 display). Quality levels drive a price premium of up to 50% in `PublicSalesPhase`. The Company Research Dashboard at `/company/:id/research` displays all product research states, quality level badges, and price premium breakdown.
  - `ResearchPhase` processes PRODUCT_QUALITY and BRAND_QUALITY units each tick: each active unit converts a fraction of its operating costs into an `AccumulatedResearchBudget` (stored in USD for cross-city fairness). Budgets decay 0.1%/tick when investment stops. Quality is computed as `min(1, budget / baseTarget)` relative to the top competitor globally.
  - `PublicSalesPhase` applies quality multiplier: `effectivePrice = basePrice × (1 + QualityPricePremiumRate × combinedQuality)`. At quality level 5 (combinedQuality = 0.5), sellers earn a 25% price premium with equivalent consumer demand. At quality level 10 (combinedQuality = 1.0), the premium reaches 50%.
  - `productQualityProfile(companyId, productTypeId)` GraphQL query returns detailed quality state (rdQuality, marketingQuality, combinedQuality, qualityLevel 0–10, accumulated/base/competitor budgets in USD, price premium %, estimated ticks to next level).
  - `brandQualityOverview(companyId)` GraphQL query returns all research states for a company across all products and categories, with total R&D budget in USD.
  - `buildingResearchProgress(buildingId)` GraphQL query exposes per-unit research progress: current quality level, combined quality, fractional progress to next level, and price premium.
  - Company Research Dashboard (`/company/:id/research`): product quality table with quality level badges (Lv 0–10), price premium column, R&D/marketing quality progress bars, and total R&D investment summary. Empty state guides players to build an R&D facility. Navigation link added to company action row on the Dashboard.
  - R&D Building Detail panel: quality level badge `Lv N` overlaid on research brand entries, price premium indicator row in budget panel (`+X%`), quality level colour tiers (gold ≥ 8, green ≥ 5, blue ≥ 2, dim = 0).
  - All i18n keys added in English, Slovak, and German (`research.dashboard.*`, `research.qualityLevelBadgeTitle`, `research.budget.qualityPricePremium`).
  - 11 backend integration tests: `productQualityProfile` own company, foreign company (null), missing company (null), with brand (quality level populated), `brandQualityOverview` own company, foreign/missing company (empty), `buildingResearchProgress` own building, foreign building (empty), non-RD building (empty), full tick cycle quality increment, PublicSalesPhase quality premium application.
  - `competitorQualityIntelligence(cityId, productTypeId)` GraphQL query: returns ranked list of competitors (and own company) by combined quality level in a given city/product. Response includes companyId, companyName, qualityLevel (0–10), pricePremiumPct, isOwnCompany flag. Sorted by quality descending. 4 integration tests cover: unauthenticated access denied, empty result when no competitors, isOwnCompany flag correctness, sorted-by-quality ordering.
  - `QualityDecayPhase` (Order=750): perishable products in STORAGE units lose `0.0005` quality per tick. When quality reaches zero, the entire inventory slot is removed and an `InventorySpoilageRecord` plus a `SPOILAGE_LOSS` ledger entry are created. `IsPerishable` field added to `ProductType`; FoodProcessing and Healthcare products seeded as perishable. EF migration `AddIsPerishableAndSpoilageRecord` added. 4 integration tests cover: perishable decay, non-perishable unchanged, zero-quality removal, spoilage record creation.
  - Competitor intelligence panel in `MarketDashboardView.vue`: clicking a product row loads competitor rankings from the backend, displaying medal emojis (🥇🥈🥉), quality badges (gold/green/blue/dim), company name, "You" badge for own company, and price premium percentage.
  - `SPOILAGE_LOSS` ledger category key added to en/sk/de i18n; `research.competitors.*` keys added for competitor intelligence panel labels.
### Security Improvements

- [x] (100%) Harden GraphQL pre-execution middlewares to inspect every selected root field in named operations and every JSON-array batched item, covering auth rate limits, introspection, depth, and complexity.
- [x] (100%) Trust `X-Forwarded-For` only from configured reverse proxies, fall back to `RemoteIpAddress` otherwise, and test that spoofed headers cannot rotate rate-limit identities.
- [x] (100%) Normalize duplicate registration errors so message, extension code, and timing do not reveal whether an email already exists in either API.
- [x] (100%) Move the game API seed admin password out of committed defaults (`__SET_IN_ENV__` + `.env.example`/README guidance), and block non-Development startup when `Auth:PasswordAuthEnabled=true` and `SeedData:AdminPassword` is missing/placeholder while keeping Development warning-only.
- [x] (100%) Move game-frontend browser sessions to HttpOnly SameSite cookie auth (`credentials: include`) and stop persisting JWT session tokens in `localStorage`/`sessionStorage` for normal gameplay requests.
- [x] (100%) Finish canonical object-authorization error normalization in legacy economy CRUD paths and keep the GraphQL surface inventory gate aligned with the normalized contract.
