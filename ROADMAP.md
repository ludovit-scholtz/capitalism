# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Building Unit Grid Configuration System (Core Production Chain)

- [x] (100%) Building Unit Grid Configuration System is fully live: players configure interactive 4×4 production-unit grids inside Mine, Factory, Sales Shop, and R&D buildings. The system is the core gameplay mechanic that turns map buildings into a living economic simulation.
  - `storeBuildingConfiguration` mutation validates grid constraints (max 1 unit per cell, valid unit type per building type, link validity), deducts upgrade costs from company bank account, and applies layout atomically.
  - `scheduleUnitUpgrade` mutation queues a unit-level upgrade with tick countdown and 90% cost refund on cancellation.
  - `cancelPendingConfiguration` mutation discards a staged plan and unlocks the building for new changes.
  - Mine buildings allow MINING, STORAGE, B2B_SALES; Factory adds PURCHASE, MANUFACTURING, BRANDING; Sales Shop allows PURCHASE, MARKETING, STORAGE, PUBLIC_SALES; R&D allows PRODUCT_QUALITY, BRAND_QUALITY.
  - Tick engine phases execute unit processing: MiningPhase fills mining-unit inventory from deposit lots each tick respecting diminishing-return efficiency; ManufacturingPhase converts linked inputs to outputs; PublicSalesPhase and B2BSalesPhase settle sales and credit company bank accounts.
  - `buildingDetail` query returns full unit grid with resource history sparklines (last 100 ticks per unit).
  - Frontend `BuildingDetailView` renders the 4×4 interactive grid with unit-type icons, fill bars, level badges, link arrows, edit/staging mode, and desktop side-by-side layout.
  - Integration tests: happy-path Mine/Factory/SalesShop configuration, invalid unit type rejection, unauthenticated access rejection, cancel pending plan, tick-engine mining inventory production, upgrade lifecycle.

### Buildings & Land Map System (Core Gameplay Loop)

- [x] (100%) Buildings & Land Map System is fully live: players can browse available land parcels on a real-world GPS map (Leaflet.js, OpenStreetMap tiles), purchase lots, construct all 10 building types (Mine, Factory, SalesShop, ResearchDevelopment, Apartment, Commercial, MediaHouse, Bank, Exchange, PowerPlant), and manage their property portfolio via the "My Properties" sidebar.
  - Real-world GPS coordinates per city with population-index-weighted lot pricing.
  - Auto-generation guard (`EnsureMinimumAvailableLotsAsync`) ensures ≥10 available lots per resource type per city at all times — triggered on each `cityLots` query.
  - Mine lots are constrained to resource-deposit plots; a purchased mine lot auto-creates a replacement mine lot so the minimum availability guarantee holds.
  - `purchaseLot` mutation deducts from the company's local-currency bank account, supports all three seeded cities (Bratislava EUR, Prague CZK, Vienna EUR), and returns structured errors: `INSUFFICIENT_FUNDS`, `LAND_NOT_AVAILABLE`, `INVALID_BUILDING_TYPE_FOR_LAND`, `MINE_REQUIRES_RESOURCE_DEPOSIT`, `BUILDING_ALREADY_ON_LOT`.
  - `setBuildingForSale` / `makeOfferOnBuilding` / `acceptBuildingOffer` mutations enable a player-to-player building secondary market with optimistic-concurrency conflict detection.
  - Game-engine tick consumes `rawMaterialQuantity` on each mine extraction tick with a diminishing-return factor; `getMineExtractionHistory` and `getMineDepletionForecast` queries give players forward visibility on remaining deposits.
  - `/city/:id` city-map frontend route with colour-coded lot markers (green = available, blue = yours, grey = other owner), lot detail panel, purchase confirmation, and strategic recommendation labels driven by `populationIndex` and resource data.

### Onboarding

- [x] (100%) Personal account name is generated in the onboarding process before user signs in. The game server now resolves public player labels from the stored player profile across rankings, chat, account ownership labels, and player GraphQL surfaces instead of exposing JWT auth names.

### Economy & Markets

- [x] (100%) FX Exchange with Gold Token AMM and liquidity pools is live: players can quote/execute currency swaps, create/add/remove Gold AMM liquidity positions, receive proportional 1% swap-fee accrual, and use the Forex UI route for trading and pool management.
- [x] (100%) Stock Exchange is live: players can browse listed companies on `/stocks` and `/stock-exchange`, trade shares from personal or company USD settlement accounts, review shareholder pie charts and portfolio holdings, execute hostile takeovers with the explicit `replaceCEO` flow at 50% combined ownership, merge companies at 90% combined ownership, and track Race to the Top progress from the personal ledger.
- [x] (100%) Company Settings, salary-level governance, and shareholder dividend voting are live: owners can manage per-city salary settings and overhead visibility on `/company/:id/settings`, CEOs can propose dividend-policy changes, shareholders vote with weighted ownership, and approved policies update annual dividend distributions.
- [x] (100%) Accounting Ledger: Income Statement, Cash Flow Statement, and Balance Sheet with interactive drilldown navigation are live: players can view `/ledger/:companyId` (also accessible at `/company/:id/ledger`), inspect Income Statement revenue/expenses with drilldown to individual entries, review Balance Sheet assets and liabilities, navigate historical game years, view Income Tax Schedule with next-payment tick, see Buildings Performance by building, and track logistics shipments and city financial breakdown. A Race to the Top panel links to the Personal Ledger for endgame progress. All `ledger.*` i18n keys added in English, Slovak, and German. 12 full-journey E2E tests cover income statement, balance sheet, cash flow, drilldown, history, Race to the Top panel, route alias, and error states.

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

### Security Follow-Ups

- [x] (100%) Finished `NOT_FOUND_OR_NOT_OWNED` plus balance-redaction normalization across building-market, exchange, and bank-transfer mutations so authenticated probes cannot infer foreign object existence, listing state, company linkage, or exact available funds.
- [x] Add password-auth abuse controls across `projects/Api` and `projects/MasterApi`: account-aware login throttling or temporary lockout, endpoint rate limiting, duplicate-email response normalization, and monitoring for repeated failed attempts. *(100% — LoginThrottleService with 5-failure lockout, AuthRateLimitMiddleware with 10 req/IP/min, neutral duplicate-email message, structured lockout warning logs; disabled in Development/Testing)*
- [x] (100%) Added HotChocolate query-budget enforcement in both `Api` and `MasterApi` (`GraphQL:MaxDepth`, `GraphQL:MaxComplexity`, `GraphQL:MaxPageSize`), wired cost analyzer + weighted `[Cost]` fields, and standardized `MAX_DEPTH_EXCEEDED` / `MAX_COMPLEXITY_EXCEEDED` responses with security warning logs.
- [x] (100%) Rate-limit the `SendChatMessage` mutation per authenticated user (20 messages/60 seconds) and enforce a maximum message length of 500 characters to prevent chat spam and database bloat. *(ChatRateLimitService with sliding-window IMemoryCache counter, structured RATE_LIMITED/MESSAGE_TOO_LONG errors, WARNING-level violation logs; frontend character counter at 450 chars, red highlight, toast on rate-limit)*
- [x] (100%) HotChocolate Nitro IDE and schema introspection are now gated to `IsDevelopment()` only in both APIs, with non-development introspection requests returning `FORBIDDEN`.
- [x] (100%) Added startup guard in both APIs that throws `InvalidOperationException` when `Jwt:SigningKey` is placeholder/insecure (null, whitespace, short, or known placeholder) outside Development and logs a critical startup-block event with `Jwt__SigningKey` override guidance.
- [x] Restrict CORS open fallback (`AllowAnyOrigin()`) to `IsDevelopment()` only; non-Development deployments with an empty `Cors:AllowedOrigins` list should reject all cross-origin requests with a warning log. (100%)
- [x] (100%) Add `Strict-Transport-Security` header to `projects/frontend/nginx.conf` with `max-age=31536000; includeSubDomains`.
- [x] (100%) Remove `unsafe-inline` from `script-src` in `projects/frontend/nginx.conf` CSP header; verify production Vite bundle works without it and implement nonce-based CSP if inline scripts are required.
- [x] Implement a time-limited email-based password reset flow (or document OIDC re-linkage as the only recovery path) to prevent permanent player lock-out on credential loss. (100%)
- [x] (100%) Move `RootAdministratorEmails` and database credentials out of committed `appsettings.json` into environment-variable configuration or a secrets manager.
- [x] (100%) Implement JWT session revocation: maintain a server-side token revocation set (Redis or DB) to support explicit logout and admin-initiated session termination. Currently stateless JWTs remain valid for up to 120 minutes after compromise is detected.
- [x] (100%) Fix SSL certificate validation bypass for master-server HTTP client: the bypass is conditioned on URL containing "masterapi" (container hostname) rather than on `IsDevelopment()`, meaning it activates in Docker Compose production deployments. Replace with an environment-based or explicit development-only bypass.
- [x] (100%) Add security headers to `projects/master-frontend` deployment: the master portal has no nginx.conf and relies on the Vite dev server or static hosting without HSTS, CSP, X-Frame-Options, or X-Content-Type-Options headers.
