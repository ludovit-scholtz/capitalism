# Capitalism Roadmap

Create a fun game on style of the capitalism II game. This game is economic simulation where players can experience price elasticity, resource scarcity, resource oversupply, different competition types, marketing, product quality, difficulties with scaling up the companies, and other base economic factors.

It will use real world map. The game will start in single city and later other cities will be added.

## Issues to work on


### Dashboard speed

- [ ] When I go to /dashboard it takes few seconds to load with few players in the game server. Make sure it is optimized well and takes less then 100ms to load.

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

### Archive E2E tests (0% complete)

- [ ] Optimize test speed so that every tests (.net tests, e2e tests and unit tests) runs faster and takes no more then 10 minutes to run
- [ ] Pick only the most important tests to keep which allows wider end to end testing and archive all other tests so that the tests will take less then 10 minutes to run

### Tailwind migration (40% complete)

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