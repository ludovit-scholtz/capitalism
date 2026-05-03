# Capitalism Roadmap

Create a fun game on style of the capitalism II game. This game is economic simulation where players can experience price elasticity, resource scarcity, resource oversupply, different competition types, marketing, product quality, difficulties with scaling up the companies, and other base economic factors.

It will use real world map. The game will start in single city and later other cities will be added.

## Issues to work on

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

### More industries and products (70% complete)

**Shipped increment (Electronics Pro-starter):** Electronics industry is now a fully playable Pro-subscriber-exclusive onboarding starter path. Three Silicon-driven products (Basic Electronics, LED Screen, Circuit Board) are seeded with direct silicon manufacturing recipes, exposed in the encyclopedia and resource-detail views, and Pro-gated at both the backend (`Player.IsProSubscriber` check in `startOnboardingCompany` / `finishOnboarding`) and the frontend (industry card with PRO badge, error on non-Pro click). The manufacturing encyclopedia and resource detail views already surface the full Electronics chain via the existing industry filter. All existing test suites pass, and new backend and E2E tests cover Pro-gating, product seeding, and the full Electronics onboarding flow.

**Shipped increment (Construction Pro-starter):** Construction industry is now a fully playable Pro-subscriber-exclusive onboarding starter path alongside Electronics. Three Iron Ore-driven products (Residential Block, Commercial Block, Industrial Block) are seeded with direct iron-ore manufacturing recipes and are Pro-gated at both the backend (`ProOnlyStarterIndustries` constant in `startOnboardingCompany` / `finishOnboarding`) and the frontend (industry card with PRO badge, crane 🏗️ icon, and error on non-Pro click). The manufacturing encyclopedia surfaces the Construction chain via the existing industry filter. All existing test suites pass, and new backend and E2E tests cover Pro-gating, product seeding, recipe correctness, and the full Construction onboarding flow.

- [x] Add Electronics industry with Silicon as raw input: define product types for Basic Electronics, LED Screen, and Circuit Board with manufacturing recipes linking Silicon resource to each product via factory purchase → manufacturing → public sales chain.
- [x] Expose Electronics as a Pro-subscription-only starter choice in the onboarding industry selection step, gating it behind `Player.IsProSubscriber` on the backend; free players see only Furniture, Food Processing, and Healthcare.
- [x] Add 3 Electronics starter products to the database initializer with correct silicon-only recipes resolvable via the manufacturing encyclopedia.
- [x] Update encyclopedia and resource detail views to surface Electronics product chains (Silicon resource detail page links to all three starter products).
- [x] Add Construction industry with Iron Ore as raw input: three starter products (Residential Block, Commercial Block, Industrial Block) with direct iron-ore manufacturing recipes and higher base prices ($80–$180) compared to other starter industries.
- [x] Expose Construction as a Pro-subscription-only starter choice alongside Electronics.

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

### City expansion (0% complete)

- [ ] Add at least two additional cities to the seeded city list: Berlin (EUR, Germany) and Warsaw (PLN, Poland) with their own resource abundance profiles, starting lot inventory, weather patterns, and per-city salary base rates consistent with real-world data.
- [ ] Implement inter-city trade routes: allow a player to configure a B2B sales unit in one city to fulfill purchase orders from a factory purchase unit in a different city, with transit costs calculated from real GPS distance and product weight constants defined in `GameConstants`.
- [ ] Add a city selection map overview page (`/cities`) that shows all available game cities on a world map with key metrics (population, active players, dominant industry, average resource prices) so new players can make an informed city choice during onboarding.

### Building secondary market (0% complete)

- [ ] Allow a company owner to mark any building for sale via the building detail page, setting an asking price and a "negotiate" flag. Other players browsing the buy-building page can see for-sale listings alongside new lots and make an offer. The original owner accepts or rejects via a notification.
- [ ] Implement building transfer: when a sale is accepted, atomically debit the buyer's bank account, credit the seller's bank account, transfer building ownership, and write LedgerEntry records for both parties under a new `BuildingAcquisition` category.
- [ ] Show "For Sale" badge on city map lot markers and in the buy-building grid so players can discover available buildings at a glance.

### Resource depletion and scarcity feedback (0% complete)

- [ ] Show a depletion progress bar in the mining unit detail panel: current remaining quantity vs. original deposit quantity, estimated ticks until depletion at current extraction rate, and a warning badge on the dashboard building card when remaining stock falls below 20%.
- [ ] When a mine's raw material is fully depleted, stop the mining unit output and emit a player notification; display a "Depleted" badge on the building card and a recommended action to purchase a new mining lot.
- [ ] Seed per-city resource replenishment events: every 8760 ticks (one game year), game engine randomly restores 10–30% of a subset of depleted mine deposits across all cities to simulate geological discovery, with a news event announcing the replenishment so players have opportunity to react.

### Seasonal demand (0% complete)

- [ ] Define a `DemandSeasonality` table seeded with per-product seasonal multipliers across the four game-year quarters (Q1 Jan–Mar, Q2 Apr–Jun, Q3 Jul–Sep, Q4 Oct–Dec) so that, for example, heating fuel has higher demand in Q4 and furniture has higher demand in Q2 spring/move season.
- [ ] Apply the seasonal multiplier as an additional factor in `PublicSalesPhase` demand calculation alongside salary signal, brand quality, and price index so sales volumes fluctuate naturally during the year without requiring player action.
- [ ] Expose the current season and seasonal demand outlook (next-quarter multiplier) in the public sales unit detail panel so players can plan inventory and pricing strategy ahead of demand peaks.

### In-game tutorials and interactive help (0% complete)

- [ ] Add a `TutorialProgress` entity tracking per-player completion of guided tutorial milestones: first resource sold, first B2B trade, first loan taken, first competitor observed in market intelligence, first brand established.
- [ ] Render contextual tooltip overlays on the dashboard and building detail views the first time a player encounters a new UI area (e.g., the first time they open a factory's grid editor) with a "Got it" dismiss button that marks that milestone complete and never shows again.
- [ ] Add a `/tutorial` view accessible from the help menu that lists all tutorial milestones with completion status, points earned per milestone (tied to the master ranking bounty system), and a "Resume" deep-link to the relevant page for incomplete steps.

### Player profile and statistics page (0% complete)

- [ ] Add a `/player/:id` public profile page showing: player display name, join date (game year), total company equity, current leaderboard rank, industries active in, number of cities with buildings, and total products sold across all ticks.
- [ ] Include a "Hall of Fame" panel on the profile page listing the player's highest single-tick revenue, largest single acquisition, highest brand quality ever achieved, and longest consecutive days active.
- [ ] Allow players to add a short bio (max 160 chars) and a custom profile badge unlocked by specific master-ranking bounty completions, visible on their profile page and on the leaderboard table.

### Company growth and second IPO path (0% complete)

- [ ] Implement a "New Company IPO" flow accessible from the personal account dashboard after the player's first company has been operational for at least 1 game year (8760 ticks): player configures a new company name, selects raise amount and ownership split (same 25/33/50% tiers as onboarding), and funds the new company bank account via the stock exchange.
- [ ] Enforce a maximum of 5 player-controlled companies per person account to prevent monopolistic lockout of all available land lots while still enabling meaningful business diversification.
- [ ] Show the "Start New Company" CTA on the personal account dashboard only when prerequisites are met (first company profitable for ≥ 365 ticks, player balance ≥ $200k), with a tooltip explaining the requirement when the button is locked.

### City economic health indicators (0% complete)

- [ ] Add a `CityEconomicReport` that is computed each tax cycle and stores: total salaries paid in the city that cycle, total public sales revenue, number of active companies, total power consumption vs. supply, and average product quality index across all public sales units.
- [ ] Show a "City Health" mini-dashboard card on the city map page (`/city/:id`) with an overall economic index score (0–100) derived from the latest report, a traffic-light colour (green/yellow/red), and sparkline trends for the last 10 tax cycles.
- [ ] Surface the city economic health index as an input to the `PopulationIndex` recalculation so thriving cities see slight population growth over game years and declining cities (few salaries, low power) see slow population erosion, creating genuine city competition dynamics.

### Dashboard speed

- [ ] When I go to /dashboard it takes few seconds to load with few players in the game server. Make sure it is optimized well and takes less then 100ms to load.

**Shipped (increment 1):** Dashboard initial load now batches critical startup data (`myCompanies`, `gameState`, `myPendingActions`, and `cities`) into one GraphQL request and renders immediately, while non-critical derived analytics (city power, ledgers, unit status, building financial summaries) hydrate asynchronously in the background and only for the active company context.

### Bots

- [ ] Create NPC bot console app
- [ ] If the bot did not setup a company yet, create an account and resolve the onboarding process
- [ ] On npc bot console app run check the current state of the account and check if it is profitable to change the current settings

### Architecture optimization (24% complete)

- [ ] Make sure to split big files into the components on frontend or better classes on backend. Make sure no file is bigger then 500 lines.
- [ ] Optimize the pefromance for tick calculations, make sure it works as efficient as possible, while preserving the security of the game accounts. Make sure that game is playable by thousounds of people at one time.

**Shipped (increment 1):** `GameConstants.cs` was split into partial engine files to reduce monolithic backend source size, and tick processing now avoids per-tick phase re-sorting and reduces allocations in recent-salary aggregation during `BuildContextAsync`.

**Shipped (increment 2):** `PublicSalesPhase` was split into partial backend class files to keep engine sources maintainable, and tick sales processing now reuses preloaded `TickContext.LotsByBuildingId` instead of executing a per-tick database lot lookup query.

**Shipped (increment 3):** `BankStatementView.vue` was split into dedicated banking presentation components (`BankStatementSummaryCard` and `BankStatementTable`), reducing the view from 513 lines to 312 lines while preserving the existing account-selection, filtering, and pagination flow.

**Shipped (increment 4):** `ContextSwitcher.vue` was split so the dropdown panel now lives in `ContextSwitcherPanel.vue`, reducing the parent layout component from 525 lines to 295 lines while preserving the existing city/account switcher selectors and behavior.

**Shipped (increment 5):** `ProductPicker.vue` was split so the teleported dropdown panel now lives in `ProductPickerPanel.vue`, reducing the picker parent from 735 lines to 278 lines while preserving the existing `.product-picker-panel` and `.picker-*` DOM hooks used by the UI flow.

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