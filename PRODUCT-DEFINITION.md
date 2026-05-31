# Capitalism Game

Create a fun game in the style of Capitalism II. This game is an economic simulation where players can experience price elasticity, resource scarcity, resource oversupply, different competition types, marketing, product quality, and difficulties with scaling up companies.

It will use a real-world map. The game starts in a single city and later expands to more cities.

## Endgame win condition

The game shard ends immediately when any player surpasses the estimated net worth of the #1 richest real-world billionaire (USD benchmark).
After the winner is declared, the server becomes read-only, ticks stop, and a final newsletter with winner details and top rankings is published.
The personal ledger must show a "Race to the Top" benchmark panel so players can track progress toward this objective from the start.

## FX Exchange

Each city is located in a physical country and has a local currency, for example CZK for Prague, EUR for Vienna, or USD for New York.

The FX exchange is visible in the main menu. When a user opens the FX exchange, they pick the source currency, then the destination currency, and enter the amount. The system generates a quote and, if the user confirms, executes the trade. The quote also shows the 1% swap fee.

Besides city FX currencies, the FX exchange supports the gold token.

Swapping the gold token has special rules and requires liquidity in an in-game AMM. It uses the traditional AMM function `fx currency * gold = constant`. Each player can create a liquidity pool or fund an existing pool and see their liquidity positions. To create a liquidity pool, a person picks the currency, adds FX currency and gold amounts, and creates a liquidity position. Market this clearly so liquidity providers understand they earn 1% AMM fee rewards. Users must be able to remove liquidity from the pool. Users cannot use resources blocked inside the AMM pool.

## Gold token

Gold token is a special in-game currency representing 1 gram of real-world gold. The gold token amount is stored in the user's account on the master server.

The server global administrator can manage gold token funds on player accounts in the master-frontend global administration.

Pro subscription activation is gold-paid only:
- Monthly Pro purchase costs `$20` equivalent and is charged as `0.137` grams of tokenized gold.
- Startup pack purchase costs `$40` equivalent and is charged as `0.274` grams of tokenized gold, granting 3 months of Pro.
- Referral-active accounts receive a 10% discount on both Pro purchase paths.

Master frontend gold transfer requirements:
- Provide a deposit page for tokenized gold with both VOI and Algorand source networks.
- Use network-specific asset ids: VOI `302228`, Algorand `1241944285`.
- Generate ARC26 QR payloads for deposit intents to reduce manual transfer mistakes.
- Provide a withdrawal page where users create withdrawal requests; withdrawals are processed manually by administrators and then marked as processed.
- Provide an administrator oversight page listing all gold deposit and withdrawal requests with manual processing controls.


## Email support

Master API sends transactional and weekly emails through Azure Email Communication Services when email is enabled in configuration. Email HTML uses one shared Handlebars layout with English, Slovak, and German copy, and the master account stores each player's preferred locale after login with English as the fallback.

Registration email is sent once per master account and records the URL the player was using when the account was created. Weekly reports are scheduled for Friday noon UTC and summarize active game servers, player ranking context, bounty points collected in the previous week, and recent changelog items.

## Multiple Game Servers

The master website is the product-pitching website where users can find in-game documentation and a list of active game servers. Authenticated users can see their Pro subscription and purchase an extension.

Master API has its own database and handles the subscription management.

### Backup and recovery requirements

- Platform operations must run an automated daily backup pipeline for Kubernetes artifacts and PostgreSQL data.
- Backups must be retained for 7 days minimum.
- Recovery tooling must support restoring Kubernetes manifests first and then loading PostgreSQL dumps so shard restart is fast.
- Backup and restore scripts must be maintained in-repo and documented for operators.

## Authorization

When player creates the account, he creates it at the master server. When user requests the token, he does it against the master server. The token is usable against every game server and master server.

The game supports configuration-driven authentication modes:
- **OIDC-only mode (default)**: Biatec OpenID Connect is the only login method.
- **Hybrid mode (explicitly enabled by configuration)**: native account authentication (`register` and `login`) is enabled alongside OIDC for local development and automated tests.

When OIDC-only mode is active, navigating to `/login` must immediately start the OIDC authorization flow (equivalent to clicking the Google authorization button).

OIDC integration requirements:
- Client flow must validate `state` and `nonce` before accepting callback tokens.
- API and Master API must validate external JWT `iss`, `aud`, signature, and expiry against the Biatec OIDC configuration and JWKS.
- Player provisioning from external claims must use normalized email matching to prevent duplicate-player creation.
- OIDC sessions should schedule periodic renewal before token expiry to avoid hard logout during active use.
- Production environments must use HTTPS for OIDC authority and redirect URIs.

### Bot API authorization

Players can generate API keys bound to their personal account to authorize bot automation.

- Each API key can impersonate only the owner's personal account and owner-controlled companies.
- API keys must not allow controlling foreign companies, bypassing ownership checks, or bypassing gameplay restrictions (including forex and stock authorization rules).
- API key creation, rotation, revocation, and usage audit trail must be available in player settings and administrator tooling.
- Bot console applications must use API-key-based authorization instead of interactive user login.

## Buildings

Every building must be placed on existing land. Land can be purchased on the map and has value that can increase over time, GPS coordinates, and attributes like population index that feed public-sales calculations.

Player can buy the buildings:
- mines, 
- factories, 
- sales shops, 
- research and development buildings,
- apartment buildings, 
- commercial buildings,
- media houses - Newspaper, Radio, TV, 
- banks, 
- exchanges
- Power plants - coal, gas, nuclear, solar, wind.

Building can be set for sale and other players can buy the building. Each building requires power.

Mines, factories, sales shops, and R&D buildings have a configuration option with a 4x4 units grid. Grid units can be linked to adjacent units with active or inactive links. Diagonal links can also be active, inactive, or active in both diagonals.

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

Apartment and commercial buildings allow setting the price per m^2. After a change, price is applied after 1 day. The apartment building has occupancy and fixed size. If price is higher than the area average, occupancy percentage goes down and vice versa. It is more difficult to reach full occupancy.

Media houses improve brand quality.

Banks allow players to borrow money. Players can configure bank interest rates.

## Company settings

Special page will be dedicated to the company settings.

The name of the company can be set by the player. Only the owner of the company can change the company name.

In company settings, players can choose salary levels for each city. This directly affects unit operating costs.

With bigger company there will be higher administration overhead. Show this information in the company profile.

Administration overhead of 50% is the maximum for a 2-year-old company with the highest asset equity.

Company dividends can also be set in company settings. The acting CEO suggests a change and shareholders approve or reject it. The dividend defaults to 20%.

## Land

Game engine ensures there is always at least 10 available lands available for each building type in each city. Buildings can be purchased only on existing lands.

Each land has properties:

### GPS coordinates

Logistics costs between buildings are calculated when resources move. Real distance between buildings is used.

GPS coordinates cannot change. Only game engine is allowed to modify this property.

### Population index

Population index is information on how close to the city center the building is located, with respect the randomness and respect of closeby residential and commercial occupancy and city overall population.

Population index changes over time. Only game engine is allowed to modify this property.

The population index is the input to the public sales unit function. Products are sold better in more populated areas.

### Raw material

One land can contain only one raw material type. For each raw material type there are always at least 2 available lands. Mines can be built only on matching raw-material deposits.

The land purchase price also includes the base price of the raw material. The base price is evaluated by quality and quantity and by the base price of that resource in the global market in that city.

### Raw material quality

If land contains raw material the raw material quality must be defined.

### Raw material quantity

Quantity of the raw material at the land is consumable by the mining process.

## Ranking

Each player is ranked by his total wealth. Players can start multiple companies. Company pays out the dividends.

## Units configuration

### Mining operation unit

Produces raw materials. Depending on the resource type on the mine, it can produce different raw materials such as coal, iron, gold, chemical minerals, wood etc. The production rate can be increased by upgrading the unit. Storage capacity is defined by the level of the building and is fully filled on tick.

Each raw material has a different mining unit. Production capacities differ, for example a coal mining unit may have base capacity 0.1 ton per tick and a wood gathering unit may have capacity 1 log per tick. A raw-material mine or lumberjack operation can only be created if the resource exists on the map. Different map locations have different resource quality. When purchasing land for a building, calculate land price according to resource quality and quantity. Land resources are consumable; when fully consumed, the mining unit cannot gather more. There is also a diminishing-return factor: when there are many resources, mining is easier; when resources are low, efficiency drops and the mining unit may not fill storage each tick.

### Storage unit

Allows to store raw materials or finished products. The storage capacity can be increased by upgrading the unit.

### B2B sales unit

Allows selling raw materials on-site or shipping them to the exchange warehouse. On-site sale can be public, limited to the company, or limited to user companies. Sales storage size can be increased by upgrading the unit. Users can set a minimum price to receive. The unit holds resources up to max storage capacity.

### Purchase unit

Allows to purchase products from the exchange warehouse or from other players. The purchase capacity can be increased by upgrading the unit. The maximum purchase price can be set by the player. The purchase can be locked for specific vendor, specific exchange or can be set to buy at the optimal price. The minimum product quality can be set by the player. The purchase unit can be set to buy raw materials or finished products. Unit holds max storage capacity resources.

By default make sure the purchase is the optimal price.

### Manufacturing unit

Allows manufacturing products from raw materials linked to the manufacturing unit. Manufacturing speed and storage size can be increased by upgrading the unit. Players can set the product type to manufacture. Manufactured-product quality depends on raw-material quality and researched-product quality. Quality can be increased by upgrading the unit. The unit holds resources up to max storage capacity for each input.

The game engine does not move the input resources from the manufacturing unit to output unit.

Capacity in a manufacturing unit for a specific input resource must be lower than 1/(input resource count for product plus output resource count)% so manufacturing storage is not halted by one input resource.

The manufacturing takes one tick to process. It converts the input resources to output resources. The costs for the unit such as labor or energy costs are compounded to the sourcing costs of the output product.

### Branding unit

Allows setting the brand of products manufactured in the factory. The brand can be product-specific, category-specific, or company-specific. This unit is not upgradable. Brand quality affects sales. Higher brand awareness and quality mean more sales. The unit holds resources up to max storage capacity for each resource.

### Marketing unit

Allows setting budget for linked products. Money is paid to the selected media house. The marketing unit increases product brand awareness. This unit has no storage capacity.

### Public sales unit

Allows to sell products directly to general public. The sales capacity can be increased by upgrading the unit. The player can set the minimum price for the products sold in this unit. The sales can be limited to specific company or open to all players. Unit holds max storage capacity of the resource.

Details show a pie chart of player market share, other players' market shares, and non-player market share, plus product elasticity index, sale-price history, and a chart of revenue earned in each tick in the last 100 ticks.

Quantity sold to the public changes every tick with market saturation, branding and product quality, city population, property population index, game currency collected by salaries in the past 10 ticks, and other variables highlighting elasticity, oversupply, or scarcity. Public-sales quality is one of the main factors for player enjoyment.

### Product quality

Allows selecting a product that increments the company's internal knowledge of how to produce it. As research progresses, manufacturing quality improves over time.

### Marketing brand quality

Select what type of marketing to research: global company branding, industry branding, or product-specific branding. When industry branding is selected, player also selects which industry brand to improve. When product-specific branding is selected, player selects a product. This does not increase brand quality directly, but increases the efficiency of the marketing unit.

## Unit display and design

On big displays, grid is shown on half of the page and unit details are shown on the other side.

When a unit has a configured resource, display that resource in the grid at the unit (including image). Also show visually how much resource is stored.

Show the most important details in the grid - for example the price to sell the product.

Links between units are directional. Make sure to show the arrow between the units if they are active.

When configuring a building and buying a new unit, show the unit price and subtract costs when building configuration is applied on the backend.

For every resource held in the unit make sure to show the value of the resource.

Show costs associated with the unit and next tick payment for the labor costs.

Every unit with resources shows a chart of historic resource movement. The manufacturing unit clearly shows how many of each resource were consumed and how much was produced when the resource is selected.

## Unit price

Each unit costs money to build it. 

Also each unit employs labor depending on the unit level. Labor costs are paid 

## Ledger

Accounting ledger allows viewing the income statement, cash flow statement, and balance sheet. Statement items can be opened to show exact details. For example, opening long-term tangible assets from the balance sheet shows the list of all buildings. Clicking income shows each sales item from each unit and allows navigation to the building. Clicking costs shows property purchases, unit upgrades, purchase-unit purchases, marketing costs, and other items with drilldown.

Ledger information about the game year and information when income tax is going to be paid is displayed in the ledger.

Ledger is reset in new tax year, but player can see the old years including the details in the ledger history.

## Timing & Game engine

Game is played in ticks. One game day is 24 ticks. One game year is 8760 ticks. Game time is visible in-game. Start time is year 2000. Show game time in the header.

Each change (new building, building-unit plan change, or unit upgrade) takes a specific number of ticks to execute.

Backend handles tick-based resolution of actions. Tick system runs in a loop every N seconds configured in the app, defaulting to 10 seconds. Tick processing must be efficient enough to handle 1000 concurrent users, 20000 buildings, and 500000 units in less than one second.

Tick-based system mainly handles:
- Sale of the resources to the public
- Paying rent
- Moving resources between storage capacity of the units if the move is possible
- Mining operations
- Purchasing resources at the purchase units
- Marketing - payment to media houses and brand improvements
- Research and development updates
- Handling upgrade of the units and changes in the unit links
- New building availability 
- Ranking recalculation
- Taxes

Frontend integration to tick resolution must be seamless. Users should see next-tick calculation on the website and real-time estimates while waiting for actions such as building completion.

Tick-based system handles units from end directions and moves each resource only once. Sales buildings are processed before factories. If there is a purchase unit, manufacturing unit, storage unit, and B2B sales unit, processing order is: fill B2B sales from storage, move from manufacturing to storage, then move from purchase to manufacturing. This means storage and sales should stay non-empty if manufacturing and purchasing are configured properly.

Always show game time to users, with exact tick number in element title/tooltip. This improves look and feel while preserving exact tick-event visibility.

## Building modification

Building unit configuration can be modified. User can edit building and prepare all modifications on frontend. When building modifications are ready, user confirms selection. Each unit can have different suspension time. For example upgrade from level 1 to 2 may take 10 ticks, level 2 to 3 may take 100 ticks, and level 3 to 4 may take 1000 ticks. Link changes between units take one tick to apply. Each unit or link action is processed separately. Users cannot change building attributes directly. Everything must be scheduled by tick-resolution engine.

When a unit is being modified, user can still change it. For example, if user upgrades a unit and it takes 100 ticks to process, cancelling reverts action in 10% of ticks.

## Onboarding

The onboarding wizard is available without prior authentication. Guest progress is held in browser local storage (not on the backend) so new players can explore the full flow risk-free before committing to an account.

Referral flow requirements in onboarding:
- When user arrives through a referral link, store the referral code in onboarding state and show the referral discount message before first login.
- Do not show the invitation/referral message after user has logged in.
- After referral code is successfully persisted to backend, clear the referral code from game Pinia onboarding state.

Personal account name requirements in onboarding:
- Generate personal account name during IPO step using first-name, middle-name, and last-name generator.
- Do not show onboarding controls for choosing or regenerating the personal account name; keep the generated value in onboarding state for backend submission and guest migration.
- Personal account name is shown in rankings instead of raw OIDC identity name.
- Personal account name can be changed later in player settings, with clear warning not to use real legal identity.
- Personal account name is stored in MasterApi so one person uses same name across all game servers.
- If personal account name already exists for the player in MasterApi, onboarding must preserve it and not overwrite it.

Onboarding steps (city-first order):
1. **City selection** — Player picks their starting city (e.g. Bratislava EUR, Prague CZK, Vienna EUR, Berlin EUR, Warsaw PLN). City choice is first because it fixes starting currency, available lot inventory, salary baseline, and bank-account context for every subsequent step. The selected city is also reflected in the navbar context switcher immediately.
2. **Player name & personal bank account** — The system keeps a generated display name in onboarding state. The player can change it later in player settings. The system grants $200 000 USD to a personal bank account. This is the startup capital for the IPO.
3. **IPO plan** — Player transfers a founder contribution from the personal bank account to the new company bank account, decides how much to raise on the public market ($800 000 / $600 000 / $400 000), and sets founder ownership (25% / 33% / 50%). The system keeps a generated company name in onboarding state and shows it before factory purchase; the company name can be changed later in company settings. After IPO the personal bank account balance is $0.
4. **Industry selection** — Player picks the business category:
   - *Free starter industries:* Furniture (Wood), Food Processing (Grain), Healthcare (Chemical Minerals)
   - *Pro-subscription starter industries:* Electronics (Silicon), Construction (Iron Ore), Pharmaceuticals (Gold), Energy (Coal), Logistics (Cotton). Pro-gated industries show a PRO badge; clicking without subscription shows an upgrade modal.
   Each industry card explains the fantasy, the first product name and price, and a why-choose tagline.
5. **Product selection** — Player picks one of three starter products within the chosen industry. Product card shows base price calibrated to the selected city currency.
6. **Factory lot purchase** — Player picks a lot from the city map or list and purchases the first factory. The wizard auto-configures the starter factory layout (PURCHASE → MANUFACTURING → STORAGE → B2B_SALES) and shows the configured unit chain on the completion screen. Cost analysis is shown before purchase (lot price, remaining balance, pricing guidance).
7. **Sales shop lot purchase** — Player picks a lot and purchases the first sales shop. The wizard auto-configures the shop layout (PURCHASE → PUBLIC_SALES). Payment is from the company bank account; the UI shows current balance clearly.
8. **Save progress** — Guest players are prompted to authenticate via Biatec OIDC or native registration. Biatec OIDC requires Google Drive permission (used to create and manage the wallet file). Granting that permission is a required part of this step. On success, all guest-session progress (city, company, factory, shop configuration) is migrated into a persistent player account. If an error occurs during migration (e.g. lot taken by another player), the player is re-authenticated and the wizard resumes from the interrupted step.

Do not require authentication for steps 1–7. Only step 8 triggers authentication. Do not store guest progress on the backend; keep it in browser localStorage only. If the same player resumes after an interruption post-factory purchase, detect the existing onboarding state server-side and skip directly to the shop purchase step.

## Stock exchange

There is one global stock exchange where all company shares are traded. The share price is calculated as the sum of all equities of the company (including land, units, warehouse stocks, bank-account balances, owned stocks, and other assets) plus profit expectation divided by number of issued stocks.

Profit expectation is a complex formula where new companies start at zero. Formula includes current-year profit, history of profits in past years, and dividends paid.

Player acting for the company or person account can buy shares for any company including its own from public investors. Market bid price is 1% below the share price and offer is 1% above the share price. The buying of the company shares directly by the company is considered as the company buy back and reduces the number of issued shares. Every trade settles between bank accounts and the acting person/company must choose the source or destination bank account for the settlement.

Player acting for the company or person account can sell shares it owns.

When sum of ownership for person account and all controlled companies in another company reaches 50%, player can replace CEO (takeover) and gain control of that company.

When sum of ownerships for person account and all controlled companies reaches 90%, person can merge this company into another company. This way all assets owned by the company are moved to the new company and the merged company is closed. Taxes for old company are paid on the tick of merge for old company. The merge-time tax is clamped to the company's available cash and only the amount actually settled is recorded, so a cash-poor company cannot merge to overdraw or escape tax.

Stock-exchange company details include shareholder list and pie chart.

## Account switching

Player can switch between his person account and any company he controls.

Game administrators can switch to any player account. In the player account they can switch to any of the player controlled person or company accounts.

In top menu, player can switch between person account and company accounts. Top navigation contains menu to switch player's view to any controlled account. In selected-company view, if person controls more than one company they can act for different companies (for example see accounting of another company or build for selected company). Personal account is also selectable. In that case player cannot build, but can start a new company.

## Person account

In the onboarding the player picks the game player name. This is the person account. At the start he owns certain amount of company shares the player creates. The ledger info for the player account is customized to person view.

Person cannot own land or buildings and does not pay tax. He can only own bank account balances or shares in the companies. Person account income is the sale of shares and dividends.

Player can switch to person view so that he can trade the stocks.

The person account display name is a cross-server identity attribute stored in MasterApi and reused by every game shard.

## City Global Exchanges

In each city there is one in-game global exchange serving as hub connecting cities. Global Exchange acts as never-ending resource sale source for every resource. Each city has different resource pricing and quality at the global exchange.

## Transit costs

When resource is sent between one unit to another (sale to purchase or exchange to purchase or b2b sale to exchange) the transit costs are calculated. The transit costs must be visible in the purchase unit when selecting the resource.

Transit costs must never be zero. Every transfer, even between player's buildings, costs shipping money. Shipping costs are determined by geolocation distance, each building has GPS coordinates and distance can be calculated between them. Different products must have different weight assumptions, so shipping one unit of medicine differs from shipping one unit of bed.


Shipping costs are visible in the company ledger.

Game aggregated shipping costs are visible in the administrator dashboard, clickable and then overview of the shipping costs per company is displayed.

## Taxes

At specific tick rounds the taxes are calculated.

## Encyclopedia

All product combinations are visible in manufacturing encyclopedia, which serves as in-game documentation.

When user clicks on the resource he can see at the same screen without scrolling all manufacturable resources associated with it.

Make resource detail a separate view from the encyclopedia entry. 

Encyclopedia entry is the list of all resources with search field.

The resource detail consists of resource description, picture, list of all resources it is used in input or output and the manufacturing details.

Every resource must have unique picture.

## Chat

In-game chat will be available.

### Discord chat bridge

The master server runs a Discord bot that bridges the in-game chat with a dedicated
Discord channel. Messages posted in the bridged Discord channel by a player whose
Discord account is linked are relayed into the in-game chat on that player's behalf,
and in-game chat messages are mirrored back into the Discord channel, allowing players
to interact across Discord and the game in both directions.

## Game administrators

Game administrators have a dashboard where they can see all critical issues in the game like inflow of money, highlighting users which may be doing multiaccount gaming where they boost one of the account.

Game administrators can switch a person to invisible mode. In this mode, person can see their chat messages, but others do not see them.

Game administrators can do impersonation of player's view. In this mode they can act on behalf of player, player's person account, or any player's company. Logs must show acting game administrator, target user, and target person/company account.

Game administrators can publish newspaper entries or modify latest changelog. Allow rich HTML editor for news editing and multilingual support before publishing.

There are roles in the game that can be assigned to any user account. Root administrator can assign or remove global and local game-administrator roles. User with global game-administrator role can access every game admin dashboard and perform game-administrator actions. Local game administrator can manage only a single game instance.

Game administration is managed in the master API, but local game-administrator role can be managed on game server.

List of root game administrators is managed by master API configuration.

### Operations dashboard (game frontend)

The game frontend uses one unified operations surface at `/operations/statistics` (legacy `/admin` route removed from primary navigation).

- Operations pages are split into focused subpages (statistics, players, interventions, publishing) instead of one oversized page.
- Money-flow statistics use live LedgerEntry aggregations (`operationsStatistics`) with inflow and outflow grouped by economic category.
- Product analytics table uses live production/sales aggregations (`adminProductAnalytics`) with sorting, filtering, and export support.
- News and changelog publishing is integrated into operations navigation with dedicated page layout and editor flow.

## Newspaper and changelog

Master API database holds changelog and newspaper. Admins can publish news to direct users or report progress.

With every change the changelog must be updated. The changelog is visible in the news section in every game.

Game administrators can edit any changelog or news record in any localization.

Track whether user has read news; if not, show unread-message count in navbar.

## Media house

Media building has single unit layout and does not show the grid.

The configuration for this single unit is spending level on content per tick.

The quality of the content is determined by accumulated costs spent by the media building. With the upgrade of the building, the content is more efficient. At start 50% of the costs goes to the aggregated content (1-1/2). Next level of building has 66% (1-1/3) efficiency, and so on.

Each tick every media house loses 0.5% of aggregated content value.

Content quality is determined by comparison with other media houses. If a media house has highest content, it is ranked at 100%. If a competing media house has half of top aggregated content value, it is ranked at 50%. This applies in same media category and city. Different categories do not affect each other.

The content quality ranking determines the speed with which the branding quality is increasing.

When media house building upgrade is in progress, marketing units must remain configurable and selectable against that media house. Upgrade state must not block marketing-configuration workflows.

## Monetization

Startup pack is available after user finishes onboarding. It is a time-limited offer and costs $20 in real money.

Startup pack includes 3 months of Pro subscription and in-game currency.

In pro subscription the players will have more products to manufacture and sell.

Pro subscription will cost $10/month.

## Research & Development

Show user's used products first in the R&D unit product-quality selection.

Research quality model is cumulative spending-budget model. R&D product research adds money to research. For each product define specific base quality-model budget where, if user accumulates this amount and there is no competitive company doing same research, player reaches 100% quality. If two players research same product, player with highest accumulated research money is base for all others. Every tick 0.1% of accumulated research budget is lost, so if player stops researching product, research quality diminishes over time.

With upgrading the unit to do research, the efficiency to do research improves. At start 50% (1-1/2) of the unit costs are accumulated to the research. Next level improves this to 66% (1-1/3)%, next level to 75% (1-1/4) and so on. While the upgrade is in progress, the player pays half of the costs for the unit.

## Banks

In loan menu, if person does not own bank, show link to buy a bank building. If person already owns bank, show link to their bank building.

In loan menu show list of all banks with current deposit and lending interest rates, sortable and filterable.

In bank building, allow people to deposit funds to receive interest from the player, and other players to ask for a loan. Player can issue loan only if he has deposits to the bank.

Bank building does not have any configurable unit, whole bank acts as a single unit.

In the bank, there is a configuration to set the interest to pay to deposit account holders, and interest rate which lenders pay to the player.

When player creates a bank, they must deposit base capital of $10000000. This serves as initial capital to be lent and is counted toward bank deposits.

### Deposits

When player opens a third-party bank, they can see current interest rate and deposit funds there. Deposit is created in bank and every tick interest is paid to depositor.

Player can withdraw money from the bank any time, even if bank does not have enough deposits on the account.

Bank owner company can deposit funds to the bank or withdraw money up to the base capital deposit.

### Loans

When player opens a third-party bank, they can see current lending rate and total money available to be lent.

Sum of available money to be lent is 90% of current deposits. Bank must preserve 10% deposit-to-loan ratio.

Company can request loan from any bank which has available deposits.

User can borrow money only for buildings which are not mortgaged. User can pick a building and he can borrow against it a money up to 70% of the property value.

Borrower decides amount and duration of loan. When player goes to bank, they can request loan with chosen duration and requested amount. They also deposit a building as collateral. One building can be used in only one loan.

Collateral enforcement requirements:
- If loan has missed payments and collateral building is moved to forced market sale, asking price must stay in building city/property currency (not in lending bank currency).
- Loan settlement after collateral sale must apply required FX conversion internally so debt is closed in loan currency while property sale remains in local property currency.
- User must not be allowed to cancel forced sale while collateralized loan is in missed-payment state.

Creating a loan creates a contract between bank and a player which will hold the interest rate even if the bank player changes the lending interest rate. Each contract has a maturity date. User can see each tick payment amount. The calculation is the same as in the real world mortgage payments with difference that the payment is done on every tick. The borrower pays the interest and principal amount.

Borrower can repay any part of the loan any time.

### Central bank

If bank deposits are negative because depositors withdrew money, bank borrows money from central bank. Interest rate for borrowing from central bank is variable depending on how many banks borrow from it. Rate fluctuates between 2% and 5% per game year.

If depositors add new money and bank has central-bank loan, bank repays central-bank loan using deposited money.

### Bank building details

When bank owner company is the current player, show the bank profit chart, interest rates chart, other details and composition of the loans.

When another player displays bank detail, ensure they see professional design for making deposits or requesting a loan.

## Power plants

In power-plant grid allow building following units:
- Purchasing unit - allows to buy the coal or gas
- Wind turbine unit - produces wind force. Each city has weather channel with prediction for next 50 ticks on wind intensity (0% to 100%), changing randomly by 2-5%.
- Water turbine unit - produces water force, extremely expensive, but produces steady force units
- Storage unit - allows users to store wind force and optimize for steady energy output
- Energy producing unit - consumes the coal or gas, wind force, and produces energy
- Battery unit - can store extra energy in peaks and output when production is insufficient.

Resource flow is:
- Purchasing unit | Wind turbine unit | Water turbine unit -> Storage unit | Energy producing unit
- Storage unit -> Energy producing unit
- Energy producing unit -> Battery unit

Power plant as a building has planned-output configuration. If output is oversupplied, power plant does not receive money for oversupply. If output is undersupplied, it receives government fines for not generating enough energy.

Make sure to show power-plant P&L chart in building overview.

## City selection

- In top menu where company is selected, also add city selection.
- In the player dashboard filter only buildings in the selected city
- In the banking page show only banks in selected city.
- In global exchange remove city selection and use selection from navbar.
- In marketing analytics page show only data related to selected city.
- If user logs out while selected city has no owned factory, after next login the context switcher must automatically fall back to the player's primary city (city with highest owned factory count).

## Referral program

- Every user can become a referral if they fill in real name and tax domicile.
- In master frontend there is a referral dashboard where user can activate referral account.
- One user can create multiple referral codes. First referral code is auto-generated as 8-character alphanumerical string.
- Any user can fill in referral code of another player. This way they receive referral discount on subscription and referral owner receives share of first- and second-level subscriptions.
- When purchasing subscription and user does not have active referral, user is prompted to add referral code. There is no explicit message that referral code gives discount, so the recommender should promote it.
- Referral dashboard shows number of registered users under specific referral code, number of second-level referral registrations, number of active subscriptions, and number of second-level active referral subscriptions.

## Support system

At the master frontend user can create a support ticket. The support tickets are of 3 types: 

- I have a suggestion
- I found a bug
- Other

The ticket has state: 
- Submitted
- In progress
- Finished

User can see his own tickets and its states in the table where he can filter, sort by creation date or title.

Administrators can see all tickets from all users, can sort and filter by type, date, title. By default the newest tickets are at the top.

The tickets are created in md format. There must be nice wysiwyg editor so that users are happy and can also post the images.

If the post contains images or links make sure to show the raw file to admin and after he confirms it is safe he is able to see the formatted md content.

## Discord bot

The master server provides a Discord bot so a community Discord can interact with both
the staging and production master servers from one place. Every slash command is
prefixed to keep environments apart: production uses the `cap5` prefix and staging uses
the `cap5stage` prefix (for example `/cap5-verify` and `/cap5stage-verify`). Staging and
production run as separate Discord applications with their own bot tokens.

Commands:

- **verify** (`/cap5-verify`): links the player's Discord account to their master account
  using a one-time code generated on the master frontend, and awards the Discord player
  bounty.
- **deposit** (`/cap5-deposit`): creates a tokenized-gold deposit request and returns the
  deposit address and the required note field.
- **withdraw** (`/cap5-withdraw`): creates a tokenized-gold withdrawal request for the
  given amount and destination address.
- **help** (`/cap5-help`): posts the master frontend URL, the community Discord invite,
  and basic instructions on how to play.

The bot also bridges in-game chat with a configured Discord channel in both directions
(see the Discord chat bridge under Chat). Setup instructions live in
`docs/discord-bot-setup.md`.

## Master ranking point system

Master ranking system is player engagement tracker with bounties. There are different type of bounties - with periodic refresh or one time bounties.

Each player can see his points and also the bounties track record for each bounty in the master frontend.

At the UTC midnight (once per day) the ranking of all players is reduced to 99%. This will ensure that old players who are not playing any more in time will loose the ranking.

Ranking system is evaluated once per an hour by the scheduled task at the master backend.

It must be enjoyable for players to see and compete with other players in the master ranking.

Below is the list of the bounties

### Game improver
- Periodic bounty with refresh interval 1 day at the utc midnight
- Reward: 5 points

When user fills in the game improvement or bug report form, he receives this bounty. The bounty is limited to one submission per day.

### Recommend a friend
- Periodic bounty with refresh interval 1 day at the utc midnight
- Reward: 5 points

When user recommends other player using the referal link and other player register the user receives this bounty

### Recommend a good friend
- Periodic bounty with refresh interval 1 day at the utc midnight
- Reward: 100 points

When recommended player purchase the starting pack or activates the subscription, the referee receives the bounty reward

### Retweet a X post
- One time per post
- Reward: 5 points

Administrator can set up the bounty for retweeting the post. The player will be awarded the bounty if he retweets, submit the url for the check, and the tweet will tag at least 2 other player's friends.

The retweets are private and only administrators can see the links of the retweets in the bounty records.

### Discord player
- One time bounty
- Reward: 50 points

If user joins the game discord and validates through the discord bot, he receives the bounty reward.

Discord username is private for public, but Administrators can check the discord username in the player's bounty rewards.

### Log in to the game
- Periodic bounty with refresh interval 1 day at the utc midnight, scoped for each game server
- Reward: 5 points

For every game server where user logs in and loads the user dashboard, if he did not receive the bounty reward he will receive it.

### Manufacturer
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 1 points

If user has a factory and produced any amount of products, he receives the bounty reward. This can be applied only once per day from any game server.

### Wholesaler
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 1 points

If user has a sales shop and sold any amount of products, he receives the bounty reward. This can be applied only once per day from any game server.

### Researcher
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 2 points

If user owns a R&D building and has setup a research budget in any unit, he receives the bounty. This can be applied only once per day from any game server.

### Real estate magnate
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 2 points

If user owns a Appartment or commercial building and has any occupancy, he receives the bounty. This can be applied only once per day from any game server.

### Media owner
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 2 points

If user owns any Media house and has setup any budget for the content creation, he receives the bounty. This can be applied only once per day from any game server.

### Banker
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 2 points

If user owns a bank building and any other user has made a deposit to it, the bank owner user receives the bounty reward. This can be applied only once per day from any game server.

### Lender
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 2 points

If user owns a bank building and any other user has active loan, the bank owner user receives the bounty reward. This can be applied only once per day from any game server.

### FX Trader
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 2 points

If user swaps any currency to any other in game currency, user receives the bounty reward. This can be applied only once per day from any game server.

### Stock Trader
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 2 points

If user buys any stocks, user receives the bounty reward. This can be applied only once per day from any game server.

### Energy Trader
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 2 points

If user owns any power plant and ships any energy to the market, user receives the bounty reward. This can be applied only once per day from any game server.

### Good employer
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 10 points

If user has highest wage rate in any city in which he pays salaries in any game server, user receives the bounty reward. This can be applied only once per day from any game server.

### Dividends master
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 2 points

If user's owned company pays out the dividend to shareholders, user receives the bounty reward. This can be applied only once per day from any game server.

### Top player
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 5 points

If user's personal account is ranked in top 10 players in any game server, user receives the bounty reward. This can be applied only once per day from any game server.

### Great player
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 2 points

If user's personal account is ranked in top 100 players in any game server, user receives the bounty reward. This can be applied only once per day from any game server.

### Company master
- Periodic bounty with refresh interval 1 day at the utc midnight, applied only once for any game server
- Reward: 5 points

If user's company is ranked in top 10 players in any game server, user receives the bounty reward. This can be applied only once per day from any game server.

## NPC bots

In the game are basic NPC bots which plays the same way as basic users. They are run by the scheduled console app. Their role is to find the best products to produce and sell at the market while utilizing the marketing and research. The bots must play in a way to reach in game profit with level 1 units.

Bots are written in c# as the console apps and on scheduled time once an hour analyze the product what they want to produce and sell, and if they have the capacity to reorganize their factories and sales shops.

Game administrators can start new bot.

## Industries

All eight industries are available on the game server. The three starter (free) industries are unlocked for every player. The five advanced industries require an active Pro subscription.

| Industry | Raw material | Starter products | Subscription |
|---|---|---|---|
| Furniture | Wood | Wooden Chair, Wooden Table, Wooden Bed | Free |
| Food Processing | Grain | Bread, Flour, Porridge | Free |
| Healthcare | Chemical Minerals | Basic Medicine, Bandages, Vitamins | Free |
| Electronics | Silicon | Microchip, LED Bulb, Solar Panel | Pro |
| Construction | Iron Ore | Steel Beam, Concrete Block, Nail Pack | Pro |
| Pharmaceuticals | Gold | Pain Relief Tablet, Antiseptic, Antibiotic | Pro |
| Energy | Coal | Electricity Token, Heat Pack, Generator | Pro |
| Logistics | Cotton | Packaging Box, Rope Coil, Fabric Wrap | Pro |

Each industry has a clearly defined fantasy, a first product the player will produce, and a "why choose this" differentiation. Industry cards in the onboarding wizard display this information to help new players make an informed choice.

## Supply chain visualization

The building detail view contains a **Supply Chain** tab that renders the full upstream and downstream flow of resources for that building.

- Each edge between buildings in the flow diagram shows the transit cost per unit (shipping distance × weight factor × fuel price index).
- Hovering an edge shows the cost breakdown (km, weight, rate, FX amount).
- Each building in the diagram shows a health score badge (green / yellow / red) based on whether inventory is flowing, stalled, or depleted.
- The health score is also shown as a badge in the company dashboard building list so players can triage broken supply chains at a glance without opening each building.

## Market intelligence

The **/market-intelligence** view provides competitive analysis data:

- **Competition pie chart** inside the Public Sales unit detail panel: shows each seller's share of city demand for the current product, updated each tick.
- **Resource price sparklines** on the global exchange view: per-resource mini charts of the last 50 ticks showing price trend at a glance.
- Aggregate demand drivers panel: shows which of the five demand signals (salary, brand, quality, trend, price) is dominant for the current product/city combination, with a numeric factor for each.
- Price recommendation engine: the UI suggests a price band (min / optimal / max) based on market demand, competitor pricing, and the base price benchmark.

## Player notifications and alerts

A bell icon in the navbar shows an unread notification badge.

Notifications are created server-side by game events:
- Mine resource deposits depleted below a threshold (configurable).
- Loan approaching maturity.
- Building construction or upgrade completed.
- Share price movement beyond a threshold.
- Competitor building appears in your city.

Players can configure alert thresholds per notification type in account settings. Clicking the bell icon opens the notification panel; notifications are marked read on open.

## City expansion and inter-city trade

The game world contains multiple cities. Cities active at launch:

| City | Currency | Region |
|---|---|---|
| Bratislava | EUR | Central Europe |
| Prague | CZK | Central Europe |
| Vienna | EUR | Central Europe |
| Berlin | EUR | Western Europe |
| Warsaw | PLN | Eastern Europe |

Each city has independent resource abundance, salary levels, fuel price index, population, and economic health. Players can own buildings in multiple cities simultaneously.

### Inter-city trade routes

When a player's purchase unit buys from a B2B seller or global exchange in a different city, a **trade route** is created. Transit takes multiple ticks proportional to GPS distance.

- Transit cost = `distanceKm × weightPerUnit × transitCostRatePerKmPerWeightUnit × fuelPriceIndex`.
- The transit cost is shown on the purchase unit when the player selects the source seller.
- Transit costs appear in the company ledger under the "Shipping" category and are visible in the administrator dashboard.
- Shipping costs are never zero; even intra-city transfers have a minimum transport cost.

The **/cities** overview page lists all active game cities with their population, economic health index, currency, and resource availability summary. Each city links to its interactive Leaflet map.

## Building lifecycle

### Building destruction

A building owner can choose to destroy a building from the sell-building workflow. The destruction workflow:

1. Player opens sell-building form and selects the destroy action.
2. UI must show refund preview before confirmation.
3. On confirmation, building is destroyed and owner receives 80% of the exact building value.
4. Exact building value is defined as: the recorded lot purchase amount paid for that parcel + current building construction cost for that building type + replacement cost of every active unit at its active level (base unit construction cost plus all required upgrade steps).
5. Destroyed lot becomes available for purchase again.
6. If collateralized loan has missed payments, system forces building sale at market appraised value minus 10% and user cannot cancel that sale.
7. If missed-payment debt is still unresolved after 72 ticks (3 game days), building is destroyed and remaining debt is settled from collateral sale proceeds.

The same exact-value basis is used for the market-valuation preview and minimum sale price in the sell-building workflow so players never receive a demolition or forced-sale valuation that exceeds the real recorded land cost plus the active structure and unit build costs.

### Building secondary market

Buildings can be listed for sale on the in-game building market.

- Owner marks a building "For Sale" and sets an asking price from the building overview panel.
- A "For Sale" badge is shown on the building card in the dashboard and on the city map.
- The **/buildings/market** page lists all buildings currently offered for sale, with filters for city, building type, and price range.
- Interested buyers submit a purchase offer. The seller can accept, counter, or reject the offer.
- On acceptance, ownership transfers atomically and the payment moves bank-account-to-bank-account. All loans against the building transfer to the new owner unless the loan terms prohibit it.
- A building used as loan collateral can be listed but not transferred until the collateral is released.

## Resource depletion and scarcity

Mining buildings consume resource deposits over time. Each mine lot has a defined `materialQuantity` (tonnes equivalent) and `materialQuality` (0–100%).

- Every tick a MINING unit extracts a quantity proportional to its level and the current deposit remaining.
- As the deposit depletes, extraction rate follows a diminishing-returns curve: below 30% remaining the rate drops to 60% of nominal; below 10% it drops to 20%.
- A `MineDepletionRecord` is written each tick with current remaining quantity.
- When a deposit falls below 20% remaining, the game creates a player notification and shows a depletion progress bar in the **Mining Resource Status** panel on the building detail view.
- Once per game year a `ResourceReplenishmentSchedule` event can partially refill depleted deposits (simulating geological timescales compressing). Players are notified when replenishment occurs.
- Fully depleted mine lots still occupy the map but produce zero output. The owner may demolish the building to free the lot.

## Seasonal demand

Public sales volumes are modified by seasonal demand multipliers. Each product belongs to a demand seasonality category.

- Seasonal multipliers are defined per product category per quarter (Q1 Jan–Mar, Q2 Apr–Jun, Q3 Jul–Sep, Q4 Oct–Nov).
- Example: winter products (heating, food staples) peak in Q1/Q4; summer products (furniture, outdoor) peak in Q2/Q3.
- The multiplier is applied in `PublicSalesPhase` on top of the base demand calculation.
- The public sales unit detail panel shows the current season multiplier and a quarterly demand forecast chart so players can plan inventory.

## In-game tutorials

A guided tutorial system helps new players navigate key game mechanics after onboarding.

- The **TutorialProgress** entity records completion of five canonical milestones: first resource sold, first B2B trade, first loan taken, first competitor observed in market intelligence, first brand established.
- The **/tutorial** view shows checklist progress, completion timestamps, and deep links for unfinished milestones.
- **TutorialTooltip** component is the standard contextual overlay and supports dismiss, escape, and auto-dismiss behavior.
- Contextual overlays for first dashboard visit and first building-detail/grid-editor visit are required integration points for next increment.
- Tutorial completion must integrate with master ranking bounties; each tutorial bounty is awarded once per lifetime per user, and bounty award must mark the tutorial milestone as completed when needed.

## Player profile and statistics

Every player has a public profile accessible at **/player/:id**.

The profile displays:
- Display name with Pro badge if active subscription.
- Join date (in game year).
- Bio (editable by the player, max 160 chars).
- Global leaderboard rank and total wealth in USD.
- Company count, active cities, active building types, and total products sold.
- Company equity breakdown.
- **Hall of Fame** panel: highest single-tick revenue, largest single building acquisition price, and highest brand quality ever achieved (with brand name).

Leaderboard rows link directly to the player profile page. A custom profile badge can be unlocked by completing specific master-ranking bounties and is visible on the leaderboard table and profile page.

## Company growth and second IPO

After a player's first company has been operational for at least one game year (8 760 ticks) and meets profitability prerequisites, the player can launch a second company through the **New Company IPO** flow accessible from the personal account dashboard.

Prerequisites to unlock the CTA:
- First company profitable for ≥ 365 ticks.
- Personal USD balance ≥ $200 000.
- Fewer than 5 active player-controlled companies.

The IPO wizard mirrors the onboarding IPO step: player configures name, city, raise amount, and ownership split. On success the new company is provisioned with a bank account, founder shareholding, and ledger entries.

The dashboard CTA shows a prerequisites checklist with a tooltip explaining each locked requirement when gates are not met.

Maximum of **5 player-controlled companies** per person account. This prevents monopolistic lockout of all available lots while still enabling meaningful diversification.

## City economic health

A **City Economic Report** is computed each tax cycle and stored per city. The report aggregates: total salaries paid, total public sales revenue, number of active companies, total power consumption vs. supply, and average product quality index.

An overall **economic health index** (0–100) is derived from these signals: 40% salary weight, 30% revenue, 15% power balance, 15% quality.

The **City Health** mini-dashboard card on the city map page shows:
- The numerical health index with a traffic-light colour (green ≥ 70, yellow 40–69, red < 40).
- Sparkline trends for the last 10 tax cycles.
- Full history accessible via a detail modal.

Cities with a health index ≥ 70 experience +0.5% population growth per tax cycle. Cities below 40 experience −0.2% population erosion. This creates genuine city competition dynamics over long game years.

## FX Exchange rates display

The FX rates page shows all available currency pairs with a three-price display:
- **Buy price** (ask): the rate a player pays to acquire the foreign currency.
- **Mid price**: the theoretical mid-market rate used for valuation.
- **Sell price** (bid): the rate a player receives when selling the foreign currency.

Currency-pair orientation follows stronger-currency-first convention in this strength order: EUR > USD > CNY > GBP > INR > CZK > PLN.

Examples:
- If selected context currency is CZK (Prague), rate list should show pairs like `USDCZK` and `EURCZK`.
- If selected context currency is EUR (Vienna), rate list should show pairs like `EURUSD` and `EURCZK`.

The rates table is displayed above the pair-history chart.

A **rate history chart** is available for any selected currency pair, showing the last 100 ticks of mid-price movement.

The FX swap UI allows players to select source and destination bank accounts, displays the current balance for each account in the selector, defaults to the swap tab, and shows the converted amount in real time as the player types.

## Country flags

Country flags are displayed using the `country-flag-icons` npm package wherever cities or currencies appear:
- City list on **/cities** overview page.
- Footer language picker (shows flag of active locale country).
- Context switcher city dropdown.
- City map header.

## Ranking enhancements

- Each player row on the leaderboard links to that player's public profile page.
- The currently authenticated player's row is highlighted (accent background) for easy self-location.
- When navigating to the leaderboard, the pagination automatically scrolls to the page containing the current player's entry.
- A link to the **master ranking** (cross-server, hosted on MasterApi) is shown at the top of the in-game leaderboard page.

## Responsive design

The game UI is designed and tested at four viewport categories:
- **Mobile** (≤ 480 px): single-column layout, collapsible navigation, touch-optimised tap targets.
- **Tablet** (481–1024 px): two-column content grids, side-by-side panels where space allows.
- **Full HD** (1 920 × 1 080): default desktop target; all charts, tables, and grids optimised for this size.
- **4K** (≥ 2 560 px): constrained max-widths to prevent excessively wide content, upscaled typography where appropriate.

All Playwright E2E tests for player-facing views must include at least a mobile (375 px) and full HD (1 920 × 1 080) viewport assertion.

## Security audits

A security audit is conducted weekly by reviewing the audit log and running a structured threat model against the OWASP Top 10. The audit covers:
- Whether any API mutation or query allows one player to gain an unfair advantage over another through unvalidated input, object-level authorisation bypass, or race conditions.
- Whether server-controlled fields (tick counts, prices, balances, ownership) can be overridden by client-supplied input.
- Whether GraphQL anti-abuse controls (introspection, query depth, complexity, and auth rate limiting) are enforced consistently across both single and JSON-array batched requests and across named operations on the game API and Master API.
- Whether session bearer tokens are kept out of persistent web storage: gameplay sessions authenticate via the secure cookie session and the frontend never persists the raw JWT in `localStorage`.
- Findings are documented in `/audits/<YYYY-WW>.md` with risk severity, affected endpoint or component, and a remediation action plan.

## Building lifecycle delivery notes

See **Building lifecycle → Building destruction** above for canonical behavior. Implementation and rollout must follow those rules and remain consistent with collateral-enforcement behavior under missed loan payments.

## News feed behavior

News and changelog feed includes a **Mark all as read** action for authenticated players. Read receipts are tracked server-side and unread counter in navbar must clear immediately after successful action.

## Banking deposit rate management (planned)

Bank owners can change the deposit interest rate they pay to depositors. The new rate applies with a 24-tick delay so depositors have time to withdraw before the rate change takes effect. The rate change applies uniformly to all deposit accounts held in that bank.

# Technical implementation

Game server frontend is Vue.js with source code located at projects/frontend using Tailwind styling.

Master server frontend is Vue.js with source code located at projects/master-frontend using Tailwind styling.

Game server backend is .NET with GraphQL engine and data stored in PostgreSQL. Source code is at projects/Api.

Master server backend is .NET with GraphQL engine and data stored in PostgreSQL. Source code is at projects/MasterApi.

Players must receive near real-time user experience.

Deployed to Kubernetes.

## Deployment topology and CI/CD

All public environments are deployed to Kubernetes through GitHub Actions. GitHub Actions workflows, Kubernetes manifests, ingress definitions, and runtime secret wiring are the source of truth for stage and production. Manual cluster edits are emergency-only and must be reconciled back into git.

GitHub Actions must define two deployment environments: `Stage` and `Production`. `Stage` is the continuously updated integration environment for the latest `main` branch. `Production` is the public player-facing environment and must use separate secrets, protection rules, and approval flow from stage.

Production master entry points:
- `https://www.capitalism5.com` is the canonical master frontend URL.
- `https://capitalism5.com` must stay reachable and redirect to `https://www.capitalism5.com`.
- `https://api.capitalism5.com` hosts the master API.

Stage master entry points:
- `https://www.stage.capitalism5.com` hosts the stage master frontend.
- `https://api.stage.capitalism5.com` hosts the stage master API.

Each game server must use a humanly memorable slug derived from the display name. Example: `Inception of Wealth` becomes `inception-of-wealth`.
- Production game frontend: `https://inception-of-wealth.capitalism5.com`
- Production game API: `https://inception-of-wealth-api.capitalism5.com`
- Stage game frontend: `https://inception-of-wealth.stage.capitalism5.com`
- Stage game API: `https://inception-of-wealth-api.stage.capitalism5.com`

All ingress resources must use the cert-manager `ClusterIssuer` named `letsencrypt-dns`. Certificate coverage must match the real DNS zones in use: `capitalism5.com` for the production master endpoints, `*.stage.capitalism5.com` for stage endpoints, and `*.capitalism5.com` for production game-server endpoints. Wildcard certificates must not be specified for a different DNS zone than the hostname they secure.

Every push to `main` must rebuild and redeploy all stage workloads: the stage master frontend, the stage master API, every stage game frontend, and every stage game API. Stage must always represent the current integrated branch head.

GitHub Actions must also provide a dedicated workflow for provisioning a brand-new game server end-to-end. That workflow must create the PostgreSQL runtime, generate or inject per-server secrets, deploy the game API and game frontend, configure ingress and TLS, register the shard with the master server, and run smoke checks so the new game is fully configured before it is announced.

Operational documentation must describe how to create GitHub Actions environment secrets and variables for `Stage` and `Production`, how to keep administrator contact data and other sensitive values out of git, and how those values are materialized into Kubernetes Secrets without printing them into workflow logs.
