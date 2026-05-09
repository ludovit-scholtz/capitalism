# Capitalism Roadmap

Create a fun game on style of the capitalism II game. This game is economic simulation where players can experience price elasticity, resource scarcity, resource oversupply, different competition types, marketing, product quality, difficulties with scaling up the companies, and other base economic factors.

It will use real world map. The game will start in single city and later other cities will be added.

## Issues to work on

### Endgame (100% complete)

- [x] Fix endgame. This is not true: 'The server ends when a player exceeds the wealth of the 5th richest real-world billionaire.' The game ends when player exeecds the wealth of the most richest real world billionare, not the 5th.
- [x] Show real-world billionaires in the in-game personal account rankings with Race to the Top benchmark context.

### Onboarding (100% complete)

- [x] Allow players to change the personal account name. Make sure in the dashboard for the personal account is tab to change the player name. Store the personal account name in the master database so that the personal account player name is the same in all game servers. If the personal account name already exists in the master database after new game onboarding, do not change it, and make sure the personal account name is preserved.

### Authorization (100% complete)

- [x] When I log out from game server, the login is executed. Make sure to show the main page after the logout.
- [x] Do login/password authorizaiton only if configuration allows it. Make it disabled by default, but make sure to enable it in the tests. This works now on game server, but user password authentication is still enabled on master server.

- [x] Do login/password authorizaiton only if configuration allows it. Make it disabled by default, but make sure to enable it in the tests. Do this on game frontend, master frontend and both backends as well. When biatec oidc is the only authorization method, when user goes to /login page, make sure to automatically follow the authorization process as user would click the authorize with google button.
- [x] Allow special token based authorizations for bots. Create a form for users to create an API key. Each API key is bound to the personal account and user can impersonalize this key to control his controlled companies. Track the usage of the API keys in the administrators section. Create tests to test also negative scenarios such as user is not allow to control foreign company or he cannot do forex swaps. Make sure the bots console app is using this form of authorization.

### Buildings (100% complete)

- [x] Improve design of the tabs in building editation mode for public sale unit. The `unit-insight-card recent-activity-panel` div has the top border while `unit-detail-tabs` div has bottom border which creates effect of two horizontal lines. Also `unit-detail-tabs` is touching the tab button bottom border `unit-tab-btn--active`. 
- [x] Improve design of the tabs in building editation mode for purchase unit. In the first tab add some space between the tabs and content. Add to the basic info also the history of the purchase price and quality of purchased products. In other tabs there is one extra line below the tab headers.
- [x] The recommended market value in the building sales flow is very low. I think it does not include the property value, and perhaps it does not include also the unit values. Make sure the market price for the building is calculated properly.
- [x] Do not allow to sell building below 70% of its market value.
- [x] Add some space below `customer-bank-profile` div.
- [x] In bank building in `operating-account-row` is too much content that does not fit into the row. Add `Bank Statement Review` in second line or somewhere else.
- [x] Create a workflow to destroy a building. Make sure the button to destroy the building is in the sell building form and show also the refund how much user will receive. When building is destroyed, return the user 80% of the building property value. Make the property available for purchase again. When bank loan is not paid set it for sale for the property market price minus 10%. When the debt from missed payments is not paid in 3 game days (72 ticks), destroy the building and pay any remaining debt from the sale of property to the bank owner.

### Consumable Raw Materials & Resource Scarcity Mechanics (90% complete)

- [x] Mining now consumes finite lot deposits each tick with a diminishing-return efficiency curve tied to remaining reserve levels.
- [x] Added live resource-scarcity GraphQL surfaces (`getLandResourceStatus`, `getCityResourceMap`) with efficiency and depletion estimates.
- [x] Added mine low-reserve (20%) and critical-reserve (5%) notifications plus stronger map/dashboard depletion visibility.
- [x] Enforced game-engine safeguard to maintain at least 2 non-depleted purchasable deposits per resource type in each city.
- [ ] Add full mine-side historical extraction chart UX (30-day sparkline + expanded depletion timeline dialog) in building detail.

### Power Plants & Energy System (100% complete)

- [x] Add the city-wide power grid with five power-plant types, weather-aware renewable output, thermal fuel reserve handling, and legacy-grid fallback when a city has no player-owned plants.
- [x] Show power status and power-balance guidance in the dashboard, city map, and building detail flows, including power-plant analytics and dispatch controls.
- [x] Cover the power system with backend and Playwright regression tests, including dispatch authorization/validation guardrails and power-plant analytics ownership checks.


### Banks (100% complete)


- [x] After fx transfer the transfer amount resets and it shows error 'Enter a positive amount.'. Do not show the error after the successful transfer.
- [x] Make sure the interest paid from the bank to the users who deposited money to the bank account at the bank is clearly visible in the ledger and the bank statement for every tick.
- [x] Investigate why bank statement latest row does not equal to current balance on the bank account. Perhaps it is related to the loan payments as I do not see the loan received nor any of the loans currently on the bank account statement.
- [x] When bank loan is not paid set it for sale for the property market price minus 10%. When the debt from missed payments is not paid in 3 game days (72 ticks), destroy the building and pay any remaining debt from the sale of property to the bank owner.
- [x] When bank loan is not paid, make sure to notify user using the notifications that he has pending debt to the bank. When user goes to the bank, make sure the pending debt amount is clearly visible and also pending time until the building in the collateral will be destroyed.
- [x] When bank loan is unpaid and building goes for sale, make sure to put it on sale in proper currency. When builing in Prague which costs 10M CZK is collateralized in USD bank, the collateral amount is correctly calculated and allows to lend 300k USD. However when unpaid loan is hit, make sure to sell it not for 300k USD but for 10M CZK. After the building is sold on market make sure to settle the loan payments in correct currency - make sure to do the swap if required.
- [x] When there is unpaid loan and building is put on sale, user can cancel the sale of the building. Do not allow user to cancel sale of the building which is collateralized for loan and loan has missed payments.

### FX Exchange (100% complete)

- [x] Make sure to show the rate in the stronger currency. The currency strength is EUR,USD,CNY,GBP,INR,CZK. So when user has selected in the context switcher Prague the CZK currency it will show USDCZK and EURCZK numbers. When Vienna and EUR is selected make sure to show rates for EURUSD and EURCZK. Show the pair also in the rate list as it is common in standard forex. 
- [x] Move the rates table above the currency pair chart

### Fix city selection (100% complete)

- [x] When I switch city to city where i dont have any factory, log out and log in later with biatec oidc, i want the context switcher automatically switch to my main city where I have the most factories

### Ranking (100% complete)

- [x] Move link from game ranking to master ranking next to richest players and richest companies

### Stock Exchange & Company Share Trading (100% complete)

- [x] Deliver the in-game stock exchange with live listings, personal/company account trading, portfolio overview, dividend history, and per-company shareholder breakdown.
- [x] Extend the exchange listing UX with city/industry filtering metadata and daily change percentages visible in the market table.
- [x] Add native limit-order entities and a tick-based price-time priority matching engine with cancellable pending orders and explicit order book depth.
- [x] Add dividend-governance proposals and shareholder voting windows (propose, notify, vote, settle after 10 ticks).

### Dynamic Macroeconomic Cycles & Market Events (100% complete)

- [x] Added a server-driven global economic cycle (Expansion → Peak → Recession → Trough) with monthly evaluation, phase intensity multipliers, and 48-tick recession warning notifications.
- [x] Added market-event infrastructure for commodity shocks, interest-rate changes, and seasonal demand surges, and wired these multipliers into tick processing (public sales demand, purchase sourcing, loan repricing, and exchange offers).
- [x] Added GraphQL economy queries (`currentEconomicCycle`, `activeMarketEvents`, `economicHistory`) and surfaced the data in the dashboard economy widget plus contextual event banners in forex and public-sales views.

### Optimize for mobile (100% complete)

- [x] Make sure the design is smooth on small mobile devices for all pages. Mainly the content should be visible without the page scroll to the right.
- [x] Make sure the design is smooth on tablet sized screens.
- [x] Make sure the design is smooth on Full HD devices
- [x] Make sure the design is smooth on 4K screens

### In-game tutorials and interactive help (100% complete)

- [x] Add a `TutorialProgress` entity tracking per-player completion of guided tutorial milestones: first resource sold, first B2B trade, first loan taken, first competitor observed in market intelligence, first brand established.
- [x] `getTutorialProgress` GraphQL query returns all 5 milestones with `isCompleted` and `completedAtUtc` for the authenticated player. `markTutorialMilestoneComplete` mutation persists a milestone completion idempotently with full validation.
- [x] Add a `/tutorial` view accessible from the navigation that lists all 5 tutorial milestones with completion status, bounty points per milestone, a progress bar, and "Resume" deep-links for incomplete steps. View is public (unauthenticated visitors see descriptions but no Resume buttons).
- [x] `TutorialTooltip.vue` reusable component with fade-in animation, "Got it" dismiss button, Escape-key support, and 30-second auto-dismiss.
- [x] `useTutorialContext` composable for milestone state management, completion fetching, and completing milestones from any view.
- [x] All tooltip and tutorial UI strings available in English, Slovak, and German via vue-i18n.
- [x] Contextual tooltip overlays on building-detail first visit and first grid-editor open now use `TutorialTooltip.vue` + `useTutorialContext`, including dismiss persistence via `markTutorialMilestoneComplete`.
- [x] Tutorial milestones now grant dedicated master-ranking tutorial bounties (once per lifetime), and `/tutorial` completion display uses bounty-award status as the source of truth with a “Bounty Earned ✓” badge.

### Player profile and statistics page (100% complete)

- [x] Add a `/player/:id` public profile page showing: player display name, join date (game year), total company equity, current leaderboard rank, industries active in, number of cities with buildings, and total products sold across all ticks.
- [x] Include a "Hall of Fame" panel on the profile page listing the player's highest single-tick revenue, largest single acquisition, highest brand quality ever achieved.
- [x] Allow players to add a short bio (max 160 chars) visible on their profile page.
- [x] Add a custom profile badge unlocked by specific master-ranking bounty completions, visible on the leaderboard table.
- [x] Rank history chart over last 365 ticks.
- [x] Export statistics as PDF or CSV.

### Audits (100% complete)

- [x] In root directory create audits folder, and every week do the audit of the security. List all potential risks and create the action plan to resolve them. The main focus should be on question: Can one player gain unfair advantege of another player by executing an api call or exploting some unfair game mechanics?

### Media house (100% complete)

- [x] When media house is in the upgrade, allow the marketing units to configure it
