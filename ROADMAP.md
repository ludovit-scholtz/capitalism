# Capitalism Roadmap

Create a fun game on style of the capitalism II game. This game is economic simulation where players can experience price elasticity, resource scarcity, resource oversupply, different competition types, marketing, product quality, difficulties with scaling up the companies, and other base economic factors.

It will use real world map. The game will start in single city and later other cities will be added.

## Issues to work on

### Operations Dashboard (100% complete)

- [x] Organize Operations Dashboard to level 2 menu, add proper routing, and create components
- [x] Fix News & changelog publisher style. The form is stretched along the whole list of news items. Create multiple pages for this like it is in the ticket support system.
- [x] Players & intervention tools - Make sure to show the table of the users and on user detail page show the actions
- [x] Create page for the game statistics where in one columns will be items that distributes the money such as the public sales buildings, the rent, IPOs, or other money distribution sections. In the other column will be where people are paying money - taxes, fx fees, labour costs, energy, research, stock exchange fees or others..
- [x] Create admin page with detailed statistic for every product. Do it in table. Make sure the table is exportable sortable and filterable. In the table will be the product insights such as the aggregated costs of materials, energy, labor to build the product, number of products produced, sold, market size, saturation, marketing, and research details.
- [x] Backend: Wire `operationsStatistics` query to real LedgerEntry aggregations for live money flow data
- [x] Backend: Wire `adminProductAnalytics` query to real ProductionRecord/PublicSalesRecord aggregations

### Stock exchange (100% complete)

- [x] Do not show the government in the stock market. Do not allow players to trade government company stocks.

### Onboarding (100% complete)

- [x] Before user first sign in to the game make sure to fill in the referal code. Show link (and allow copy on one click) in the master frontend in the referal section where users can refer user for the game server. When user comes to this link, make sure the referal code is stored in the pinia state, and stored to the user account when he first logs in. Before user logs in, show him that he is using specific referal code and he will get 10% discount for in game purchases.
- [x] Create better Company name generator. Find npm package with the word list, and do a proper name generation with the combination of two words. Make sure the company names sounds great.
- [x] Create name generator for personal account name. Find npm package with the names wordlist and do a combination of the Firstname, Middlename and Last name. Allow players to change the personal account name later. In ranking show the personal account name, not the oidc name please. In the form to change name, tell people not to use the real name.

### Buildings (75% complete)

- [x] Add tab view to building overview. Make the default tab to be shown one with the building P&L and statistics. Add second tab the bank account. Make sure the tabs are properly routed through tab view. Make sure to use the components for each tab please.
- [x] When in the building edit mode, create tab view. For each tab make sure to use the component. 
- [x] When in the building edit mode in the unit mode, create tab view. For each tab make sure to use the component. Extract each section to the separate components and show them in tabs. Make sure the design is professional and for the colors and layouts is used the tailwind.
- [x] When in the edit mode, the cancel and store upgrade should be next to each other. Also use some icons there so that users has better UX.
- [x] When I click the sell building show only sell building form. Make sure the form is properly designed.
- [x] Do not allow to put on sale building which is used as a collateral for a loan.
- [x] Create a workflow to destroy a building. When building is destroyed, return the user 80% of the building property value. Make the property available for purchase again. When bank loan is not paid set it for sale for the property market price minus 10%. When the debt from missed payments is not paid in 3 game days (72 ticks), destroy the building and pay any remaining debt from the sale of property to the bank owner.
- [ ] In game dashboard, the number of units count for specific building touches the word Buildings in the navigation. Add some space between the tag and text please.
- [x] When building is in editation mode, allow copy the configuration of the unit at the building. When user hits ctrl+c or cmd+c on mac, he copies to the clipboard the json config of the unit.
- [x] When building is in editation mode, allow paste of the configuration of the unit at the building. When user hits ctrl+v or cmd+v on mac and he has selected the unit, and if the json schema is valid for the unit configuration, apply the configuration to the unit. This copy and paste mechanism will improve the UX of the unit management. For example when user want to expand the factory or if he wants to expand the R&D building.
- [x] Allow to paste unit configuration to empty unit space

**Shipped (sell building increment):** A professional sell-building workflow is live at `/building/:id/sell`. Players can list any building for sale with a custom asking price, cancel an active listing, and see an estimated market value (EMV) based on building level, installed units, and lot population index. The collateral guard blocks listing buildings that are active or overdue loan collateral while allowing listing for defaulted loans (so players can sell to repay defaulted debt). The EMV calculation is extracted into `src/lib/sellBuilding.ts` with 23 dedicated unit tests. Edge-case E2E tests cover: validation (zero price blocked), 150% EMV warning, collateral blocking with loan count, defaulted loan not blocking, already-listed state showing cancel and update buttons, and destroyed buildings hiding the sell action. Backend: 25 integration tests in `BuildingSecondaryMarketTests.cs` cover the full secondary-market lifecycle (list, unlist, make offer, accept offer with ownership transfer and ledger entries, reject offer, concurrent offers, unauthenticated guard, collateral validation for Active/Overdue/Defaulted/Repaid loan states).

**Shipped (building destruction increment):** Automatic building destruction is now live for loan defaults. When a secured loan is unpaid for 72+ ticks, the building is automatically listed on the secondary market at 90% of appraised value. If not purchased within a further 72-tick grace period, the building is automatically destroyed, the owner receives 80% of the original collateral value as compensation, the land becomes available for repurchase, and a `BuildingDestructionRecord` audit entry is persisted. The tick engine runs `BuildingDestructionPhase` (order 750) and `LoanRepaymentPhase` (which now also creates auto-listings). UI: the building detail page shows a prominent "⚠️ Loan Default" alert banner with destruction countdown when any secured loan is in DEFAULTED status; the dashboard building card shows the "⚠️ Loan Default" badge. 16 backend integration tests cover all foreclosure scenarios. CHANGELOG.csv updated.

### News (100% complete)

- [x] Add button to news and changelog to mark all news as read.
- [x] Changelog.csv news are not imported to the database. Make sure that after every restart every changelog news item is imported. If any error occurs during the import log it and skip the import of that one item. Do it more resilient to errors.

### Banks (50% complete)

- [x] When player creates a bank account at second player bank, make sure the second player (the bank owner) can change the interest rate for deposits. The new rate will be applied in 24 ticks from the time when the bank owner changed the deposit interest rate and is applied to all bank account deposits from all players. At the moment the bank account stick with the interest rate which was set when the account was created. For the loans it works good as every loan is specific contract and can have different interest rate, but for the deposits all bank account interest rate which they receive from the bank must be equal and bank owner must be able to change it. Bank owner cannot change the interest rate for existing loan conract which is correctly implemented now.

### FX Exchange (100% complete)

- [x] On rates page make sure to show the buy price, mid price and sell price for the rate. 
- [x] Make sure to show the rate in the stronger currency. The currency strength is USD,EUR,CNY,GBP,INR,CZK. So when user has selected in the context switcher Prague the CZK currency it will show CZKUSD and CZKEUR numbers. When Vienna and EUR is selected make sure to show rates for EURUSD and CZKEUR. Show the pair also in the rate list as it is common in standard forex.
- [x] Collect history for rates and create a chart when user selects in the rates page the specific exchange pair.

**Delivered (increment 1):** The FX Rate List now shows buy/mid/sell columns with tooltip explanations, uses compact standard pair codes (for example `CZKUSD`, `CZKEUR`, `EURUSD`) following the configured strength hierarchy, and adds a responsive mobile stacked layout so all three prices stay visible on narrow screens.

**Delivered (increment 2):** FX Rate History Chart is now live. The backend `FxRateHistoryPhase` captures buy/mid/sell snapshots every game tick for all EUR-based pairs, persists them in `FxRateHistories` with a rolling 24-month window, and exposes them via the public `fxRateHistory(quoteCurrencyCode, ticksBack)` GraphQL query. The frontend Rates tab shows an interactive SVG line chart with separate buy (green), mid (blue), and sell (red) series, a currency pair selector, time range buttons (24h / 7d / 30d), hover tooltip with exact rates per tick, and a responsive mobile layout. Unit tests cover all chart math helpers; integration tests cover snapshot creation, spread validation, and empty-state handling; E2E tests cover chart rendering, legend, pair selector, time range controls, empty state, and mobile viewport.

### Country flags (100% complete)

- [x] Add npm library country-flag-icons and use it everywhere where the country flag should be
- [x] in /cities the flags are visible
- [x] in footer in the language picker the flags are visible. Sync the footer language picker between master and game frontend.
- [x] In context switcher make sure flags are visible.

### Fix city selection (100% complete)

- [x] When I switch city to city where i dont have any factory, log out and log in later with biatec oidc, i want the context switcher automatically switch to my main city where I have the most factories

### Ranking (100% complete)

- [x] Add link from game ranking to master ranking
- [x] Highlight the active player in the ranking - In both game ranking and master ranking
- [x] On ranking page enter, make sure to show the page where player is actively located. For example if player is ranked as 25th make sure to show 3rd page if there is 10 items per page. Do it in both game ranking and master ranking

### Optimize for mobile (0% complete)

- [ ] Make sure the design is smooth on small mobile devices. Mainly the content should be visible without the page scroll to the right.
- [ ] Make sure the design is smooth on tablet sized screens.
- [ ] Make sure the design is smooth on Full HD devices
- [ ] Make sure the design is smooth on 4K screens

### FX Exchange with AMM Liquidity Pools (100% complete)

**Shipped:** Dynamic FX exchange mechanics with AMM (Automated Market Maker) liquidity pools for gold token (XAU) trading are fully implemented. Players can create fiat/XAU pools seeded with Uniswap v2 LP shares, add or remove liquidity proportionally, and execute constant-product swaps with a 1% fee that accrues to liquidity providers.

The backend enforces that deposited funds cannot be double-spent, guards against excessive slippage via `MinOutputAmount`, and persists every swap as an auditable `GoldAmmTradeRecord`.

The frontend exposes all operations under a dedicated "Gold AMM" tab inside the Forex Exchange view: a pool creation form, add/remove liquidity panels, a swap direction selector with real-time quote preview showing fee and slippage, and a "My Positions" tab showing claimable fiat and gold balances.

- [x] Create `GoldAmmPool` entity: stores currency code, fiat/gold reserves, total LP shares, and timestamps with one-pool-per-currency enforcement.
- [x] Create `GoldAmmPosition` entity: tracks per-player LP shares, fiat and gold originally deposited, with player and pool navigation properties.
- [x] Create `GoldAmmTradeRecord` entity: audit log per swap with direction, amounts, fee, implied price, and game tick.
- [x] Register all three entities in `AppDbContext` with proper indexes (player+currency on positions) and EF migrations.
- [x] Implement `createGoldAmmPool` mutation: validates 3-letter non-XAU currency code, checks available fiat and gold balances, seeds pool with `sqrt(fiat × gold)` LP shares (Uniswap v2), deducts from player balances atomically.
- [x] Implement `addGoldAmmLiquidity` mutation: proportional gold calculation from fiat input, slippage guard via `MaxGoldAmount`, creates new position or updates existing position, updates pool reserves.
- [x] Implement `removeGoldAmmLiquidity` mutation: fractional share removal (0–1), proportional fiat/gold return from pool, position cleanup when shares reach zero, credits balances atomically.
- [x] Implement `executeGoldAmmSwap` mutation: constant-product AMM formula with 1% fee staying in pool, slippage guard via `MinOutputAmount`, inserts `GoldAmmTradeRecord`, updates pool reserves atomically.
- [x] Implement `goldAmmPools` public query returning all pools with optional authenticated player position (claimable fiat/gold, share percent).
- [x] Implement `goldAmmSwapQuote` query returning output amount, fee, slippage percent, and available input balance without executing the swap.
- [x] Implement `myGoldAmmPositions` query returning the authenticated player's positions with claimable amounts.
- [x] Implement `myGoldBalance` query returning total wallet gold, sum of gold originally deposited in pools, and available gold.
- [x] Add `GoldAmmSection.vue` frontend component with inner tab navigation: Swap, My Positions, Add Liquidity, Create Pool.
- [x] Integrate Gold AMM section as the "gold" tab in `ForexExchangeView.vue` with deep-link support (`?tab=gold`).
- [x] Add i18n strings for all Gold AMM UI labels and messages to `en.ts`, `sk.ts`, and `de.ts`.
- [x] Add comprehensive backend integration tests in `GoldAmmTests.cs`: AMM math unit tests, pool creation/duplication/insufficient-funds cases, add/remove liquidity flows, swap quote validation, swap execution with balance checks, blocked-resource enforcement, fee accrual, and full deposit-then-withdrawal round-trip.
- [x] Add Playwright E2E tests in `forex-exchange.spec.ts` covering Gold AMM tab navigation, swap form, positions tab empty/populated states, add liquidity happy path, blocked-funds warning, and pool creation form display.

### Fix onboarding (100% complete)

- [x] Change the onboarding steps. The first step will be city selection. When user selects the city in the onboarding make sure to set it also in the context changer in the navbar.
- [x] In product selection make sure to show the price only for the currently selected city
- [x] Create test which will check that after the onboarding there is 200k USD transfered from the personal account and the current balance is 0. Personal account must not have any money after the onboarding - 0 usd, 0 eur nor any other currency. It must be visible in the ledger that the personal account has deposited his funds to the company IPO from both sides - in the personal ledger as outgoing tx, and in the company ledger the incomming transaction from player and public IPO.
- [x] Each step in the onboarding make sure is centered into the middle
- [x] In bank full view, make sure to allow openning the bank account for personal account. At the moment i can see the bank card, but there is missing the open bank account button.
- [x] Fix pre-IPO deposit to be 200k USD not 200k EUR

- [x] In forex swap allow to swap between the selected accounts only from the context switcher. At the moment it is possible to select person account and swap to the company account. This must not be possible.
- [x] In forex transfer allow to transfer between the selected accounts only from the context switcher. At the moment it is possible to transfer from person account to company account. This must not be possible. It must be possible to transfer funds within the single company only using the transfer form.
- [x] Fix notification icon. Make sure to register it properly in main. Update copilot instructions so that after next adding icon the icon will be visible.


### Stock exchange completion (100% complete)

- [x] Implement takeover mechanics: when a player's combined person-account and controlled-company ownership in another company reaches 50%, show a "Initiate Takeover" action that replaces the target company's CEO with the acquiring player, transferring operational control including building configuration and company settings.
- [x] Implement company merge: when combined ownership reaches 90%, expose a "Merge into Company" action that transfers all assets, bank accounts, buildings, inventory, and loans of the absorbed company to a chosen surviving company, settles the absorbed company's tax in the merge tick, and closes the absorbed company.
- [x] Implement share buyback: when a company purchases its own shares on the stock exchange, reduce the total issued share count by the purchased amount and remove those shares from public float, updating share price accordingly.
- [x] Add E2E and backend integration tests for takeover trigger at exactly 50%, merge at exactly 90%, and buyback share-count reduction so these mechanics are regression-proof.

### More industries and products (90% complete)

**Shipped increment (Electronics Pro-starter):** Electronics industry is now a fully playable Pro-subscriber-exclusive onboarding starter path. Three Silicon-driven products (Basic Electronics, LED Screen, Circuit Board) are seeded with direct silicon manufacturing recipes, exposed in the encyclopedia and resource-detail views, and Pro-gated at both the backend (`Player.IsProSubscriber` check in `startOnboardingCompany` / `finishOnboarding`) and the frontend (industry card with PRO badge, error on non-Pro click). The manufacturing encyclopedia and resource detail views already surface the full Electronics chain via the existing industry filter. All existing test suites pass, and new backend and E2E tests cover Pro-gating, product seeding, and the full Electronics onboarding flow.

**Shipped increment (Construction Pro-starter):** Construction industry is now a fully playable Pro-subscriber-exclusive onboarding starter path alongside Electronics. Three Iron Ore-driven products (Residential Block, Commercial Block, Industrial Block) are seeded with direct iron-ore manufacturing recipes and are Pro-gated at both the backend (`ProOnlyStarterIndustries` constant in `startOnboardingCompany` / `finishOnboarding`) and the frontend (industry card with PRO badge, crane 🏗️ icon, and error on non-Pro click). The manufacturing encyclopedia surfaces the Construction chain via the existing industry filter. All existing test suites pass, and new backend and E2E tests cover Pro-gating, product seeding, recipe correctness, and the full Construction onboarding flow.

**Shipped increment (Pharmaceuticals, Energy, Logistics Pro-starters):** Three new Pro-exclusive starter industries are now fully playable:

- **Pharmaceuticals 💊** — Gold-driven industry with three tier-1 products: Aspirin (€55), Vitamin Capsule (€80), Antibiotic (€120). High-margin pharma chain with inelastic consumer demand. Plus 9 advanced tier-2/3 products (Analgesic Syrup, Antiseptic Gel, Cough Syrup, Eye Drops, Pharmaceutical Capsule, Medical Cream, Vaccine Vial, Insulin Pen, Paracetamol Pack, Diagnostic Kit, Nasal Spray).
- **Energy ⚡** — Coal-driven industry with three tier-1 products: Coal Briquette (€28), Heating Oil (€50), Industrial Fuel (€75). Steady industrial demand across every city. Plus 9 advanced products (Coke Block, Charcoal Pack, Gas Canister, Battery Cell, Power Pellet, Refined Kerosene, Turbine Oil, Fuel Rod, Compressed Gas Bottle, Energy Tablet).
- **Logistics 📦** — Cotton-driven industry with three tier-1 products: Shipping Bag (€20), Storage Sack (€35), Cargo Pack (€55). Volume-play supply chain backbone. Plus 8 advanced products (Cotton Wrap, Padded Envelope, Tote Bag, Fabric Label, Insulated Liner, Pallet Cover, Courier Bag, Heavy Duty Sack, Flat Pack Box).

All three industries are Pro-gated at both backend (`ProOnlyStarterIndustries`) and frontend (PRO badge on industry card, upgrade modal for non-Pro players). Full E2E tests cover the onboarding flow for each industry, Pro-gating errors, product selection step, and card content (first product hint, why-choose tagline, description).

- [x] Add Electronics industry with Silicon as raw input: define product types for Basic Electronics, LED Screen, and Circuit Board with manufacturing recipes linking Silicon resource to each product via factory purchase → manufacturing → public sales chain.
- [x] Expose Electronics as a Pro-subscription-only starter choice in the onboarding industry selection step, gating it behind `Player.IsProSubscriber` on the backend; free players see only Furniture, Food Processing, and Healthcare.
- [x] Add 3 Electronics starter products to the database initializer with correct silicon-only recipes resolvable via the manufacturing encyclopedia.
- [x] Update encyclopedia and resource detail views to surface Electronics product chains (Silicon resource detail page links to all three starter products).
- [x] Add Construction industry with Iron Ore as raw input: three starter products (Residential Block, Commercial Block, Industrial Block) with direct iron-ore manufacturing recipes and higher base prices ($80–$180) compared to other starter industries.
- [x] Expose Construction as a Pro-subscription-only starter choice alongside Electronics.
- [x] Add Pharmaceuticals industry with Gold as raw input: three tier-1 starter products (Aspirin €55, Vitamin Capsule €80, Antibiotic €120) and 9 advanced tier-2/3 products with manufacturing recipes.
- [x] Add Energy industry with Coal as raw input: three tier-1 starter products (Coal Briquette €28, Heating Oil €50, Industrial Fuel €75) and 9 advanced products covering fuel processing and storage.
- [x] Add Logistics industry with Cotton as raw input: three tier-1 starter products (Shipping Bag €20, Storage Sack €35, Cargo Pack €55) and 8 advanced packaging and shipping products.
- [x] Expose Pharmaceuticals, Energy, and Logistics as Pro-subscription-only starter choices with PRO badges, disable click for non-Pro players with upgrade modal.
- [x] Integrate all three new industries into the onboarding industry selector with i18n keys (en/sk/de) for descriptions, first-product hints, and why-choose taglines.
- [x] Backend integration test `FinishOnboarding_ResultIncludesSelectedProductBasePrice_ForAllIndustries` covers all 8 industries including the three new ones.
- [x] Full Playwright E2E tests for each new industry: card visible to authenticated users, PRO badge, non-Pro upgrade modal, Pro subscriber can advance, product step shows three starter options, card content assertions.

### Supply chain visualization (100% complete)

- [x] Add a "Supply Chain" tab to the building detail view for factories that renders an interactive flow diagram showing the connected purchase → manufacturing → storage → B2B sales / public sales unit chain, with arrows indicating resource direction and color-coded resource fill levels per unit so players can diagnose bottlenecks at a glance.
- [x] Show transit cost estimates as tooltip labels on each inter-unit arrow in the supply chain diagram so players understand the running shipping cost of each resource hop without opening individual unit panels.
- [x] Persist a supply chain "health score" per building visible in the dashboard company card: green when all linked units have stock moving, yellow when any unit stalled for more than 5 ticks, red when a unit has been empty for more than 20 ticks consecutively.

### Competitive market intelligence (100% complete)

- [x] Add a `/market-intelligence` view accessible from the player dashboard that shows, per product type, a ranked table of all sellers currently offering that product in the selected city: display name, asking price, brand quality percentage, and estimated weekly sales volume derived from public sales records, so players can benchmark pricing strategy against competitors.
- [x] In the public sales unit detail panel, add a "Competition" section showing a pie chart of market share by player (anonymized to "Player A/B/C" outside top 3) and a mini price-history chart for the product in that city over the last 100 ticks, so the current PRODUCT-DEFINITION.md market-share pie is fully implemented and visible.
- [x] Surface resource price trends on the global exchange view as a sparkline chart per resource showing the last 50 ticks of ask prices so players can time their mine or factory purchases relative to market cycles.

### Player notifications and alerts (100% complete)

- [x] Design and implement a notification entity on the game backend that stores per-player events: building construction complete, pending upgrade applied, loan repayment due within 10 ticks, bank account balance below configurable threshold, and B2B sale order fulfilled by another player.
- [x] Add a notification bell icon to the navigation bar showing unread notification count as a badge, opening a slide-over panel listing the last 20 notifications with timestamp and a direct link to the relevant building, bank account, or loan contract.
- [x] Allow players to configure alert thresholds per bank account (minimum balance trigger) and per public sales unit (notify when inventory drops below X units) through the building detail and bank account settings panels.

### City expansion (80% complete)

**Shipped increment (Berlin and Warsaw):** Berlin (DE, EUR) and Warsaw (PL, PLN) are now fully playable cities. Both cities are seeded with correct GPS coordinates within validated bounds, population data, salary rates, fuel-price indices, and resource abundance profiles. Curated hand-crafted building lots (7 per city) cover all building types: industrial/mine deposits (coal, iron-ore, grain, wood), factory-only starter sites, commercial storefronts, bank/office plots, residential blocks, and energy zones. The `/cities` overview page shows all 9 cities with key metrics and resource chips. The onboarding city-selection step lists Berlin and Warsaw alongside all other cities with their EUR/PLN currency badges. Integration tests validate lot seeding, city metadata, coordinate bounds, resource abundances, and salary rates.

- [x] Add at least two additional cities to the seeded city list: Berlin (EUR, Germany) and Warsaw (PLN, Poland) with their own resource abundance profiles, starting lot inventory, weather patterns, and per-city salary base rates consistent with real-world data.
- [x] Implement inter-city trade routes: allow a player to configure a B2B sales unit in one city to fulfill purchase orders from a factory purchase unit in a different city, with transit costs calculated from real GPS distance and product weight constants defined in `GameConstants`. Route creation, IN_TRANSIT status tracking, inventory blocking, delivery with bank-account settlement, failure with inventory return, and concurrent multi-route handling are all implemented and covered by 16 backend integration tests and 10 Playwright E2E tests.
- [x] Add a city selection map overview page (`/cities`) that shows all available game cities on a world map with key metrics (population, active players, dominant industry, average resource prices) so new players can make an informed city choice during onboarding.

### Building secondary market (100% complete)

- [x] Allow a company owner to mark any building for sale via the building detail page, setting an asking price and a "negotiate" flag. Other players browsing the buy-building page can see for-sale listings alongside new lots and make an offer. The original owner accepts or rejects via a notification.
- [x] Implement building transfer: when a sale is accepted, atomically debit the buyer's bank account, credit the seller's bank account, transfer building ownership, and write LedgerEntry records for both parties under a new `BuildingAcquisition` category.
- [x] Show "For Sale" badge on city map lot markers and in the buy-building grid so players can discover available buildings at a glance.
- [x] Building Market page (`/buildings/market`) with "Market" and "My Listings" tabs, offer management (accept/reject), and city/type/price filters.
- [x] `BuildingSaleOffer` entity, EF migration, and `BuildingAcquisition`/`BuildingSale` ledger categories for full auditability.
- [x] 14 backend integration tests (mark for sale, unlist, insufficient funds, duplicate offers, ownership transfer, ledger entries, concurrent offers, unauthenticated guard) and 14 Playwright E2E tests covering the full buyer and seller flows.

### Resource depletion and scarcity feedback (100% complete)

- [x] Show a depletion progress bar in the mining unit detail panel: current remaining quantity vs. original deposit quantity, estimated ticks until depletion at current extraction rate, and a warning badge on the dashboard building card when remaining stock falls below 20%.
- [x] When a mine's raw material is fully depleted, stop the mining unit output and emit a player notification; display a "Depleted" badge on the building card and a recommended action to purchase a new mining lot.
- [x] Seed per-city resource replenishment events: every 8760 ticks (one game year), game engine randomly restores 20–30% of a subset of depleted mine deposits across all cities to simulate geological discovery, with a news event announcing the replenishment so players have opportunity to react.

**Shipped (May 2026):**
- Per-tick deduction of `BuildingLot.MaterialQuantity` by the mining unit's extraction rate each tick. `OriginalMaterialQuantity` seeded at initializer time for relative % tracking.
- Mining output becomes 0 when `MaterialQuantity ≤ 0`; `MineDepletionRecord` entity created for audit; `PlayerNotification` emitted with a deep-link to purchase a new mining lot.
- `ResourceReplenishmentSchedule` entity tracks per-city replenishment. Every 8760 ticks, 20–30% of fully-depleted lots are randomly restored to 20–30% of original quantity; a `GameNewsEntry` news item is broadcast per city.
- Frontend `MiningResourceStatusPanel` in the building detail shows deposit progress bar (green/yellow/red), remaining quantity, extraction rate, estimated ticks to depletion, and a "⚠️ Depletion Risk" badge below 20%.
- Dashboard building card shows a ⚠️ depletion risk badge for mine buildings below the 20% threshold.
- i18n keys added for all three locales (en/sk/de) under the `mining.*` namespace.

### Seasonal demand (100% complete)

- [x] Define a `DemandSeasonality` table seeded with per-product seasonal multipliers across the four game-year quarters (Q1 Jan–Mar, Q2 Apr–Jun, Q3 Jul–Sep, Q4 Oct–Dec) so that, for example, heating fuel has higher demand in Q4 and furniture has higher demand in Q2 spring/move season.
- [x] Apply the seasonal multiplier as an additional factor in `PublicSalesPhase` demand calculation alongside salary signal, brand quality, and price index so sales volumes fluctuate naturally during the year without requiring player action.
- [x] Expose the current season and seasonal demand outlook (next-quarter multiplier) in the public sales unit detail panel so players can plan inventory and pricing strategy ahead of demand peaks.

### In-game tutorials and interactive help (85% complete)

- [x] Add a `TutorialProgress` entity tracking per-player completion of guided tutorial milestones: first resource sold, first B2B trade, first loan taken, first competitor observed in market intelligence, first brand established.
- [x] `getTutorialProgress` GraphQL query returns all 5 milestones with `isCompleted` and `completedAtUtc` for the authenticated player. `markTutorialMilestoneComplete` mutation persists a milestone completion idempotently with full validation.
- [x] Add a `/tutorial` view accessible from the navigation that lists all 5 tutorial milestones with completion status, bounty points per milestone, a progress bar, and "Resume" deep-links for incomplete steps. View is public (unauthenticated visitors see descriptions but no Resume buttons).
- [x] `TutorialTooltip.vue` reusable component with fade-in animation, "Got it" dismiss button, Escape-key support, and 30-second auto-dismiss.
- [x] `useTutorialContext` composable for milestone state management, completion fetching, and completing milestones from any view.
- [x] All tooltip and tutorial UI strings available in English, Slovak, and German via vue-i18n.
- [ ] Contextual tooltip overlays on the dashboard and building detail views (first grid-editor open, first building detail visit) using `TutorialTooltip.vue` and `useTutorialContext` — integration deferred to next increment.
- [ ] The tutorials does not grant the points to the master ranking at the moment. Create bounty in the master ranking for every tutorial. Make sure that the tutorial bounties are counted only once per lifetime per user. Make sure the tutorial is marked as completed if the bounty is awarded.

### Player profile and statistics page (60% complete)

**Shipped (increment 1):** `/player/:id` public profile page is live. Each player row on the leaderboard now links directly to the profile page. The profile displays: player display name (with Pro badge), join date, game year, bio (editable by the player, max 160 chars), global leaderboard rank, total wealth in USD, company count, cities active, active building types, total products sold, company equity, and a Hall-of-Fame panel (highest single-tick revenue, largest building acquisition, highest brand quality with brand name). Backend: `playerProfile(playerId: UUID!)` GraphQL query, `updatePlayerBio` mutation (authenticated), and `Players.Bio` column (EF Core migration `AddPlayerBio`).

- [x] Add a `/player/:id` public profile page showing: player display name, join date (game year), total company equity, current leaderboard rank, industries active in, number of cities with buildings, and total products sold across all ticks.
- [x] Include a "Hall of Fame" panel on the profile page listing the player's highest single-tick revenue, largest single acquisition, highest brand quality ever achieved.
- [x] Allow players to add a short bio (max 160 chars) visible on their profile page.
- [ ] Add a custom profile badge unlocked by specific master-ranking bounty completions, visible on the leaderboard table.
- [ ] Rank history chart over last 365 ticks.
- [ ] Export statistics as PDF or CSV.

### Company growth and second IPO path (100% complete)

- [x] Implement a "New Company IPO" flow accessible from the personal account dashboard after the player's first company has been operational for at least 1 game year (8760 ticks): player configures a new company name, selects raise amount and ownership split (same 25/33/50% tiers as onboarding), and funds the new company bank account via the stock exchange.
- [x] Enforce a maximum of 5 player-controlled companies per person account to prevent monopolistic lockout of all available land lots while still enabling meaningful business diversification.
- [x] Show the "Start New Company" CTA on the personal account dashboard only when prerequisites are met (first company profitable for ≥ 365 ticks, player balance ≥ $200k), with a tooltip explaining the requirement when the button is locked.

**Shipped (increment 1):** `startAdditionalCompany` GraphQL mutation enforces five prerequisite gates (oldest company ≥ 8 760 ticks, ≥ 365-tick profitability window, personal USD balance ≥ $200 k, < 5 existing companies, valid city). On success it atomically creates the new `Company`, provisions an FX-scaled bank account, debits the player's personal settlement account, records `FounderContribution` + `IpoRaise` ledger entries, and seeds a founder `Shareholding`. The `additionalCompanyPrerequisites` query returns a per-gate eligibility snapshot for the CTA checklist. The frontend `NewCompanyModal.vue` two-step wizard (name+city → IPO tier cards) lives on the personal-account Overview tab of the Dashboard, showing a prerequisite checklist when gates are not met and redirecting to the onboarding flow after a successful launch.

### City economic health indicators (100% complete)

- [x] Add a `CityEconomicReport` that is computed each tax cycle and stores: total salaries paid in the city that cycle, total public sales revenue, number of active companies, total power consumption vs. supply, and average product quality index across all public sales units.
- [x] Show a "City Health" mini-dashboard card on the city map page (`/city/:id`) with an overall economic index score (0–100) derived from the latest report, a traffic-light colour (green/yellow/red), and sparkline trends for the last 10 tax cycles.
- [x] Surface the city economic health index as an input to the `PopulationIndex` recalculation so thriving cities see slight population growth over game years and declining cities (few salaries, low power) see slow population erosion, creating genuine city competition dynamics.

**Shipped:** `EconomicReportPhase` (Order=1050) computes per-city economic index each tax cycle (0.4×salary + 0.3×revenue + 0.15×power + 0.15×quality scores). Index ≥70 → +0.5% population growth, 40-69 → neutral, <40 → −0.2% erosion. GraphQL queries `getCityEconomicReport` and `cityEconomicHistory` expose data. `HealthIndicatorsPanel.vue` renders SVG score ring, traffic-light status badge, 2×2 metrics grid, sparkline trend, and detail modal with full history. 15 backend integration tests and 8 E2E tests shipped.

### Dashboard speed — 100% complete

- [x] When I go to /dashboard it takes few seconds to load with few players in the game server. Make sure it is optimized well and takes less then 100ms to load. — 100% complete.

**Shipped (increment 1):** Dashboard initial load now batches critical startup data (`myCompanies`, `gameState`, `myPendingActions`, and `cities`) into one GraphQL request and renders immediately, while non-critical derived analytics (city power, ledgers, unit status, building financial summaries) hydrate asynchronously in the background and only for the active company context.

**Shipped (increment 2 — performance optimization):** Eliminated the root-cause backend bottleneck: `GetMyCompanies` GraphQL resolver was calling `BuildingConfigurationService.ApplyDuePlansAsync` on every dashboard read — a write path that loads **all** pending building-configuration plans across **all** players and calls `SaveChangesAsync`. With 50+ active players and hundreds of buildings this caused multi-second dashboard loads. Fixes applied: (1) `GetMyCompanies` is now fully read-only — plan application is the exclusive responsibility of `BuildingUpgradePhase` in the tick engine; (2) `AsNoTracking()` added so EF does not track read-only query results; (3) per-user 5-second `IMemoryCache` entry added to throttle burst concurrent requests from the same player (e.g. rapid page reloads); (4) frontend `performance.now()` timing added to `DashboardView` so load times are observable in browser DevTools console (`[Dashboard] Initial load: Xms`). A dedicated regression test (`DashboardPerformanceTests`) verifies the read-only invariant: a due plan must remain in the DB after a `myCompanies` query, and must only be deleted after `TickProcessor.ProcessTickAsync` runs.

### Bots

- [x] Create NPC bot console app — 100% complete. `projects/NPCBot/` is a standalone .NET 10 console application targeting the game GraphQL API. It supports configurable bot counts, allowed industries, poll interval, and graceful Ctrl+C shutdown.
- [x] If the bot did not setup a company yet, create an account and resolve the onboarding process — 100% complete. The bot automatically registers (or logs in if already registered), completes `startOnboardingCompany` + `finishOnboarding`, and resumes from the shop-selection step if interrupted mid-flow.
- [x] On npc bot console app run check the current state of the account and check if it is profitable to change the current settings — 100% complete. `BotProfitCalculator` (pure static helpers) classifies profitability as Profitable/Neutral/Unprofitable/Unknown, computes annualised profit rate, and produces a `StrategyRecommendation` (including a price-adjustment factor) when the bot has been running long enough. `BotStateValidator` validates token, onboarding, skipped, and staleness state, producing a `BotStateValidationResult` with per-issue diagnostics and an immutable issue list. `PriceAdjustmentHelper` provides pure, testable helpers (`SelectAdjustableUnits`, `ComputeNewPrice`, `IsAdjustmentMeaningful`). `PriceAdjustmentService` applies the recommendation by calling the `updatePublicSalesPrice` GraphQL mutation for all PUBLIC_SALES units each tick. `BotAccount.PendingRecommendation` stores the latest advisory and is always cleared after every apply attempt. `BotAccount.CurrentRank` stores the bot's last-known leaderboard position, populated each tick by `FetchRankingsAsync` (best-effort; rankings fetch failure is non-fatal). `BotOrchestrator.GetBotStatusLabel` and `ComputeRecommendationForBot` expose pure logic (ACTIVE/ONBOARDING/NO_TOKEN/SKIPPED, tick-elapsed-aware recommendation) for observability and testability. `GraphQLResponseParser` exposes the pure JSON error-parsing logic from `GameApiClient` as testable statics (`ParseFirstError`, `HasErrors`, `HasData`). `OnboardingHelpers.ShouldResumeFromShopStep` exposes the mid-flow resume decision as a testable static. `BotOrchestrator` depends on `IAccountService`, `IOnboardingService`, and `IPriceAdjustmentService` interfaces (injected via DI) so the full RunAsync loop and tick logic can be tested with in-memory fakes. The companion `projects/NPCBot.Tests/` project ships **823 unit and integration tests** across `BotAccountTests`, `BotRosterFactoryTests`, `BotOptionsTests`, `GameModelsTests`, `BotProfitCalculatorTests`, `BotStateValidatorTests`, `OnboardingHelpersTests`, `StrategyRecommendationTests`, `GraphQLExceptionTests`, `GraphQLResponseParserTests`, `PriceAdjustmentHelperTests`, `BotDecisionFlowTests`, `BotOrchestratorTests`, `BotOrchestratorIntegrationTests` (35 tests), `BotOrchestratorErrorCoverageTests` (9 tests for tick-level error paths and ConsecutiveErrors lifecycle), `BotOrchestratorAdvancedTests` (10 tests — zero-bot roster, rank cleared when bot drops from rankings, rank changes across consecutive ticks, all-bots-pre-skipped tick, CurrentNetWorth updated from fresh profile, `IsAtRisk` with maxConsecutiveErrors=0, three-bot simultaneous rank assignment, empty rankings list clearing pre-set rank, MinTicksBeforeAdjustment guard), `BotSystemScenarioTests`, `BotRobustnessScenarioTests`, `BotAdditionalEdgeCaseTests`, `BotTickProgressionTests`, `PriceAdjustmentServiceTests`, `BotCoverageCompletionTests`, `AccountServiceTests` (all 6 public methods — register, DUPLICATE_EMAIL fallback, login, profile deserialization with nested companies/units, game-state, rankings, price update), `GameApiClientTests` (HTTP error paths 500/401/404, GraphQL errors array, errors-take-precedence, no-code errors, missing data field, auth header injection, omission when null, pre-cancelled token), `OnboardingServiceTests` (full 6-step happy path, SHOP_SELECTION resume with 3 HTTP calls, null profile guard, empty cities/industries/lots/products error cases, GraphQL error propagation, HTTP 500 propagation, pre-cancelled token, multi-option selection paths), `BotThirdWaveCoverageTests` (20 tests — rank preserved when rankings throw, BotProfitCalculator constant-value regression protection, StrategyRecommendation reason-text format verification, total-loss classification, NoAction singleton coherence, NoAction singleton identity check for neutral/profitable bots, and BotOptions defaults), and `BotSixthWaveCoverageTests` (13 tests — orchestrator tick price-adjustment throw scenarios, BotStateValidator multiple simultaneous issues, BotAccount.OnboardingCompleted after profile nulled, ContainsSuitableType whitespace-only field, StrategyRecommendation default Reason, BotProfitCalculator boundary cases, and ComputeNetWorth single-company profile), and `BotSeventhWaveCoverageTests` (30 tests — ComputeRecommendationForBot negative/just-below/exactly-at tick-threshold boundary cases, ComputeAnnualisedRatePercent full-year and two-year cycles, BotRosterFactory strategy cycling/email format/index sequence/display-name format, SelectAdjustableUnits null/zero MinPrice exclusion with mixed-list variant, ShouldResumeFromShopStep non-resumable step values and case-insensitive match, IsAtRisk at 100% and 1% ratio boundaries, StrategyRecommendation.NoAction factor=0 and singleton identity, BotOptions AllowedIndustries default count and TokenRefreshBufferMinutes consistency, BotAccount.TrackingStartTick default=0, GameStateSummary TaxCycleTicks JSON deserialization, CompanySummary multi-building JSON roundtrip, CurrentRank lifecycle, and Classify identical/extreme/total-loss edge cases), and `BotNinthWaveCoverageTests` (15 tests — tick-phase CT guard between bots proving FetchProfileCallCount stops at 5 not 6, rankings-assigned-before-TickBotAsync ordering proving bot1 rank set but bot2 null when CT fires, skipped-bot rank freeze proving `continue` fires before rankings assignment, active/skipped two-bot mixed scenario, BotOptions GraphqlUrl/BotPassword/AllowedIndustries default regression guards, BotProfitCalculator.Recommend profitable-side neutral-band exact boundary, BotRosterFactory email-domain end-to-end with custom domain, BotAccount HasValidToken after token assignment, whitespace token rejection, BotStateValidator multi-issue summary join and Issues list immutability), and `BotTenthWaveCoverageTests` (19 tests — IsReadyForOperation vs Validate divergence for stale bots, GetBotStatusLabel returns ACTIVE for stale-but-ready bot, roster-wide email and display-name uniqueness invariants, 1-based sequential Index invariant, email lowercasing with uppercase BotNamePrefix, ComputeNetWorth with empty company list, ProfitDelta exactly-zero crossing, GraphQLException catch-by-base-Exception pattern, AllowedIndustries no-duplicate guard, immutable Email identity, PriceAdjustmentHelper constant regression guards, and IsReadyForOperation/Validate agreement for healthy non-stale bots).

### Architecture optimization (62% complete)

- [x] Make sure to split big files into the components on frontend or better classes on backend. Make sure no file is bigger then 500 lines. — 100% complete.
- [ ] Optimize the pefromance for tick calculations, make sure it works as efficient as possible, while preserving the security of the game accounts. Make sure that game is playable by thousounds of people at one time.

**Shipped (increment 1):** `GameConstants.cs` was split into partial engine files to reduce monolithic backend source size, and tick processing now avoids per-tick phase re-sorting and reduces allocations in recent-salary aggregation during `BuildContextAsync`.

**Shipped (increment 2):** `PublicSalesPhase` was split into partial backend class files to keep engine sources maintainable, and tick sales processing now reuses preloaded `TickContext.LotsByBuildingId` instead of executing a per-tick database lot lookup query.

**Shipped (increment 3):** `BankStatementView.vue` was split into dedicated banking presentation components (`BankStatementSummaryCard` and `BankStatementTable`), reducing the view from 513 lines to 312 lines while preserving the existing account-selection, filtering, and pagination flow.

**Shipped (increment 4):** `ContextSwitcher.vue` was split so the dropdown panel now lives in `ContextSwitcherPanel.vue`, reducing the parent layout component from 525 lines to 295 lines while preserving the existing city/account switcher selectors and behavior.

**Shipped (increment 5):** `ProductPicker.vue` was split so the teleported dropdown panel now lives in `ProductPickerPanel.vue`, reducing the picker parent from 735 lines to 278 lines while preserving the existing `.product-picker-panel` and `.picker-*` DOM hooks used by the UI flow.

**Shipped (increment 8 — second frontend refactor):** Three more large Vue files were split via composables and child components, all now under 500 lines:
- `StockExchangeView.vue` 923 → 7 lines (thin wrapper); `StockExchangeContent.vue` 290 lines (template + CSS); `useStockExchange.ts` composable 292 lines; `stockExchangeQueries.ts` 165 lines.
- `ForexExchangeView.vue` 980 → 7 lines (thin wrapper); `ForexExchangeContent.vue` 168 lines (tabs scaffold); `useForexData.ts` composable 156 lines; `ForexSwapSection.vue` 287 lines.
- `AdminDashboardContent.vue` 834 → 407 lines; `AdminNewsComposer.vue` 317 lines (self-contained news composer); `AdminPlayerManagement.vue` 272 lines (government + players sections).

**Shipped (increment 7 — view refactoring):** Four large view files were each split into a parent orchestrator + dedicated child component using props/emits, all now under 500 lines:
- `DashboardView.vue` 921 → 411 lines — `DashboardMainContent.vue` handles all company tabs, create-company flow, and NewCompanyModal.
- `LedgerView.vue` 860 → 198 lines — `LedgerMainContent.vue` handles the ledger table and drill-down panel.
- `MarketingAnalyticsView.vue` 817 → 270 lines — `MarketingAnalyticsContent.vue` handles analytics table helpers and CSS.
- `CityMapView.vue` 744 → 359 lines — `CityMapContent.vue` handles Leaflet map initialisation, lot selection, and all city section sub-components.

**Shipped (increment 6 — tick performance):** Three strategic database indexes were added via EF Core migration `AddTickPerformanceIndexes` to eliminate full-table scans on the hot tick path:
- `IX_ExchangeOrders_IsActive` — the tick context loads only active orders each tick; previously every tick did a full scan of the entire historical order book.
- `IX_InterCityTradeRoutes_Status_ExpectedArrivalTick` — `TradeRoutePhase` queries in-transit routes by status and arrival tick; without this index the phase performed a full table scan on every tick.
- `IX_LedgerEntries_Category_RecordedAtTick` — the salary-window computation queries ledger entries by category without a company filter; existing indexes all started with `CompanyId`, causing a full table scan on a high-volume table with 1000+ concurrent players.
Per-phase timing was added to `TickProcessor` (each phase is now individually stopwatch-measured and logged when any phase exceeds 100 ms, or the total tick exceeds 500 ms), giving operators instant visibility into which phase is the bottleneck. A reproducible `TickPerformanceBenchmarkTests` test class seeds 20 simulated players with realistic factory + shop portfolios and runs 3 consecutive ticks, asserting correctness (buildings survive, ledger entries created) and a 10-second total time budget for the EF Core InMemory provider.

### Archive E2E tests (60% complete)

- [ ] Optimize test speed so that every tests (.net tests, e2e tests and unit tests) runs faster and takes no more then 10 minutes to run
- [ ] Pick only the most important tests to keep which allows wider end to end testing and archive all other tests so that the tests will take less then 10 minutes to run
- [x] please organize the frontend e2e tests to special folders . I want to archive old tests to e2e/archive folder, screenshots for documentation tests to e2e/docs, full end to end tests to e2e/full-journey. Also organize the next level subfolder according to the test category. Extract full journey tests from the current test files to full-journey folder. Create npm run commands to run the tests. Also update the pipeline which runs e2e tests to run only full journey tests.
- [x] For further development please focus on running full journey end to end tests only which tests longer user walkthrough with navigation and expected features.
- [x] Update copilot instructions to follow this rules.

**Shipped (increment 1):** Frontend Playwright specs are now organized by intent: `e2e/full-journey/<category>/` for the canonical CI suite, `e2e/docs/<category>/` for screenshot documentation specs, and `e2e/archive/<category>/` for archived regression coverage. NPM scripts were added for full-journey/archive/docs runs, screenshot paths were updated, and `.github/workflows/playwright.yml` now executes only the full-journey suite by default.

### Real-world Map Integration (100% complete)

- [x] Integrate Leaflet.js mapping library into the Vue3 frontend for interactive city map rendering (`CityMapView.vue` at route `/city/:id`).
- [x] Store GPS coordinates (latitude/longitude) on `BuildingLot` entity with double precision (≥6 decimal places).
- [x] Implement Haversine distance calculation in `GlobalExchangeCalculator.ComputeDistanceKm` with ≤0.5% error for inter-city routes — within the 2% accuracy requirement.
- [x] Calculate logistics cost based on GPS distance: `cost = distanceKm × weightPerUnit × TransitCostRatePerKmPerWeightUnit × fuelPriceIndex`.
- [x] Implement `LandService.EnsureMinimumAvailableLotsAsync` to guarantee ≥10 available lots per building type per city via procedural generation.
- [x] Expose `cityLots(cityId)` as a public GraphQL query (no auth required) returning lots with GPS coordinates, population index, resource deposits, and appraised value.
- [x] Implement `purchaseLot` mutation with optimistic concurrency control to prevent race conditions.
- [x] Add population index calculation (`ComputePopulationIndex`) based on distance from city center and economic activity.
- [x] Show map markers color-coded by ownership (green = available, blue = player-owned, gray = competitor).
- [x] Implement lot detail panel showing GPS coordinates, population index, appraised value, resource premium, and supported building types.
- [x] Seed Bratislava with 14 named lots covering all building types at realistic GPS coordinates within city bounds.
- [x] Seed Berlin and Warsaw as new cities with GPS coordinates, resource abundances, and building lots.
- [x] Add unit tests for: Haversine distance accuracy vs. WGS-84 geodesic reference (<2%), land availability constraints (≥10 per type per city), GPS coordinate bounds for all seeded cities, population index bounds, logistics cost scaling.
- [x] Add E2E full-journey tests for: map rendering, GPS coordinate display, lot purchase flow, GPS immutability post-purchase, performance with 100+ lot markers.



- [ ] Migrate all views to Tailwind
- [ ] Update all components to use Tailwind utilities

**Shipped (increment 2 — core gameplay views):** Migrated five high-traffic views from legacy scoped CSS to Tailwind v4 utilities: `LeaderboardView` (wealth rankings with gradient hero, tab switcher, medal cards), `NewsView` (news/changelog feed with pill badges, unread indicators, and market-report table styles), `CompanySettingsView` (company profile, overhead dashboard, salary table), `PersonalLedgerView` (personal wealth breakdown, share trade history, dividend history), and `ManufacturingEncyclopediaView` (catalog grid with search, industry filter, and resource/product cards). All E2E selector classes preserved; scoped CSS fully removed from all five files (total ~1,200 style-lines eliminated). Files reduced well below the 500-line limit.

### Power plants (100% complete)

- [x] When I edit powerplant building, and click the empty unit in the grid, I do not see any options to setup any of the unit. Make it to work similarily as the factory for example where every unit will have special feature.

### Units (100% complete)

- [x] Do not show bank account change if unit is selected in a grid while editing the building
- [x] When new unit is selected in the grid, automatically select that unit. So if i create new purchase unit in position 1,1 i do not want the user to click on that unit again to configure it.
- [x] Fix css styles after tailwind migration. Make sure the design is professional.

### Audits (0% complete)

- [ ] In root directory create audits folder, and every week do the audit of the security. List all potential risks and create the action plan to resolve them. The main focus should be on question: Can one player gain unfair advantege of another player by executing an api call or exploting some unfair game mechanics?

### Media house (20% complete)

- [ ] When media house is in the construction, allow the marketing units to configure it.
- [ ] When media house is in the construction, do not make any caluclations for the marketing units, only charge the unit labor and energy costs.

### Mining (100% complete)

- [x] Make sure every mining land property has the custom resource defined what is in that property. It must have the quality and resource amount defined. 
- [x] For each resource must be always available at least one property in each city
- [x] When user buys the mining property using the buy building flow, make sure to show the resource quality and quantity available at the property land. 
- [x] Make sure user can filter the land by the resource type when buying the mining property.
- [x] Make sure the prices for the purchase of the land is very expensive ~ $20M to $200M depending on the quality of the resource and the amount of resource there is available to be mined.

**Shipped (increment 1):** Dynamic land generation now guarantees that every MINE lot has a mapped resource deposit (`resourceType`, `materialQuality`, `materialQuantity`), enforces per-city coverage for every resource type, and clamps mine-lot deposit premiums into the strategic $20M-$200M band. Buy-building mining UX coverage was also extended with an E2E test for resource-type lot filtering.

**Shipped (increment 2):** Mine generation now enforces quality bands by city resource availability: resources native to a city are generated in the 50%-100% quality band, and fallback non-native resources are generated in the 0%-50% quality band while still guaranteeing at least one mine lot per resource in every city. The buy-building mine purchase cards and selected-lot summary now show raw-material quality and quantity directly instead of population index.

### Appartment and commercial buildings (100% complete)

- [x] I do not see the appartment building size. Make sure when buying the property the size of the commercial building or appartment building is clearly stated. Fix the current buildings which does not have the total area filled in.
- [x] Occupancy must be always a number. When there is no occupancy there must be 0%
- [x] I do not see the occupancy to be changed. Make sure the occupancy rules are applied.

### Newsroom

- [x] Add pagination to the news items. By default show last 10 items

### Support system (100% complete)

- [x] Design MasterApi support-ticket entities with ticket type, status, title, markdown source, sanitized preview, creator, timestamps, moderation fields, and immutable audit trail so workflow and security checks are fully traceable.
- [x] Implement support-ticket GraphQL mutations and queries for create, list, filter, sort, and status update flows, including strict authorization so users only see their own tickets while admins can access all tickets.
- [x] Implement ticket type validation allowing only Suggestion, Bug, and Other values, and reject malformed type inputs with consistent error codes to keep frontend filters and reporting deterministic.
- [x] Implement ticket status lifecycle with Submitted, In Progress, and Finished states, including explicit transition rules and administrator-only status updates so progress tracking remains reliable and auditable.
- [x] Build master-frontend user ticket page with sortable and filterable table by creation date and title, showing current state, last update, and ticket type for fast personal support tracking.
- [x] Build master-frontend admin ticket management page listing all users' tickets with default newest-first ordering and filters for type, date, and title to support high-volume triage operations.
- [x] Integrate a high-quality markdown WYSIWYG editor for ticket creation and editing, with image embed support, toolbar formatting controls, and client-side validation for required fields and content length.
- [x] Implement secure attachment and link handling pipeline that stores raw markdown and extracted URLs/images, flags unsafe content, and blocks formatted rendering until administrator review is explicitly approved.
- [x] Build admin moderation workflow that first displays raw markdown and raw link or image targets, then allows explicit safe-confirm action to unlock sanitized formatted preview for trusted content.
- [x] Implement markdown sanitization rules for rendered previews to prevent XSS, script URLs, unsafe HTML, and malicious embeds, while preserving allowed formatting that keeps support tickets readable and user-friendly.
- [x] Add notification and activity logging so users see status-change events and admins see moderation and workflow actions, including actor identity and timestamps for every critical support-ticket operation.
- [x] Add backend integration tests for permission boundaries, filter and sort behavior, status transitions, markdown sanitization, and moderation-gated rendering rules to prevent future regressions in support security.
- [x] Add master-frontend end-to-end tests covering user ticket submission, WYSIWYG markdown editing, table filtering and sorting, admin moderation approval flow, and visibility differences between normal users and administrators.

### Master ranking point system (95% complete)

- [x] Design MasterApi ranking entities for player points, bounty definitions, bounty reward records, daily scopes, server scopes, and one-time uniqueness keys so hourly evaluation can run idempotently without duplicate rewards or race conditions.
- [x] Implement a scheduled MasterApi hourly ranking evaluator that recalculates bounty eligibility for all players, writes reward records transactionally, and updates total points snapshots with clear audit metadata and processing duration metrics.
- [x] Implement UTC-midnight daily decay job that multiplies every player ranking score by 0.99, persists rounded values deterministically, and logs before and after totals to keep long-term competitive balance fair.
- [x] Build a player-facing master frontend ranking dashboard showing total points, global leaderboard position, movement trend, and competitive context so rankings feel rewarding and easy to compare with other players.
- [x] Build player bounty history UI in master frontend with filters by bounty type, date, game server, and status so each player can inspect exactly why and when points were awarded.
- [x] Add administrator bounty configuration interface for enabling, disabling, reward changes, visibility, proof requirements, and per-bounty validation settings while preserving immutable audit history for every configuration change.
- [x] Implement anti-duplication and cooldown guards that enforce daily reset windows, once-per-post logic, and once-per-day cross-server limits exactly according to each bounty definition and UTC boundary behavior.
- [x] Add internal observability dashboards and alerts for ranking evaluator failures, delayed schedules, abnormal reward spikes, and duplicate-key conflicts so operators can react before player trust is impacted.
- [x] Implement Game improver bounty integration with support ticket submission flow, awarding five points at most once per UTC day when a player submits a suggestion or bug report.
- [x] Implement Recommend a friend bounty integration with referral registration events, awarding five points once per UTC day when a referred player successfully creates a valid account using referral linkage.
- [x] Implement Recommend a good friend bounty integration with monetization events, awarding one hundred points once per UTC day when a referred player purchases startup pack or activates a paid subscription.
- [x] Implement Retweet a X post bounty workflow with admin-created bounty posts, URL submission, moderation queue, and reward issuance per post after manual verification of required friend tags.
- [x] Implement Retweet privacy controls so submitted social links are hidden from public player views, while administrators can review links and moderation decisions with timestamped approval or rejection reasons.
- [x] Implement Discord player bounty verification by linking Discord bot validation events to master accounts, awarding a one-time fifty-point reward only after successful ownership verification and anti-fraud checks.
- [x] Implement Discord privacy model storing Discord username in protected admin-only fields, excluding it from public ranking pages and player-exposed bounty records while preserving secure admin audit access.
- [x] Implement Log in to the game bounty ingestion from game servers, awarding five points once per UTC day for each distinct game server where the player opens dashboard successfully.
- [x] Implement Manufacturer bounty detection from game telemetry, awarding one point once per UTC day when player factories produce any product quantity on any server with cross-server deduplication.
- [x] Implement Wholesaler bounty detection from sales-shop telemetry, awarding one point once per UTC day when player shops sell any product quantity on any server with cross-server deduplication.
- [x] Implement Researcher bounty detection from R&D telemetry, awarding two points once per UTC day when any owned R&D unit has an active research budget configured on any server.
- [x] Implement Real estate magnate bounty detection, awarding two points once per UTC day when player-owned apartment or commercial buildings have nonzero occupancy on any server.
- [x] Implement Media owner bounty detection, awarding two points once per UTC day when player-owned media houses have any nonzero content-creation budget configured on any server.
- [x] Implement Banker bounty detection, awarding two points once per UTC day when another user deposits funds into a player-owned bank on any server.
- [x] Implement Lender bounty detection, awarding two points once per UTC day when another user maintains an active loan in a player-owned bank on any server.
- [x] Implement FX Trader bounty detection, awarding two points once per UTC day when player completes any currency swap between in-game currencies on any server.
- [x] Implement Stock Trader bounty detection, awarding two points once per UTC day when player buys any stock on any server with strict event deduplication and replay-safe ingestion.
- [x] Implement Energy Trader bounty detection, awarding two points once per UTC day when a player-owned power plant ships any energy amount to the market on any server.
- [x] Implement Good employer bounty calculation, awarding ten points once per UTC day when player has the highest wage rate in a city where salaries are actively paid on any server.
- [x] Implement Dividends master bounty detection, awarding two points once per UTC day when a player-owned company pays dividends to shareholders on any server.
- [x] Implement Top player bounty detection, awarding five points once per UTC day when player personal account rank is inside top ten on any server during hourly evaluation window.
- [x] Implement Great player bounty detection, awarding two points once per UTC day when player personal account rank is inside top one hundred on any server during hourly evaluation window.
- [x] Implement Company master bounty detection, awarding five points once per UTC day when any player-owned company rank is inside top ten companies on any server during hourly evaluation.
- [x] Add comprehensive backend integration tests covering midnight decay, hourly processing idempotency, one-time bounties, daily cooldown resets, cross-server deduplication, and each bounty event trigger path.
- [x] Add master frontend end-to-end tests validating ranking leaderboard rendering, player bounty history filters, privacy rules for retweet and Discord data, and real-time updates after reward issuance.
