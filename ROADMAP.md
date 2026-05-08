# Capitalism Roadmap

Create a fun game on style of the capitalism II game. This game is economic simulation where players can experience price elasticity, resource scarcity, resource oversupply, different competition types, marketing, product quality, difficulties with scaling up the companies, and other base economic factors.

It will use real world map. The game will start in single city and later other cities will be added.

## Issues to work on

### Operations Dashboard in Game frontend (100% complete)

- [x] Merge /operations/statistics and /admin pages. Keep  /operations/statistics. Remove /admin from the menu. Add to the operations all other features from the admin such as newspaper management.
- [x] Organize Operations Dashboard (/admin) to level 2 menu, add proper routing, and create components. Make sure it is not shown as big single page, but split into multiple pages.
- [x] Fix News & changelog publisher style. The form is stretched along the whole list of news items. Create multiple pages for this like it is in the ticket support system.
- [x] Players & intervention tools - Make sure to show the table of the users and on user detail page show the actions
- [x] Create page for the game statistics where in one columns will be items that distributes the money such as the public sales buildings, the rent, IPOs, or other money distribution sections. In the other column will be where people are paying money - taxes, fx fees, labour costs, energy, research, stock exchange fees or others..
- [x] Create admin page with detailed statistic for every product. Do it in table. Make sure the table is exportable sortable and filterable. In the table will be the product insights such as the aggregated costs of materials, energy, labor to build the product, number of products produced, sold, market size, saturation, marketing, and research details.
- [x] Backend: Wire `operationsStatistics` query to real LedgerEntry aggregations for live money flow data
- [x] Backend: Wire `adminProductAnalytics` query to real ProductionRecord/PublicSalesRecord aggregations

### Endgame (100% complete)

- [x] Add to the top personal account the 5 most richest persons in the real world with their current estimated wealth
- [x] The game server will stop when any of the players will be the most rich
- [x] When game server is stopped, players cannot do any in game operations - no forex trades, no stock trades, no amm trading, ticks engine does not process new tick. The newslatter is published with the game details and top personal account ranking.
- [x] Update the documentation and set a game goal for the players to become the richest persons in the world.

### Onboarding (50% complete)

- [ ] Do not show invitation message with referal code right up to the point when user logs in to the system.
- [ ] After the code has been applied and stored to the database, remove it from the game pinia store.
- [x] Before user first sign in to the game make sure to fill in the referal code. Show link (and allow copy on one click) in the master frontend in the referal section where users can refer user for the game server. When user comes to this link, make sure the referal code is stored in the pinia state, and stored to the user account when he first logs in. Before user logs in, show him that he is using specific referal code and he will get 10% discount for in game purchases.
- [x] Create better Company name generator. Find npm package with the word list, and do a proper name generation with the combination of two words. Make sure the company names sounds great.
- [ ] Create name generator for personal account name. Find npm package with the names wordlist and do a combination of the Firstname, Middlename and Last name. Allow players to change the personal account name later. In ranking show the personal account name, not the oidc name please. In the form to change name, tell people not to use the real name. Make sure the personal name is generated in the onboarding in the IPO step when player picks the company name. Make sure the personal account name is showned in the ranking. Make sure player can change the personal account name in the player settings. Store the personal account name in the master database so that the personal account player name is the same in all game servers. If the personal account name already exists in the master database after new game onboarding, do not change it, and make sure the personal account name is preserved.

### Authorization

- [ ] Do login/password authorizaiton only if configuration allows it. Make it disabled by default, but make sure to enable it in the tests. Do this on game frontend, master frontend and both backends as well. When biatec oidc is the only authorization method, when user goes to /login page, make sure to automatically follow the authorization process as user would click the authorize with google button.
- [ ] Allow special token based authorizations for bots. Create a form for users to create an API key. Each API key is bound to the personal account and user can impersonalize this key to control his controlled companies. Track the usage of the API keys in the administrators section. Create tests to test also negative scenarios such as user is not allow to control foreign company or he cannot do forex swaps. Make sure the bots console app is using this form of authorization.

### Buildings (75% complete)

- [ ] Improve design of the tabs in building editation mode for public sale unit. The `unit-insight-card recent-activity-panel` div has the top border while `unit-detail-tabs` div has bottom border which creates effect of two horizontal lines. Also `unit-detail-tabs` is touching the tab button bottom border `unit-tab-btn--active`. 
- [ ] Improve design of the tabs in building editation mode for purchase unit. In the first tab add some space between the tabs and content. Add to the basic info also the history of the purchase price and quality of purchased products. In other tabs there is one extra line below the tab headers.
- [ ] The recommended market value in the building sales flow is very low. I think it does not include the property value, and perhaps it does not include also the unit values. Make sure the market price for the building is calculated properly.
- [ ] Do not allow to sell building below 70% of its market value.
- [ ] Add some space below `customer-bank-profile` div.
- [ ] In bank building in `operating-account-row` is too much content that does not fit into the row. Add `Bank Statement Review` in second line or somewhere else.
- [ ] Create a workflow to destroy a building. Make sure the button to destroy the building is in the sell building form and show also the refund how much user will receive. When building is destroyed, return the user 80% of the building property value. Make the property available for purchase again. When bank loan is not paid set it for sale for the property market price minus 10%. When the debt from missed payments is not paid in 3 game days (72 ticks), destroy the building and pay any remaining debt from the sale of property to the bank owner.

### News (100% complete)

- [x] Add button to news and changelog to mark all news as read.
- [x] Changelog.csv news are not imported to the database. Make sure that after every restart every changelog news item is imported. If any error occurs during the import log it and skip the import of that one item. Do it more resilient to errors.

### Banks (50% complete)

- [x] Investigate why bank statement latest row does not equal to current balance on the bank account. Perhaps it is related to the loan payments as I do not see the loan received nor any of the loans currently on the bank account statement.
- [x] When bank loan is not paid set it for sale for the property market price minus 10%. When the debt from missed payments is not paid in 3 game days (72 ticks), destroy the building and pay any remaining debt from the sale of property to the bank owner.
- [x] When bank loan is not paid, make sure to notify user using the notifications that he has pending debt to the bank. When user goes to the bank, make sure the pending debt amount is clearly visible and also pending time until the building in the collateral will be destroyed.
- [ ] When bank loan is unpaid and building goes for sale, make sure to put it on sale in proper currency. When builing in Prague which costs 10M CZK is collateralized in USD bank, the collateral amount is correctly calculated and allows to lend 300k USD. However when unpaid loan is hit, make sure to sell it not for 300k USD but for 10M CZK. After the building is sold on market make sure to settle the loan payments in correct currency - make sure to do the swap if required.
- [ ] When there is unpaid loan and building is put on sale, user can cancel the sale of the building. Do not allow user to cancel sale of the building which is collateralized for loan and loan has missed payments.

### FX Exchange (100% complete)

- [ ] Make sure to show the rate in the stronger currency. The currency strength is EUR,USD,CNY,GBP,INR,CZK. So when user has selected in the context switcher Prague the CZK currency it will show USDCZK and EURCZK numbers. When Vienna and EUR is selected make sure to show rates for EURUSD and EURCZK. Show the pair also in the rate list as it is common in standard forex. 
- [ ] Move the rates table above the currency pair chart

### Fix city selection (100% complete)

- [ ] When I switch city to city where i dont have any factory, log out and log in later with biatec oidc, i want the context switcher automatically switch to my main city where I have the most factories

### Ranking (100% complete)

- [ ] Move link from game ranking to master ranking next to richest players and richest companies

### Optimize for mobile (75% complete)

- [ ] Make sure the design is smooth on small mobile devices for all pages. Mainly the content should be visible without the page scroll to the right.
- [ ] Make sure the design is smooth on tablet sized screens.
- [ ] Make sure the design is smooth on Full HD devices
- [ ] Make sure the design is smooth on 4K screens

### In-game tutorials and interactive help (85% complete)

- [x] Add a `TutorialProgress` entity tracking per-player completion of guided tutorial milestones: first resource sold, first B2B trade, first loan taken, first competitor observed in market intelligence, first brand established.
- [x] `getTutorialProgress` GraphQL query returns all 5 milestones with `isCompleted` and `completedAtUtc` for the authenticated player. `markTutorialMilestoneComplete` mutation persists a milestone completion idempotently with full validation.
- [x] Add a `/tutorial` view accessible from the navigation that lists all 5 tutorial milestones with completion status, bounty points per milestone, a progress bar, and "Resume" deep-links for incomplete steps. View is public (unauthenticated visitors see descriptions but no Resume buttons).
- [x] `TutorialTooltip.vue` reusable component with fade-in animation, "Got it" dismiss button, Escape-key support, and 30-second auto-dismiss.
- [x] `useTutorialContext` composable for milestone state management, completion fetching, and completing milestones from any view.
- [x] All tooltip and tutorial UI strings available in English, Slovak, and German via vue-i18n.
- [ ] Contextual tooltip overlays on the dashboard and building detail views (first grid-editor open, first building detail visit) using `TutorialTooltip.vue` and `useTutorialContext` — integration deferred to next increment.
- [ ] The tutorials does not grant the points to the master ranking at the moment. Create bounty in the master ranking for every tutorial. Make sure that the tutorial bounties are counted only once per lifetime per user. Make sure the tutorial is marked as completed if the bounty is awarded.

### Player profile and statistics page (100% complete)

- [x] Add a `/player/:id` public profile page showing: player display name, join date (game year), total company equity, current leaderboard rank, industries active in, number of cities with buildings, and total products sold across all ticks.
- [x] Include a "Hall of Fame" panel on the profile page listing the player's highest single-tick revenue, largest single acquisition, highest brand quality ever achieved.
- [x] Allow players to add a short bio (max 160 chars) visible on their profile page.
- [x] Add a custom profile badge unlocked by specific master-ranking bounty completions, visible on the leaderboard table.
- [x] Rank history chart over last 365 ticks.
- [x] Export statistics as PDF or CSV.

### Audits (0% complete)

- [ ] In root directory create audits folder, and every week do the audit of the security. List all potential risks and create the action plan to resolve them. The main focus should be on question: Can one player gain unfair advantege of another player by executing an api call or exploting some unfair game mechanics?

### Media house (0% complete)

- [ ] When media house is in the upgrade, allow the marketing units to configure it
