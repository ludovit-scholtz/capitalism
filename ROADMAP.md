# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Onboarding

- [x] Remove the company name selection and person name selection from the onboarding. Keep auto generated name, but hide the name selection. Users can change the name later in the game.
- [x] On mobile, when user clicks on any button in the onboarding after he scrolled little down, the screen goes to top which creates undesired confusion.
- [x] In the purchase factory step and purchase sales shop, make one of the recommended choices selected one, so that user can just click continue.
- [x] In the sales shop purchase onboarding step show on map the distance from the factory. The main point is to optimize the distance between the factory while tuning up the retail index

### Copy-Paste units

- [x] Copy-paste of the units works fine on desktop with keyboard, however this feature is not available for small devices at the moment. Add on the grid page also copy and paste buttons when building is in editation mode.

### News

- [x] Add pagination to each category of news. At each category make sure to show top 10 recent news. At the moment if there if there is more then 10 news from reporting category and i select the changelog category, it does not show any items.

### Emails

- [x] Setup email using Email Communication Services in azure
- [x] Create templates for emails using handlebars rendered from the html template file. All emails must have same design. Create professional looking email template. Make sure each localization works for every supported language.
- [x] When user never received email (create flag in the master database), send him the registration email. In the email also write his current url address which he accessed.
- [x] Send users email on weekly basis in friday noon with the report where will be listed all their active game servers, their profit and ranking in the game server, then the master server bounties points they collected in past week, and if there are any news from the changelog within a week add it there.
- [x] Store the language preference after the user logs in to the system to the master server database, and use that localization for the emails to be sent. If no language is set in the database use English.

### Account deletion

- [x] Add a Danger Zone in the master frontend user settings with a delete account section that requires the user to confirm by entering their email address, lists what they lose (game progress, tokenized gold deposits, future tokenized gold rewards) and warns that all game-server data will be removed.
- [x] Do not delete immediately: mark the account for deletion with a 24-hour cooldown that the user can cancel, and send a request email and a final confirmation email (both polite, localized, with the master portal link).
- [x] When purging game-server data destroy all of the user's buildings except banks, transfer banks to the government entity, and set deposit interest to 0% and lending interest to 20%.
- [x] Keep it secure (only the user can delete their own account) with backend and e2e test coverage, and update the in-game user documentation.

### Security audit findings

- [x] Fix company-merge tax evasion in `Api/Types/Mutation.CompanyMerge.cs`: clamp the merge-time tax to the target's available balance, assert the `TryDebit` result, and record only the amount actually paid so cash-poor companies cannot merge to escape tax. Regression test for the cash-poor merge scenario added.
- [x] Harden MasterApi auth rate limiting in `MasterApi/Security/AuthRateLimitMiddleware.cs` to parse selected GraphQL root fields across named operations and JSON-array batched bodies, matching the game API, so batched/named login and register requests cannot bypass the per-IP limiter. Regression tests cover the bypass shapes.
- [x] Make GraphQL batching explicit on MasterApi in `MasterApi/Security/GraphQlRequestSecurityMiddleware.cs`: every batch item is iterated and the same introspection, depth, and complexity checks the game API performs are applied before execution. Regression tests added.
- [x] Remove raw JWT persistence from the game frontend `frontend/src/stores/auth.ts` (`auth_token`/`auth_expires` in `localStorage`) and rehydrate gameplay sessions from the cookie session instead. Tests for login, OIDC callback, reload, and logout added.
- [x] Audit and standardize the remaining unchecked `CompanyBankingService.TryDebit`/`TryCredit` call sites in `Api/Engine/Phases` for the same ignored-failure-plus-phantom-ledger pattern, adopting the `Math.Min(balance, due)` clamp-and-record convention used by `TaxPhase` (applied to `SupplyContractFulfillmentPhase` under-delivery penalties and `TradeRoutePhase` shipping costs).
