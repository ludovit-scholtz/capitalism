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

### Security improvements

- [x] Fix company-merge tax evasion in `projects/Api/Types/Mutation.CompanyMerge.cs`: clamp the merge-time tax to the target's available balance (`Math.Min(balance, taxAmount)`), assert the `TryDebit` result, and record only the amount actually paid so cash-poor companies cannot merge to escape tax. Regression test for the cash-poor merge scenario added.
- [x] Harden MasterApi auth rate limiting in `projects/MasterApi/Security/AuthRateLimitMiddleware.cs` to parse selected GraphQL root fields across named operations and JSON-array batched bodies, matching the game API, so batched/named login and register requests cannot bypass the per-IP limiter. Regression tests cover the bypass shapes.
- [x] Make GraphQL batching explicit on MasterApi in `projects/MasterApi/Security/GraphQlRequestSecurityMiddleware.cs`: every batch item is iterated and the same introspection, depth, and complexity checks the game API performs are applied before execution. Regression tests added.
- [x] Remove raw JWT persistence from the game frontend `projects/frontend/src/stores/auth.ts` (`auth_token`/`auth_expires` in `localStorage`) and rehydrate gameplay sessions from the cookie session instead. Tests for login, OIDC callback, reload, and logout added.
- [x] Audit and standardize the remaining unchecked `CompanyBankingService.TryDebit`/`TryCredit` call sites in `projects/Api/Engine/Phases` for the same ignored-failure-plus-phantom-ledger pattern, adopting the `Math.Min(balance, due)` clamp-and-record convention used by `TaxPhase` (applied to `SupplyContractFulfillmentPhase` under-delivery penalties and `TradeRoutePhase` shipping costs).
- [x] Upgrade DOMPurify to ≥3.4.11 in both frontends: run `npm audit fix` in `projects/frontend` and `projects/master-frontend`, update the semver lower bound to `"^3.4.11"` in both `package.json` files, and confirm zero production vulnerabilities with `npm audit --omit=dev`. Resolves 8 CVEs (GHSA-x4vx-rjvf-j5p4 and related) in the library used to sanitize news and support-ticket HTML before `v-html` rendering. Current app usage (standard `sanitize()` mode) is not directly exploitable, but the dependency must be current before any future use of IN_PLACE or setConfig. Verified fixed (3.4.11 installed) in the 2026-07-07 audit.
- [x] Upgrade Vite to ≥7.3.4 in both frontends (`npm audit fix` resolves this alongside DOMPurify): fixes GHSA-v6wh-96g9-6wx3 (NTLMv2 hash disclosure via UNC path in launch-editor on Windows) and GHSA-fx2h-pf6j-xcff (server.fs.deny bypass on Windows alternate paths). Dev-only risk but affects developer workstations running `npm run dev` on Windows. Verified fixed (7.3.5 installed) in the 2026-07-07 audit.
- [x] Enforce OIDC HTTPS metadata outside Development: `"RequireHttpsMetadata": true` set in the base `appsettings.json` of both `projects/Api` and `projects/MasterApi`, `MasterApi/Security/BiatecOidcOptions.RequireHttpsMetadata` now defaults to `true`, and a fail-fast non-Development/Testing startup guard (`RequiredSecretsStartupGuard.TryGetUnsafeOidcHttpsMetadataReason`) blocks startup when `BiatecOidc:Enabled=true` and either `RequireHttpsMetadata=false` or the Authority is not HTTPS. Regression tests added in `Api.Tests/OidcHttpsMetadataStartupGuardHostTests.cs` and `MasterApi.Tests/OidcHttpsMetadataStartupGuardHostTests.cs`.
- [x] Added a weekly scheduled (cron) trigger to `api-ci-cd.yml`, `frontend-ci-cd.yml`, and `deploy-stage-k8s.yml` (which builds and deploys MasterApi/master-frontend) so Docker images are rebuilt and redeployed even when `main` has no code pushes, ensuring runtime security patches (e.g. CVE-2026-45591, fixed in .NET 10.0.9) ship on a regular cadence. Production master deploy remains manual (`workflow_dispatch`) by existing design; not changed here.
- [x] Fixed nginx `add_header` inheritance in `projects/frontend/nginx.conf` and `projects/master-frontend/nginx.conf`: every location that declares its own `add_header` (static assets, `/sw.js`, `/health`) now repeats the full security-header set so asset responses keep `nosniff` and HSTS; added the missing `Permissions-Policy` header to the game frontend. Verified with `npm run test:security-headers` in both frontends.
- [ ] Clear esbuild advisory GHSA-g7r4-m6w7-qqqr (dev-server arbitrary file read on Windows, low, dev-tooling only) in both frontends at the next dependency cycle: run `npm audit fix` or bump Vite to a release that lifts esbuild past 0.28.0, then re-run `npm audit` to confirm zero advisories. Attempted 2026-07-07 via an `overrides` pin to `esbuild@^0.28.1`, but `npm install` could not complete in the sandboxed audit environment (registry fetches for the platform-specific `@esbuild/*` optional packages were silently dropped mid-run); needs a normal dev machine or CI runner with full network access.
