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

- [ ] Add pagination to the news items. By default show last 10 items