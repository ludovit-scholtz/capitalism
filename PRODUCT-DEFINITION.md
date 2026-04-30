# Capitalism Game

Create a fun game in the style of Capitalism II. This game is an economic simulation where players can experience price elasticity, resource scarcity, resource oversupply, different competition types, marketing, product quality, and difficulties with scaling up companies.

It will use a real-world map. The game starts in a single city and later expands to more cities.

## FX Exchange

Each city is located in a physical country and has a local currency, for example CZK for Prague, EUR for Vienna, or USD for New York.

The FX exchange is visible in the main menu. When a user opens the FX exchange, they pick the source currency, then the destination currency, and enter the amount. The system generates a quote and, if the user confirms, executes the trade. The quote also shows the 1% swap fee.

Besides city FX currencies, the FX exchange supports the gold token.

Swapping the gold token has special rules and requires liquidity in an in-game AMM. It uses the traditional AMM function `fx currency * gold = constant`. Each player can create a liquidity pool or fund an existing pool and see their liquidity positions. To create a liquidity pool, a person picks the currency, adds FX currency and gold amounts, and creates a liquidity position. Market this clearly so liquidity providers understand they earn 1% AMM fee rewards. Users must be able to remove liquidity from the pool. Users cannot use resources blocked inside the AMM pool.

## Gold token

Gold token is a special in-game currency representing 1 gram of real-world gold. The gold token amount is stored in the user's account on the master server.

The server global administrator can manage gold token funds on player accounts in the master-frontend global administration.

## Multiple Game Servers

The master website is the product-pitching website where users can find in-game documentation and a list of active game servers. Authenticated users can see their Pro subscription and purchase an extension.

Master API has its own database and handles the subscription management.

## Authorization

When player creates the account, he creates it at the master server. When user requests the token, he does it against the master server. The token is usable against every game server and master server.

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

Onboarding process:
1. User is given $200000 to his personal bank account and he picks the game player name
2. IPO process: user transfers $50k from personal bank account to business bank account and decides how much money to raise ($800000, $600000, or $400000), varying own shares to 25%, 33%, or 50%. User picks company name.
3. Player selects the industry type they want to start with. The Furniture, Food processing, or Healthcare.
4. Player selects the product he wants to produce - Each starting industry allows 3 basic products to be produced.
5. Then player picks location of first factory. This sets factory layout and user pays all associated costs (property and company layout). Show cost analysis before purchase. Wizard should show important areas, like paying bank account, current bank balance, and pricing/public-sales configuration guidance.
6. Next the player buys his first sales shop and configures it to set the sales price to public. User pays for the land and sales shop unit layout from the selected company/building bank account - make sure the user has clear information about this.
7. The player is shown that the time goes on and he makes the profit from his business.
8. User is asked to create the user account.

Do not require authentication for new unauthenticated users. Do not store progress for these users on backend, but make sure they can see they bought buildings, set up resource chain, and made profit. After that ask them to log in to save progress. If an error occurs (for example lot purchased by someone else or invalid profit outcome), create profile with chosen name and restart wizard with authenticated user, then persist everything.

## Stock exchange

There is one global stock exchange where all company shares are traded. The share price is calculated as the sum of all equities of the company (including land, units, warehouse stocks, bank-account balances, owned stocks, and other assets) plus profit expectation divided by number of issued stocks.

Profit expectation is a complex formula where new companies start at zero. Formula includes current-year profit, history of profits in past years, and dividends paid.

Player acting for the company or person account can buy shares for any company including its own from public investors. Market bid price is 1% below the share price and offer is 1% above the share price. The buying of the company shares directly by the company is considered as the company buy back and reduces the number of issued shares. Every trade settles between bank accounts and the acting person/company must choose the source or destination bank account for the settlement.

Player acting for the company or person account can sell shares it owns.

When sum of ownership for person account and all controlled companies in another company reaches 50%, player can replace CEO (takeover) and gain control of that company.

When sum of ownerships for person account and all controlled companies reaches 90%, person can merge this company into another company. This way all assets owned by the company are moved to the new company and the merged company is closed. Taxes for old company are paid on the tick of merge for old company.

Stock-exchange company details include shareholder list and pie chart.

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

In top menu, player can switch between person account and company accounts. Top navigation contains menu to switch player's view to any controlled account. In selected-company view, if person controls more than one company they can act for different companies (for example see accounting of another company or build for selected company). Personal account is also selectable. In that case player cannot build, but can start a new company.

## Person account

In the onboarding the player picks the game player name. This is the person account. At the start he owns certain amount of company shares the player creates. The ledger info for the player account is customized to person view.

Person cannot own land or buildings and does not pay tax. He can only own bank account balances or shares in the companies. Person account income is the sale of shares and dividends.

Player can switch to person view so that he can trade the stocks.

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

## Game administrators

Game administrators have a dashboard where they can see all critical issues in the game like inflow of money, highlighting users which may be doing multiaccount gaming where they boost one of the account.

Game administrators can switch a person to invisible mode. In this mode, person can see their chat messages, but others do not see them.

Game administrators can do impersonation of player's view. In this mode they can act on behalf of player, player's person account, or any player's company. Logs must show acting game administrator, target user, and target person/company account.

Game administrators can publish newspaper entries or modify latest changelog. Allow rich HTML editor for news editing and multilingual support before publishing.

There are roles in the game that can be assigned to any user account. Root administrator can assign or remove global and local game-administrator roles. User with global game-administrator role can access every game admin dashboard and perform game-administrator actions. Local game administrator can manage only a single game instance.

Game administration is managed in the master API, but local game-administrator role can be managed on game server.

List of root game administrators is managed by master API configuration.

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

# Technical implementation

Game server frontend is Vue.js with source code located at projects/frontend using Tailwind styling.

Master server frontend is Vue.js with source code located at projects/master-frontend using Tailwind styling.

Game server backend is .NET with GraphQL engine and data stored in PostgreSQL. Source code is at projects/Api.

Master server backend is .NET with GraphQL engine and data stored in PostgreSQL. Source code is at projects/MasterApi.


Deployed to kubernetes.

Players must receive near real-time user experience.
