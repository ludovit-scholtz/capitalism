# Capitalism Roadmap

Create a fun game on style of the capitalism II game. This game is economic simulation where players can experience price elasticity, resource scarcity, resource oversupply, different competition types, marketing, product quality, difficulties with scaling up the companies, and other base economic factors.

It will use real world map. The game will start in single city and later other cities will be added.

## Issues to work on

### Architecture optimization (0% complete)

- [ ] Make sure to split big files into the components on frontend or better classes on backend. Make sure no file is bigger then 500 lines.
- [ ] Optimize the pefromance for tick calculations, make sure it works as efficient as possible, while preserving the security of the game accounts. Make sure that game is playable by thousounds of people at one time.

### Archive E2E tests (0% complete)

- [ ] Optimize test speed so that every tests (.net tests, e2e tests and unit tests) runs faster and takes no more then 10 minutes to run
- [ ] Pick only the most important tests to keep which allows wider end to end testing and archive all other tests so that the tests will take less then 10 minutes to run

### Tailwind migration (40% complete)

- [ ] Migrate all views to Tailwind
- [ ] Update all components to use Tailwind utilities

**Shipped (increment 2 — core gameplay views):** Migrated five high-traffic views from legacy scoped CSS to Tailwind v4 utilities: `LeaderboardView` (wealth rankings with gradient hero, tab switcher, medal cards), `NewsView` (news/changelog feed with pill badges, unread indicators, and market-report table styles), `CompanySettingsView` (company profile, overhead dashboard, salary table), `PersonalLedgerView` (personal wealth breakdown, share trade history, dividend history), and `ManufacturingEncyclopediaView` (catalog grid with search, industry filter, and resource/product cards). All E2E selector classes preserved; scoped CSS fully removed from all five files (total ~1,200 style-lines eliminated). Files reduced well below the 500-line limit.

### City selection (100% complete)

- [x] After the onboarding make sure to select the city which user selected in the onboarding. At the moment when user goes through and selects for example Prague, the first city is selected after he creates the account and logs in. Make sure to select his active city after user logs in.
- [x] When buying new building do not ask for the city where to build the building. Use the selection from the city navbar filter
- [x] In the context selection is the company cash visible. But there is error that the currency is not correct.

**Shipped:** Onboarding now persists the player's chosen city to localStorage after register/login so the active city is correct on all subsequent pages. The buy-building flow pre-selects the active city from the navbar filter instead of requiring a redundant city choice. The context switcher now formats company cash in the selected city's currency (e.g. CZK for Prague) instead of always showing USD.

### Government company (100% complete)

- [x] Hide government from the leaderboard. Keep it as player, make sure the game administrators can impersonalize to government player

**Shipped:** The government system account is now excluded from all public leaderboard queries (`rankings` and `companyRankings`). It remains a fully-functional internal simulation participant that owns banks, holds currency, and participates in economic flows. A dedicated "Government system account" section has been added to the admin operations dashboard so authorized administrators can view the government's balances and impersonate it via a clearly-labeled, admin-only button.

### Currencies and bank accounts (100% complete)

- [x] Fix the onboarding process. Make the initial player desposit in the currency where player pick up to do the business. At the moment 200k eur stays on the personal account, but it should be his first deposit to the business account. Make sure all operations like initial deposit to the player from government and IPO investment to company by public shareholders are clearly visible on the bank account.
- [x] In forex exchange show the fx rate list table and make the base currency for each other rate to be the selected city currency
- [x] Organizie forex exchange to tabs, add to the top forex tabs the amm features like add liquidity, show liquidity, and swap at AMM — Forex Exchange Gold AMM tab now has three inner sub-tabs: AMM Swap (buy/sell XAU via constant-product pools), My Positions (pool share %, claimable fees, remove liquidity), and Add Liquidity (join existing pools or create a new pool). Blocked-gold warning is shown whenever gold is locked in pools so players know they cannot use it for new swaps.
- [x] Player cannot go to minus on the bank account unless he pays money to the government for example for taxes or interest. Make sure that when player purchase items from other player in the purchasing unit for example, he cannot purchase more than he is able to pay from his building's bank account. If player do not have enough money to cover the labor costs the whole building is suspended for the tick and does not do anything. If this occurs, make sure to show this to the player on the frontend.
- [x] When selecting product in onboarding make sure to show the correct price. At the moment the product base price is showned without the fx rate adjustment.
- [x] Make the research budget be calculated in USD.
- [x] Make sure the costs for transportation are counted in local currency. Make them 10x higher as it is now to make them more significant. The pricing of the transportation costs depends on the oil price and it may be different for every city.
- [x] In B2B sales unit the recommended price is not adjusted by the fx rate. Find all occurances where this issue exists and fix it.
- [x] When buying new units, the price is not adjusted by the fx rate. Make sure the prices for units are similar in usd nomination in all cities. Find out what else is not adjusted by the fx rates where players can have advantage in one city over another because the number is the same.
- [x] In company settings when selecting salary multiplier make sure to show the proper city currency. Also when defining the base data make sure the base wage is set in the city currency properly and not in the usd for non usd cities.

**Shipped (this PR):** Unit upgrade costs and new unit placement costs are now FX-adjusted to the building's city currency. A building in Prague now shows and charges CZK amounts (e.g. 302,400 CZK for a new MANUFACTURING unit instead of 12,000 EUR). The `ScheduleUnitUpgrade` mutation and `BuildingConfigurationService.ApplyDuePlansAsync` both validate that the assigned bank account currency matches the city currency and reject with `CURRENCY_MISMATCH` on a mismatch. B2B recommended prices are also FX-adjusted via `useBuildingDetail.cityFxRate`. The unit-upgrade query (`unitUpgradeInfo`) returns the FX-adjusted cost for display.

**Shipped (R&D USD normalization):** Research budgets (`ProductResearchBudget.AccumulatedBudget`) are now always stored and compared in USD. The tick engine converts each unit's local-currency operating cost to USD before accumulating it, and the `baseQualityBudget` threshold (used to determine when a company reaches 100% uncontested quality) is also expressed in USD. The R&D panel in the building detail now displays all three budget figures (accumulated, target, top-competitor) formatted as USD amounts. Players in Prague (CZK), Vienna (EUR), New York (USD), and future cities all compete on an equal monetary footing for product-quality rankings.

**Shipped (city-aware salary settings):** Company settings salary table now shows each city's own currency code — Prague wages display as CZK, New York as USD, Delhi as INR, etc. The `CompanyCitySalarySettingResult` GraphQL type now includes a `currencyCode` field per city, and both the base wage and effective wage columns use the city's local currency formatter instead of the company's primary currency. A currency badge next to each city name and a clarifying note below the table make it unambiguous that wages are not cross-currency-converted. Backend and E2E tests verify the per-city currency codes are correct.

- [x] Allow to close down bank account if the balance of the account is equal exactly to 0.

**Shipped (zero-balance account closure):** Players can now permanently close a company bank account when its balance is exactly zero. The new `closeCompanyBankAccount` GraphQL mutation validates ownership, rejects government and deposit accounts, blocks closure if the account is still assigned as a building's active bank account (returning `ACCOUNT_IN_USE` with the building name), and rejects any non-zero balance with a clear `NON_ZERO_BALANCE` error code. The bank accounts tab in the Loan Marketplace now shows a "Ready to close — zero balance" badge for eligible accounts, displays an inline error if closure is blocked (e.g. still assigned to a building), and shows a non-zero-balance hint for accounts that need funds transferred out first. Five backend integration tests cover: happy-path closure, non-zero rejection, building-assignment rejection, wrong-owner rejection, and unauthenticated rejection.

- [x] Remove Loan Offers. Make sure every player can access any bank, including the government banks, and ask for a loan if he has a building available as collateral, and if the bank has enough deposits to provide loans.

### Number formatting (100% complete)

- [x] Create a vue component for number formatting in components/numbers folder
- [x] Everywhere where the currency is displayed, for example in the units, use the number formatting component
- [x] Add to the title the original number to be formatted and currency after it

### Power plants (96% complete)

**Shipped (previous increment):**
- 5 new power plant unit types added to the building grid: `FUEL_PURCHASE` (+10 MW/level fuel capacity), `WIND_TURBINE` (+8 MW/level weather-scaled), `WATER_TURBINE` (+12 MW/level steady hydro), `ENERGY_STORAGE` (+8 MW/level smoothing buffer), `ENERGY_PRODUCING` (+20 MW/level main converter).
- All 7 unit types (including `POWER_GENERATION` and `BATTERY_STORAGE`) allowed in `BuildingConfigurationService`.
- **Weather-scaling correctness fix**: `WATER_TURBINE`, `FUEL_PURCHASE`, and `ENERGY_PRODUCING` contributions are now computed AFTER the plant-level solar/wind weather factor so they are never incorrectly scaled when placed in a mixed WIND/SOLAR plant. Only `POWER_GENERATION` and the base plant rating scale with plant-type weather. `WIND_TURBINE` units always scale by current wind percentage regardless of plant type.
- `CompanyEconomyCalculator` calculates per-tick labor and energy-auxiliary costs for all new unit types.
- `BuildingPowerPlantPanel` shows a live **city power status** section (supply vs demand, reserve MW, BALANCED/CONSTRAINED/CRITICAL badge, contextual hint) and expanded unit guide (2 → 7 cards).
- All 7 unit type labels and descriptions localized in English, Slovak, and German.
- 10 new backend integration tests including a mixed-unit weather-scaling correctness test.

**Shipped (dispatch controls, fuel-flow chain, P&L visibility):**
- `FuelProcurementPhase` (tick-engine phase order 9): COAL/GAS plants procure fuel each tick via `FUEL_PURCHASE` units. Cost is debited from the building's bank account and recorded as `LedgerCategory.FuelCost`. Procurement scales with `DispatchTargetPercent`; if funds are insufficient, a partial fill is made (graceful degradation, not zero).
- `PowerPlantOutputCalculator` updated: thermal plants now draw output from `FuelReserveMwh` — `FUEL_PURCHASE` units fill the reserve first, `ENERGY_PRODUCING` units consume the remainder. Non-thermal plants retain their flat-boost behaviour. Total output is scaled by `DispatchTargetPercent` after all unit contributions.
- New `Building` fields: `DispatchTargetPercent` (int 0–100, default 100) and `FuelReserveMwh` (decimal, default 0). EF migration `20260428_AddPowerPlantDispatchAndFuelReserve` included.
- `setPlantDispatch` GraphQL mutation: validates 0–100% range, requires authenticated company ownership.
- `PowerPlantAnalytics` query now returns `fuelCostTotal` sourced from `FuelCost` ledger entries.
- New `GameConstants`: `FuelCostPerMwhBase`, `FuelPurchaseBoostMwPerLevel`, `FuelReserveCapacityPerUnitLevel`, `IsThermalPlant()`.
- Frontend: dispatch slider (0–100%) with live badge in `BuildingPowerPlantPanel`; fuel reserve status bar (thermal plants only); fuel costs metric in the P&L summary grid (5-metric layout for thermal, 4 for non-thermal); P&L bar chart updated to include fuel costs in the cost bar.
- i18n: `dispatch.*`, `fuelReserve.*`, and `analytics.fuelCosts` keys for en/sk/de.
- 7 new backend integration tests + 4 new Playwright E2E tests.

**Shipped (this increment — multi-fuel economics, reserve capacity dashboard, grid-linking visualization):**
- **Multi-fuel cost differentiation**: GAS plants now pay `FuelCostPerMwhBase × GasFuelCostMultiplier` (1.2×) per MWh — 20% more than COAL. `GameConstants.GasFuelCostMultiplier = 1.2` and `FuelCostPerMwhForPlantType()` apply the multiplier in `FuelProcurementPhase`. Both constants exposed via the new `fuelCostPerMwhEur` field on `PowerPlantAnalytics`.
- **Reserve capacity analytics**: `PowerPlantAnalytics` now returns `maxFuelReserveMwh`, `fuelReservePercent` (0–100 integer), `fuelPurchaseCapacityMwhPerTick`, `energyProducingCapacityMw`, `fuelConstrainedOutputMw`, `fuelTypeLabel`, and `fuelCostPerMwhEur` — computed from installed `FUEL_PURCHASE` and `ENERGY_PRODUCING` units against current `FuelReserveMwh`.
- **Constrained-output calculation**: `fuelConstrainedOutputMw = max(0, energyProducingCapacityMw − currentReserve)` gives the player an instant answer to "how much output am I losing because I need more fuel?"
- **`BuildingPowerPlantPanel` richer reserve UI**: thermal plant fuel reserve section upgraded to: (1) fuel type badge (🟠 Coal / 🔵 Natural Gas) with economics tooltip; (2) color-coded capacity progress bar (green ≥50%, yellow 20–49%, red <20%); (3) fill percent label and procurement rate; (4) constrained-output warning (red alert panel) when reserve is too low to feed all `ENERGY_PRODUCING` units; (5) guidance when no FP/EP units are installed; (6) **grid link chain** (⛽ Fuel Procurement → 🔥 Energy Producer → ⚡ City Grid) showing per-node capacity numbers; (7) GAS premium note explaining the 20% cost premium and tradeoff.
- **i18n**: 20 new keys added in `powerPlant.fuelReserve.*` for en/sk/de.
- **5 new backend tests**: `PowerPlantAnalytics_ReturnsReserveCapacityFields`, `PowerPlantAnalytics_FuelConstrainedOutput_WhenReserveLow`, `FuelProcurement_GasPlant_CostsMoreThanCoal`, `PowerPlantAnalytics_GasPlant_ReturnsFuelTypeLabel`, and one additional capacity constraint test.
- **5 new Playwright E2E tests**: capacity bar visibility, constrained-output warning, grid link chain nodes, GAS badge and premium note, no-unit guidance.

**Shipped (test coverage hardening — reserve lifecycle, nuclear non-thermal, dispatch P&L proof):**
- **4 additional backend integration tests**: `PowerPlantAnalytics_NuclearPlant_ReturnsEmptyFuelFields` (proves non-thermal plants return zero fuel fields so frontend hides the fuel panel); `FuelReserve_PreSeededReserve_MaintainsStableLevelOverMultipleTicks` (proves procurement and consumption are in balance each tick, with fuel cost entries confirming procurement ran); `DispatchChange_50Pct_HalvesFuelCostAndReducesSurplusIncome` (proves halving dispatch halves fuel cost AND reduces surplus income — directly validates the "dispatch alters output or profitability" scenario); `PowerPlantAnalytics_WhenReserveIsFull_ConstrainedOutputIsZero` (proves fuelConstrainedOutputMw returns 0 when reserve equals max capacity).
- **7 additional Playwright E2E tests**: green reserve bar (≥50%), red reserve bar (<20%), no-unit guidance empty state, dispatch badge color (yellow 40–79%, green ≥80%), 5-metric P&L grid for thermal plants (Fuel Costs visible), 4-metric P&L grid for non-thermal WIND plant (no Fuel Costs), metric label correctness smoke test.

**Shipped (this increment — advanced grid-link flow visualization):**
- **Live flow pulse on active links**: horizontal and vertical link connectors now carry a `live` CSS class when either adjacent unit had real inventory movement last tick. Active + live links pulse with a subtle keyframe animation so players can immediately see which connections have actual material flowing through them versus which are just configured but idle.
- **Selection path highlighting**: clicking any unit cell in the active grid highlights all link connectors leading directly out of (or into) that cell with a `selected-path` class, giving them a brighter primary-colored border. Adjacent cells that are linked to the selected cell also get a `connected` border highlight so the player can trace the full chain at a glance.
- **Flow hint tooltips**: every horizontal and vertical link connector now carries a native `title` attribute with a plain-language description of the flow path, for example "Wood: Purchase → Manufacturing (active last tick)" or "Wooden Chair: Manufacturing → Storage (no recent flow)". The hint adapts to the link direction (forward / backward / bidirectional), shows the configured item name where available, and uses "no recent flow" when no inventory was seen moving last tick.
- **Localized copy**: three new i18n key groups added in `buildingDetail.linkFlow*` for en, sk, and de.
- **4 new Playwright E2E tests**: live class on link with real flow, no live class when flow is absent, selected-path class appears on adjacent link when cell is clicked, title tooltip contains correct unit-type labels and arrow symbol.

- [x]  Advanced grid linking — bidirectional unit-to-unit flow arrows in the building grid editor

### Units

- [x]  In the grid show the picture of the product instead of cell-item-avatar

#### B2B sales unit

- [x]  The product selection is not localized
- [x]  Make the sale visibility default to be Group

#### Public sales unit

- [x]  The product selection is not localized
- [x]  Set the min price to be the city average price for the product
- [x]  Show more info about the product price when editing the sales unit. At the moment person does not know what price he should set for the public sales. The game must be fun to play it, and players should be well informed about decisions they are making.
- [x]  Public sales slowly increases the brand awareness for the company, product category and product. If the quality of the product is lower then the city average, the brand will slowly decline. If the quality is higher then the city average or if the company is the only seller of the product in the city, the brand is slowly increasing. The marketing of the units is much more efficient way to improve the brand, but without the marketing if the company invests to R&D and has better products then competition, their products should be more demanding.

**Status: 100% complete** (April 2026)

**Shipped (April 2026 — unit grid imagery and B2B improvements):**
- Unit/building grid cells now display the real product emoji image (from `getProductImageUrl`) instead of the generic monogram avatar whenever a product type is configured or held in inventory. Resource tiles still use the stored image URL. Fallback monogram is retained only when no product/resource is resolved.
- B2B sales product picker is fully localized: product names and industry labels are rendered using the locale-aware `getLocalizedProductName` / `getLocalizedIndustry` helpers in all three supported locales (en, sk, de). The search filter also matches localized names.
- New B2B sales units default to `GROUP` sale visibility instead of null/none, reducing misconfiguration risk for players who sell to business partners. The starter factory layout preset also uses GROUP as the default.

**Shipped (April 2026):** City-aware pricing guidance panel in the public sales unit editor shows the city market reference price (product base price × FX rate), a below/at/above-market badge with contextual hints, and a brand momentum tip. The minimum price input is now bounded to the city average price. Passive brand awareness mechanics in PublicSalesPhase: superior-quality sellers and sole-city sellers gain small awareness increments each tick; inferior-quality sellers see slow awareness decay. All three backend tests (gain, decay, only-seller) pass. `cityAveragePrice` added to `PublicSalesAnalytics` GraphQL type.

### Audits (0% complete)

- [ ] In root directory create audits folder, and every week do the audit of the security. List all potential risks and create the action plan to resolve them. The main focus should be on question: Can one player gain unfair advantege of another player by executing an api call or exploting some unfair game mechanics?

### Media house (100% complete)

**Shipped in this increment:**
- ✅ Buying a Media house no longer fails with an empty `mediaType` validation error — the buy-building and city-map flows both require media type selection before purchase.
- ✅ Media type selection (NEWSPAPER ×1.0, RADIO ×1.5, TV ×2.0) is presented clearly in both the buy-building flow and the city-map purchase panel before a lot is purchased.
- ✅ Selected `mediaType` is persisted by the backend `purchaseLot` mutation and exposed in GraphQL `building { mediaType }` responses.
- ✅ Purchased media houses render as single-unit specialized properties — the factory-style 4×4 unit grid is now hidden for MEDIA_HOUSE buildings (matching the same treatment as APARTMENT and COMMERCIAL).
- ✅ The building detail page shows the dedicated Media House Management panel (content value, content budget, city competitor ranking, effectiveness multiplier).
- ✅ Backend integration tests cover `purchaseLot` with valid mediaType, missing mediaType, and all three media types (NEWSPAPER, RADIO, TV).
- ✅ E2E tests verify the media house detail renders without the factory grid and shows the management panel.
- ✅ Strategic purchase guidance added to buy-building flow: three expandable cards explain NEWSPAPER vs RADIO vs TV trade-offs, strategic moat rationale, and when to choose each channel type.
- ✅ Media house upgrade path implemented: `upgradeMediaHouse` mutation increases building level (1→5), each level improves content conversion efficiency (50%→83%), costs are FX-adjusted to city currency, and a ledger entry is recorded.
- ✅ Upgrade UI in building detail panel shows efficiency ladder (levels 1-5 with % display), estimated cost and duration, and a success/error banner after upgrading.
- ✅ Brand-impact analytics panel added to building detail: shows active advertiser count, average and total advertising income, per-advertiser brand awareness / marketing quality bars, income history mini-chart (last 30 ticks), and a combined effective multiplier row.
- ✅ `getMediaHouseAnalytics` GraphQL query exposes upgrade cost, next-level efficiency, advertiser brand effects, income history, and strategy rating (EARLY_STAGE / GROWING / COMPETITIVE / DOMINANT).
- ✅ Combined effective multiplier visible in the Effectiveness section (channel reach × content ranking bonus).
- ✅ Backend tests cover `upgradeMediaHouse` validation (wrong type, government-owned, max level), analytics query structure, and that efficiency improves with each level.
- ✅ `MediaHouseIncome` is now surfaced as a dedicated first-class ledger category in the company ledger UI with full drill-down support. Players can see how much their media houses earned, drill into per-tick entries, and link through to the source building. Media house income is included in net income, cash from operations, and taxable income calculations. All three locales (en/sk/de) include polished category labels.

### Mining (80% complete)

**Shipped in this increment:**
- ✅ Mining property purchase UI in BuyBuildingView now shows the raw material present on each relevant land, with resource type badge on lot cards.
- ✅ Resource quality and quantity are displayed in a Mining Deposit Investment Summary panel when a mine lot is selected and MINE building type is chosen.
- ✅ Pricing breakdown shows base land value plus resource deposit premium, with a "+ resource" badge when price exceeds appraised land value.
- ✅ Mining land purchase prices scale to $20M–$200M equivalent range, driven by deposit quality, quantity, and global market value — verified by backend tests.
- ✅ Invalid mine placement (MINE on a lot without a resource deposit) is now blocked with `MINE_REQUIRES_RESOURCE_DEPOSIT` error code.
- ✅ Displayed local-currency prices match backend calculations and GraphQL/API responses.
- ✅ Backend and E2E tests cover resource display, pricing thresholds, and invalid-placement rules.

- [ ] Make sure the prices for the purchase of the land is very expensive ~ $20M to $200M depending on the quality of the resource and the amount of resource there is available to be mined.

### R&D Building (65% complete)

**Shipped in this increment:**
- ✅ Fixed CATEGORY scope validation bug: BRAND_QUALITY units with CATEGORY scope can now be configured using a direct `industryCategory` field (e.g. "FURNITURE") without requiring a `productTypeId`. The old validation incorrectly required a product type for both CATEGORY and PRODUCT scopes.
- ✅ Public-sales competitiveness now blends product, category, and company brand contributions coherently via `FindCombinedBrand`: product (full weight), category (60%), company (30%) using additive diminishing-returns formula — no double-counting.
- ✅ R&D operating costs raised to be materially impactful: PRODUCT_QUALITY and BRAND_QUALITY labor hours increased from 0.55 → 2.0 and energy from 0.09 → 0.22, making R&D roughly 3× more expensive than a marketing unit in the same city.
- ✅ Ledger entries for R&D units now use clear labels: "R&D Salary: Product Quality Research" and "R&D Salary: Brand Quality Research" so players can directly connect research investment with financial consequences in the ledger.
- ✅ 5 new backend tests covering: category scope validation via GraphQL, R&D cost exceeding marketing cost by ≥3×, R&D ledger label correctness, and combined brand (product+category) contributing more public sales than product-only brand.

**Remaining:**
- [ ] When selecting the product, make sure to show at the top the products the company is currently producing.

### Appartment and commercial buildings (90% complete)

**Shipped in this increment:**
- ✅ Grid layout hidden for APARTMENT and COMMERCIAL buildings — these now present as single-unit properties without the factory-style unit grid.
- ✅ Market Rate Guidance panel added to the property UI: shows city reference rate, location-adjusted market rate (city rate × PopulationIndex), your current rent, price position label (Very Attractive / Good / At Market / Above Market / Overpriced), % vs market, and occupancy expectations per price tier.
- ✅ Market rate hint shown inline inside the rent-setting dialog so players see the benchmark before entering a new value.
- ✅ Occupancy caps implemented in backend RentPhase: overpriced (>+10%) → 50% floor; at +10% → max 90%; below 60% of market → max 100%; linear interpolation between 60%–110%.
- ✅ Location-adjusted market rate: compares rent against `city.AverageRentPerSqm × lot.PopulationIndex`, not just the raw city average.
- ✅ Constant operating costs: each tick deducts `pricePerSqm × area × 0.75` (PROPERTY_MAINTENANCE ledger entry). Building breaks even at 75% occupancy, profitable above it.
- ✅ New GraphQL fields on Building: `cityReferenceRentPerSqm`, `adjustedMarketRentPerSqm`, `populationIndex`.
- ✅ 19 backend tests covering all occupancy/rent rules, price zones, ledger entries, and breakeven profitability.
- ✅ Rent reference rate chart added to apartment and commercial building detail screens. The SVG chart shows the full occupancy curve (0–100%), colored pricing zones (Very Attractive / Good / Above Market / Overpriced), a dashed breakeven line at 75%, and interactive markers for the city reference rate, the location-adjusted market rate, and the player's current rent. Apartment buildings show the apartment-focused chart; commercial buildings show the commercial-focused chart. Currency formatting uses the correct city currency.

**Remaining:**
- [ ] When backend is restarted it must store all news from the changelog csv to the game server database. At the moment i see only few news and changelog csv is not imported.
- [x] Create weekly and monthly report of the most used products and its profits from the manufacturing up to the sales in and do it for every city. Create separate categories in the news room for the weekly and monthly reports. **100% complete** — Weekly (`WEEKLY`) and monthly (`MONTHLY`) city market reports are now auto-generated at tick boundaries (every 168 ticks for weekly, every 720 ticks for monthly) by the new `MarketReportPhase`. Each report aggregates `PublicSalesRecord` data per city, ranks up to 10 products by revenue with gross margin %, seller count, and average price, generates bilingual HTML content (EN/SK/DE) with a styled table layout, persists as `CityMarketReport` rows (idempotent), and is published to the MasterApi newsroom as `MARKET_REPORT` entries by `MarketReportPublisherHostedService`. The frontend newsroom adds a dedicated 📊 Market Reports filter tab with teal-themed pill and special card styling for market report entries. 10 backend integration tests cover generation, idempotency, localization, ordering, DB round-trip, and GraphQL query/filter.

## FX Exahcnge

Each city is located in physical country which has the currency - CZK for Prague, EUR for Vienna or USD for New York for example.

The FX exchange is visible in the main menu. When user comes to the FX exchange he picks the currency from which he wants to swap from, then picks the currency to which he wants to swap to, and enters the amount. The system will generate him the quote and if user confirms, he makes the trade. Quote will show also the 1% swapping fee.

Besides the cities fx currencies, the FX exchange will support the gold token.

For swaping gold token special rules applies - requires a liquidity in ingame AMM. It will use traditional AMM functions `fx currency` * `gold` = `constant`. Each player can create a liquidity pool or fund to the existing liquidity pool, and he can see his liquidity pools positions. To create a liquidity pool, person needs to pick the currency, add the fx currency amount and the gold amount, and he creates a liquidity position. Marketize this that liquidity providers earn 1% AMM fee rewards. User must be able to remove his liquidity from the pool. User cannot use the blocked resources in the amm pool.

## Gold token

Gold token is special in game currency which represent 1 gram of gold in real world. The gold token amount is stored at the user's account in the master server.

Server global administrator can manage gold token funds on player's account in the master frontend global administration.

## Multiple Game Servers

The master website is product pitching website where users can find in game documentation and list of active game servers. Existing users who authenticated can see their pro subscription on they can purchase prolonging their pro subscription.

Master API has its own database and handles the subscription management.

## Authorization

When player creates the account, he creates it at the master server. When user requests the token, he does it against the master server. The token is usable against every game server and master server.

## Buildings

Every building must be placed on existing land. Land can be purchased on map and it has value which can be increased in time, has gps coordinates, and has attributes like population index which serves for the sale unit sales calculation.

Player can buy the buildings:
- mines, 
- factories, 
- sales shops, 
- research and development buildings,
- appartment buildings, 
- commercial buildings,
- media houses - Newspaper, Radio, TV, 
- banks, 
- exchanges
- Power plants - coal, gas, nuclear, solar, wind.

Building can be set for sale and other players can buy the building. Each building requires power.

Mines, factories, sales shops, and r&d buildings will have configuration option with 4x4 units grid. Grid units can be matched with units next to each other - link will be active or inactive. Diagonal links can be also active or inactive or active in both diagonals.

Mines unit grid allows:
- Mining operation unit
- Storage unit
- B2B sales unit

Factories unit grid allows:
- Purchase unit
- Manufacturing unit
- Branding unit
- Storage unit
- B2B sales unit

Sales shops unit grid allows:
- Purchase unit
- Marketing unit
- Storage unit
- Public sales unit

Research and development building allows units:
- Product quality
- Marketing brand quality

Appartment buildings and commercial buildings allows to set the price per m^2. After the change the price is applied after 1 day. The appartment building has occupancy and fixed size. If price is higher then average in the area, the occupancy percentage goes down and vice versa. It is more difficult to reach full occupancy.

Media houses improve brand quality.

Banks allows to borrow to player money. Player can configure the interest rate in the bank.

## Company settings

Special page will be dedicated to the company settings.

The name of the company can be set by the player. Only the owner of the company can change the company name.

In the company settings, player can choose the salaries level for each city. This will directly affect the costs for running the units.

With bigger company there will be higher administration overhead. Show this information in the company profile.

Administration overhead 50% is the maximum for 2 year old company with the highest asset equity.

Company dividends can be set in the company settings page as well. Acting CEO of the company suggests change and the shareholders approves or reject any change. The dividend defaults to 20%.

## Land

Game engine ensures there is always at least 10 available lands available for each building type in each city. Buildings can be purchased only on existing lands.

Each land has properties:

### gps coordinates

The logicics costs between buildings is calculated when resources moves. The real distance between buildings is calculated.

GPS coordinates cannot change. Only game engine is allowed to modify this property.

### Population index

Population index is information on how close to the city center the building is located, with respect the randomness and respect of closeby residential and commercial occupancy and city overall population.

Poplulation index changes over time. Only game engine is allowed to modify this property.

The population index is the input to the public sales unit function. Products are sold better in more populated areas.

### Raw material

One land can contain only one raw material type. For each raw material type there is always at least 2 available lands available. Mines can be built only on matching available raw material resource.

The price to purchase the land includes also the base price for the raw meterial. The base price is evaluated by the qality and quantity and the base price of the resource in the global market in that city.

### Raw material quality

If land contains raw material the raw material quality must be defined.

### Raw material quantity

Quantity of the raw material at the land is consumable by the mining process.

## Ranking

Each player is ranked by his total wealth. Players can start multiple companies. Company pays out the dividends.

## Units configuration

### Mining operation unit

Produces raw materials. Depending on the resource type on the mine, it can produce different raw materials such as coal, iron, gold, chemical minerals, wood etc. The production rate can be increased by upgrading the unit. Storage capacity is defined by the level of the building and is fully filled on tick.

Each raw material has different mining unit. It will differ in the capacity of the production, for example mining mining unit for coal will have base capacity 0.1 ton per tick and wood gathering unit will have capacity 1 log per tick. It is possible to create the raw material mine or lumber jack only if the resources are available in the map. Different locations on map will have different resource quality. When purchasing land for the building calculate the land price with accordance to the resource quality and quantity. Resources at the land are consumable - when fully consumed the mining unit will not gather more resources. Also there is diminishing return factor - when there is a lot of resources it is easier to mine it. When there is small amount of resources the efficiency of mining decreases and mining operation unit will not fully fill in the storage capacity in a tick.

### Storage unit

Allows to store raw materials or finished products. The storage capacity can be increased by upgrading the unit.

### B2B sales unit

allows to sell raw materials at the place, or ship it to the exchange warehouse. Sell onsite can be public, limitted to the company or limitted to users companies. Storage size at the sales can be increased by upgrading the unit. User can set the minimum price to be received. Unit holds max storage capacity resources.

### Purchase unit

Allows to purchase products from the exchange warehouse or from other players. The purchase capacity can be increased by upgrading the unit. The maximum purchase price can be set by the player. The purchase can be locked for specific vendor, specific exchange or can be set to buy at the optimal price. The minimum product quality can be set by the player. The purchase unit can be set to buy raw materials or finished products. Unit holds max storage capacity resources.

By default make sure the purchase is the optimal price.

### Manufacturing unit

allows to manufacture products from raw materials linked to the manufacturing unit. The manufacturing speed and storage size can be increased by upgrading the unit. The player can set the product type to be manufactured. The quality of manufactured product depends on the quality of raw materials and the quality of the researched product. The quality can be increased by upgrading the unit. Unit holds max storage capacity resources for each resource.

The game engine does not move the input resources from the manufacturing unit to output unit.

The capacity in manufacturing unit for specific input resource must be lower then 1/(input resource count for product plus output resource count) % so that the manufacturing storage capacity is not halted by one input product.

The manufacturing takes one tick to process. It converts the input resources to output resources. The costs for the unit such as labor or energy costs are compounded to the sourcing costs of the output product.

### Branding unit

allows to set the brand of the products manufactured in the factory. The brand can be product specific, product category specific or company specific. This unit is not upgradable. Brand quality affects the sales of the products. Higher brand awareness and brand quality means more sales. Unit holds max storage capacity resources for each resource.

### Marketing unit

allows to set budget for the linked products. The money is paid to the selected media house. Marketing unit increases the product's brand awareness. This unit does not have any storage capacity.

### Public sales unit

Allows to sell products directly to general public. The sales capacity can be increased by upgrading the unit. The player can set the minimum price for the products sold in this unit. The sales can be limited to specific company or open to all players. Unit holds max storage capacity of the resource.

In the details is shown the pie chart of the player market share, other players market shares and non player's market share, product elasticity index, history of the sale price, the chart showing revenue earned in each tick in last 100 ticks.

Quantity sold to public changes every tick with the saturation of the market, with branding or product quality, city population, property population index, the game currency collected by salaries in past 10 ticks and any other variables highlighting the elasticity, oversupply or scarcity. Quality of the public sales is one of the main factors for players having fun in the game.

### Product quality

Allows to select a product which will increment the company's internal knowledge how to produce the product. When doing reserarch into the products the the manufacturing quality will be improved in time.

### Marketing brand quality

Select what type of marketing to research - The global company branding, industry type of branding or product specific branding. When industry type is selected player also select which industry products brand he wants to improve. When product specific is selected player selects the specific product. This does not increase the brand quality directly, but increases the efficiency on how marketing unit is increasing the brand efficiency.

## Unit display and design

On big display the grid is shown on half of the page and unit details is showned in the other side.

When unit has configured resource, make sure to display this resource in the grid at the unit including picture. Also show visually the capacity how much much resource is stored in the unit.

Show the most important details in the grid - for example the price to sell the product.

Links between units are directional. Make sure to show the arrow between the units if they are active.

When configuring the building and buying the new unit make sure to show user the price how much the unit costs and substract the costs when the building configuration is applied at the backend.

For every resource held in the unit make sure to show the value of the resource.

Show costs associated with the unit and next tick payment for the labor costs.

Every unit with resources shows chart of historic movement of the resource. The manufacturing unit shows clearly how many of each resources were consumed and how much was produced when the resource is selected.

## Unit price

Each unit costs money to build it. 

Also each unit employs labor depending on the unit level. Labor costs are paid 

## Ledger

Accounting ledger allows to see the income statement, cash flow statment and balance sheet. Items in the statement can be opened and exact details on each item is visible. For example when the long-term tangible assets from balance sheet is opened, the list of all buildings is visible. When income is clicked the each sales item from each unit is visible and person can access the building. When costs are clicked every costs such as the property purchase, units upgrades, purchasing unit purchases, marketing costs or others are clickable to get to the source.

Ledger information about the game year and information when income tax is going to be paid is displayed in the ledger.

Ledger is reset in new tax year, but player can see the old years including the details in the ledger history.

## Timing & Game engine

Game is played in ticks. One game day is 24 ticks. One game year is 8760 ticks. Game time is visible in the game. The start time is year 2000. Show game time in the header.

Each change - new building, change of the building unit plan, or upgrade of the unit takes specific number of ticks to be executed.

Backend handles tick based resolution of actions. Tick system runs in loop every N seconds configured in the app and defaults to 10 seconds. Tick system must be very efficient and be able to handle 1000 concurrent users and 20000 buildings and 500000 units to be handled in less then one second.

Tick base system handles mainly
- Sale of the resources to the public
- Paying rent
- Moving resources between storage capacity of the units if the move is possible
- Mining operations
- Purchasing resources at the purchase units
- Marketing - payment to media houses and brand improvements
- Research and developemnt updates
- Handling upgrade of the units and changes in the unit links
- New building availability 
- Ranking recalculation
- Taxes

Frontend integration to tick resolution must be seamless. User should see next tick calculation visible on the website and should see estimate in real time when he is waiting for some action for example the wait for the building.

Tick base system handles units from the end directions and moves single resources only once. Sales buildings are processed before the factories. If there is purchase unit, manufacturing unit, storage unit and b2b sales unit, first it process movement of available resources to fill in the b2b sales unit from storage, next move resources from manufacturing unit to storage and then move resources from purchase to manufacturing. This means that storage and sales will always have not empty resources if the manufacturing and purchasing is working properly.

For users always show the game time while in the title of the element will be the tick number. This way users will receive better look & fell while they still can see the exact tick events.

## Building modification

Building unit configuration can be modified. User can edit the building and prepare all building modifications on frontend. When building is done being modified by user, user confirms his selection. Each unit can have different suspend time. For example upgrade unit from level 1 to 2 may take 10 ticks. Upgrade from level 2 to 3 may take 100 ticks. Upgrade from level 3 to 4 may take 1000 ticks. Change in the links between the units takes one tick to apply. Each item the unit or link acts separately. User cannot change the building attributes directly. Everything must be scheduled by the tick resolve engine.

When unit is being modified user can still change it. For example when user upgrades the unit and it will take 100 ticks to process, when user cancel it revert the action back in 10% of ticks.

## The onboarding 

Onboarding process:
1. User is given $200000 to his personal bank account and he picks the game player name
2. IPO Process - User transfers $50k from his personal bank account to the business bank account and has decision how much money he wants to raise - $800 000, $600000, or $400 000 varying his own shares to be 25% or 33% or 50% in the company. User picks the company name.
3. Player selects the industry type they want to start with. The Furniture, Food processing, or Healthcare.
4. Player selects the product he wants to produce - Each starting industry allows 3 basic products to be produced.
5. Then they pick the location of their first factory. This will set the factory layout for them and user pays for all costs associated with it - the property as well company layout (show costs analysis before the purchase). Wizard will show them important areas on the screen like which bank account pays, the current bank balance, the price configuration or public sales configuration.
6. Next the player buys his first sales shop and configures it to set the sales price to public. User pays for the land and sales shop unit layout from the selected company/building bank account - make sure the user has clear information about this.
7. The player is shown that the time goes on and he makes the profit from his business.
8. User is asked to create the user account.

Do not require authentication for new not authenticated users. Do not store the progress for these users to the backend, but make sure to show them they bought the buildings they setup the resources chain and they made some profit. After that ask them to log in to save their progress. If there is error such as the building was meanwhile purchased by someone else or profit is too big make sure to create their profile with the name they chosed and start the wizzard again with the authenticated user and this time save everything.

## Stock exchange

There is one global stock exchange where all company shares are traded. The share price is calculated as the sum of all equities of the company (including land, units, warehouse stocks, bank-account balances, owned stocks, and other assets) plus profit expectation divided by number of issued stocks.

Profit expectation is complex formula where new companies has this as zero. The formula includes the profit this year, history of prifits in past years and dividends paid.

Player acting for the company or person account can buy shares for any company including its own from public investors. Market bid price is 1% below the share price and offer is 1% above the share price. The buying of the company shares directly by the company is considered as the company buy back and reduces the number of issued shares. Every trade settles between bank accounts and the acting person/company must choose the source or destination bank account for the settlement.

Player acting for the company or person account can sell shares it owns.

When sum of ownerships for person account and all controlled companies in the other company reaches 50%, person can replace the CEO of the company which is considered as the take over and the player will control also this company.

When sum of ownerships for person account and all controlled companies reaches 90%, person can merge this company into another company. This way all assets owned by the company are moved to the new company and the merged company is closed. Taxes for old company are paid on the tick of merge for old company.

In the stock exchange in company details, is list of all shareholders and the pie chart.

**Status: 75% complete** (April 2026)

### What was delivered
- Global stock exchange UI with company listings, share prices, bid/ask spread, shareholder tables, and pie charts.
- Buy and sell share trading with person account and company account switching.
- Personal account ledger showing portfolio holdings, available bank-account buying power, tax reserve, and dividend history.
- Trading controls redesigned using CSS grid for precise vertical alignment across all viewport sizes; input and Buy/Sell buttons share the same grid row guaranteeing identical baseline.
- Responsive layout: labels hidden on mobile (aria-label covers accessibility), input spans full width, buttons collapse to side-by-side pair.
- Loading, disabled, validation-error, and success/error feedback states all implemented.
- Personal tax reserve lifecycle: accumulation on share sell, settlement at year-end TaxPhase.
- 58 E2E tests covering buy/sell flows, portfolio, dividends, personal ledger, alignment, and authentication states.

### What remains
- Takeover trigger when combined ownership reaches 50%.
- Company merge when combined ownership reaches 90%.
- Share buyback reducing issued share count.

## Account switching

Player can switch between his person account and any company he controls.

Game administrators can switch to any player account. In the player account they can switch to any of the player controlled person or company accounts.

In the top menu player can switch between person account or companies account. In the top navigation is menu, toswitch player's view to any account he controls. In this view the selected company view is used so if person controls more than one company he can act for different companies in this manner, for example he can see the accounting for the other company or he can build buildings for the selected company. Also personal account is selectable there. In such case the player cannot build buildings, but he can start new company.

## Person account

In the onboarding the player picks the game player name. This is the person account. At the start he owns certain amount of company shares the player creates. The ledger info for the player account is customized to person view.

Person cannot own land or buildings and does not pay tax. He can only own bank account balances or shares in the companies. Person account income is the sale of shares and dividends.

Player can switch to person view so that he can trade the stocks.

## City Global Exchanges

In each city is one in game global exchange which serves as the hub between connecting the cities. Global Exchange acts never ending resource sale for every resource. Each city has different resource pricing and quality at the global exchange.

## Transit costs

When resource is sent between one unit to another (sale to purchase or exchange to purchase or b2b sale to exchange) the transit costs are calculated. The transit costs must be visible in the purchase unit when selecting the resource.

Transit costs must never be zero. Every transit even between the player's buildings costs shipping money. Shipping costs are determined by the geo location distance - each building has the gps coordinates and distance between two gps coordinates can be calculated. Make sure that different products has different weight for example so the shipping costs between one unit of medicine will be different to one unit of bed.


Shipping costs are visible in the company ledger.

Game aggregated shipping costs are visible in the administrator dashboard, clickable and then overview of the shipping costs per company is displayed.

## Taxes

At specific tick rounds the taxes are calculated.

## Encyclopedy 

All combination of products are visible in the manufacturing encyclopedy which serves as in game documentation.

When user clicks on the resource he can see at the same screen without scrolling all manufacturable resources associated with it.

Make resource detail a separate view from the encyclopedia entry. 

Encyclopedy entry is the list of all resources with the search field. 

The resource detail consists of resource description, picture, list of all resources it is used in input or output and the manufacturing details.

Every resource must have unique picture.

## Chat

In game chat will be possible

## Game administrators

Game administrators have a dashboard where they can see all critical issues in the game like inflow of money, highlighting users which may be doing multiaccount gaming where they boost one of the account.

Game administrators can switch person as invisible - In this mode the person can see his chat messages, but others do not see them. 

Game administrators can do impersonalization to the player's view. In this mode they can do anything on behalf of the player or player's person account or any of the player's company. Make sure the logs handle this issue and show the game administrator who is acting, user on behalf of which the admin is acting and person or company account on behalf of which is the admin acting.

Game administrators can publish newspaper or modify the latest changelog. Allow rich html editor for the news editing and allow multi language support before the news are publish.

There are roles in the game which can be assigned to any user account. The root administrator can assign or remove the global game administrator role and local game administrator role. The user with global game administrator role can access every game admin dashboard, and do game administrator actions. Local game administor can manage only single game instance.

Game administration is managed in the master api, but local game administrator role can be managed at the game server.

List of the root game administrators is managed by the master api configuration.

## Newspaper and changelog

The master api database holds the changelog and newspaper. Admins can publish the news for directing users or report some progress.

With every change the changelog must be updated. The changelog is visible in the news section in every game.

Game administrators can edit any changelog or news record in any localization.

Track if user did read the news, if not show in the navbar number of unread messages.

## Media house

Media building has single unit layout and does not show the grid.

The configuration for this single unit is spending level on content per tick.

The quality of the content is determined by accumulated costs spent by the media building. With the upgrade of the building, the content is more efficient. At start 50% of the costs goes to the aggregated content (1-1/2). Next level of building has 66% (1-1/3) efficiency, and so on.

Per tick every media house looses 0.5% of the aggregated content value.

The quality of the content is determined by the comparision of the other media houses. If this media house has highest content, it is ranked at 100% content. If competitive media house has aggregated content value half of the top media house, their content is ranked at 50%. This applies for the same media house in the same category and city. Different categories do not affect each other, so one company may have 100% of the content in city 1 for TV category, other company 100% of the content for Radio category in the same city with different aggregated content, and third company may have 100% content ranking for TV but in the different city.

The content quality ranking determines the speed with which the branding quality is increasing.

## Monetization

Startup pack will be available after user finish with the onboarding. There will be time limited time offer. Startup pack will cost $20 in real money.

Startup pack will include - 3 months of pro subscription and in game currency.  

In pro subscription the players will have more products to manufacture and sell.

Pro subscription will cost $10/month.

## Research & Development
Show the user's used products first in the the R&D unit product quality improvement product selection.

Research quality model is cummulative spending budget model. R&D product research adds the money to the research. For each product define specific base quality model base budget where if user accumulates to the research this amount, and there is not going to be competetive company doing the same research, player will have quality 100%. If two players do research the same product, the player with the highest accumulated research money will be base for all other players. On every tick 0.1% of the research accumulated budget is lost, so if player stops researching the product, in time his research will diminish.

With upgrading the unit to do research, the efficiency to do research improves. At start 50% (1-1/2) of the unit costs are accumulated to the research. Next level improves this to 66% (1-1/3)%, next level to 75% (1-1/4) and so on. While the upgrade is in progress, the player pays half of the costs for the unit.

## Banks

In loan menu if person does not own bank, show him link to buy a bank building. If person already owns a bank, show him the link to his bank building. 

In the loan menu show list of all banks with the current deposit interest rate and lending interest rate - sortable, and filterable.

In bank building, allow people to deposit funds to receive interest from the player, and other players to ask for a loan. Player can issue loan only if he has deposits to the bank.

Bank building does not have any configurable unit, whole bank acts as a single unit.

In the bank, there is a configuration to set the interest to pay to deposit account holders, and interest rate which lenders pay to the player.

When player creates a bank, he must deposit there the base capital of $10000000. This serves as the initial capital to be lended and is counted towards the bank deposits. 

### Deposits

When player opens a third party bank, he can see the current interest rate, and deposit funds there. The deposit is created in the bank and every tick the interest is paid to the depositer from the bank. 

Player can withdraw money from the bank any time, even if bank does not have enough deposits on the account.

Bank owner company can deposit funds to the bank or withdraw money up to the base capital deposit.

### Loans

When player opens a third party bank, he can see the current lending rate and the sum of money available to be lended. 

Sum of available money to be lended is 90% of the current deposits. Bank must preserve 10% deposit to loan ratio.

Company can request loan from any bank which has available deposits.

User can borrow money only for buildings which are not mortgaged. User can pick a building and he can borrow against it a money up to 70% of the property value.

Borrower decide the amount and duration of the loan. When player goes to the bank, he can request a loan for his own duration and the requested amount. He also deposits a building as a collateral. One building can be used only in one loan.

Creating a loan creates a contract between bank and a player which will hold the interest rate even if the bank player changes the lending interest rate. Each contract has a maturity date. User can see each tick payment amount. The calculation is the same as in the real world mortgage payments with difference that the payment is done on every tick. The borrower pays the interest and principal amount.

Borrower can repay any part of the loan any time.

### Central bank

If the bank deposits are negative because depositers has withdrawn money from the bank. Bank borrows money from the central bank. Interest rate for borrowing money from the central bank is variable depending on how many banks borrow money from it. The interest rate fluctuates between 2 to 5% per game year.

If depositers add new money and the bank has loan from the central bank, bank repays with the deposited money the central bank loan.

### Bank building details

When bank owner company is the current player, show the bank profit chart, interest rates chart, other details and composition of the loans.

When other player displayes the bank detail, make sure he see the professional design for making the deposits or asking for a loan.

## Power plants

In powerplant grid allow to build following units:
- Purchasing unit - allows to buy the coal or gas
- Wind turbine unit - produces wind force - Each city has the weather channel with prediciton for next 50 ticks on how much wind is blown - ranges from 0% to 100% but incrementing and decrementing in random manner 2-5% up or down.
- Watter turbine unit - produces watter force - extremly expensive, but produces steady force units
- Storage unit - to users to store the wind force and optimize for steady energy output
- Energy producing unit - consumes the coal or gas, wind force, and produces energy
- Battery unit - Can store the extra energe in peaks and outputs when produciton is not good enough.

Flow of the resources is following:
- Purchasing unit | Wind turbine unit | Watter turbine unit -> Storage unit | Energy producing unit
- Storage unit -> Energy producing unit
- Energy producing unit -> Battery unit

The power plant as a building as a whole has configuration for planned output. If the output is oversupplied, the powerplant do not receive money for the oversupply. If the powerplant is undersupply, it receives the government fines for not generating enough of energy.

Make sure to show the powerplant P&L chart in the building overview.

### City selection

- In the top menu where company is selected add selection also for the city
- In the player dashboard filter only buildings in the selected city
- In the banking page show only banks in the selected city
- In the global exchange remove the city selection and use the selection from the navbar
- In the marketing analytics page show only data related to selected the city

# Technical implementation

Game server frontend is vue.js with source code located at projects/frontend with tailwind styling.

Master server frontend is vue.js with source code located at projects/master-frontend with tailwind styling.

Game server Backend is .NET with graphql engine with data stored in postgresql. Source code is at projects/Api.

Master server Backend is .NET with graphql engine with data stored in postgresql. Source code is at projects/MasterApi.


Deployed to kubernetes.

Players must receive near real time user experience.
