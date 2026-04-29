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

### Context switching (100% complete)

- [x] When user logs in, the city should be selected according to the player most used city. However i see ?? in the navbar for the city selection.
- [x] Remove from the new building flow the city selection. City must be selected with the context switching.
- [x] In the city map remove the cities switcher. Use the navbar context switcher to switch between the cities.

### Currencies and bank accounts (0% complete)

- [ ] Investigate and fix why the current balance at the bank account does not match the balance of the last item in the bank statement.
- [ ] Every operation which changes the bank account balance must be listed in the ledger entry and visible in the statement

### Number formatting (100% complete)

- [ ] In the number formatting component define also the size of the field the frontend has to show the number. If there is enough space, show number 12376909 as 12,376,909 and if there is limitted space, show it as 12M.
- [ ] Add to the title the original number to be formatted and currency after it. When player stay with mouse over the number, he should see the original number input.

### Power plants (0% complete)

- [ ] When I edit powerplant building, and click the empty unit in the grid, I do not see any options to setup any of the unit. Make it to work similarily as the factory for example where every unit will have special feature.

### Units (0% complete)

- [ ] Do not show bank account change if unit is selected in a grid while editing the building
- [ ] When new unit is selected in the grid, automatically select that unit. So if i create new purchase unit in position 1,1 i do not want the user to click on that unit again to configure it.
- [ ] Fix css styles after tailwind migration. Make sure the design is professional.

### Audits (0% complete)

- [ ] In root directory create audits folder, and every week do the audit of the security. List all potential risks and create the action plan to resolve them. The main focus should be on question: Can one player gain unfair advantege of another player by executing an api call or exploting some unfair game mechanics?

### Media house (20% complete)

- [ ] When media house is in the construction, allow the marketing units to configure it.
- [ ] When media house is in the construction, do not make any caluclations for the marketing units, only charge the unit labor and energy costs.

### Mining (20% complete)

- [ ] Make sure every mining land property has the custom resource defined what is in that property. It must have the quality and resource amount defined. 
- [ ] For each resource must be always available at least one property in each city
- [ ] When user buys the mining property using the buy building flow, make sure to show the resource quality and quantity available at the property land. 
- [ ] Make sure user can filter the land by the resource type when buying the mining property.
- [ ] Make sure the prices for the purchase of the land is very expensive ~ $20M to $200M depending on the quality of the resource and the amount of resource there is available to be mined.

### Appartment and commercial buildings (10% complete)

- [ ] I do not see the appartment building size. Make sure when buying the property the size of the commercial building or appartment building is clearly stated. Fix the current buildings which does not have the total area filled in.
- [ ] Occupancy must be always a number. When there is no occupancy there must be 0%
- [ ] I do not see the occupancy to be changed. Make sure the occupancy rules are applied.

### Encyclopedia (0% complete)

- [ ] The resources pictures are very big. Make it 6 columns on wide screen please.
- [ ] Create section for the help with the game play. Update copilot instructions with any change to the basic flow also update the documentation in the encyclopedia.
- [ ] Create help section with onboarding help. Please document the onboarding and create also pictures so that users can easier be onboarded
- [ ] Create help section with manufacturing unit setup. Please document the manufacturing setup and create also pictures so that users can understand the game better

### Referal program (0% complete)

- [ ] Create page in master frontend to setup the referal code. Allow referal code to be filled only once. Make sure the existing referal code is used.
- [ ] Create page in master frontend for any user to be a referal. If user wants to be a referal he must fill in his name, and tax domicil.
- [ ] First referal code is autogenerated - 8 alphanumerical string. User can create multiple referal codes.
- [ ] In the referal dashboard show the number of registered users under the specific referal code, number of second level referals registrations, number of active subscriptions, and number of second level active referal subscriptions
