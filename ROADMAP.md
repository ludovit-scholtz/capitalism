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

### Flutter mobile app

The scaffold in `projects/flutter_app` (routing, nav drawer, theming, i18n plumbing, GraphQL client, Bearer-token auth state) was generated without the Flutter SDK available and ships every screen as an empty placeholder. Native platform runners still need generating, and each screen below needs its real implementation, ported from the matching Vue view in `projects/frontend/src/views/`.

- [x] Generate native platform runners for `projects/flutter_app` by running `flutter create .` (Android/iOS/Web/Windows) now that the Dart source, pubspec, and l10n config exist; verified the committed `lib/`, `pubspec.yaml`, `README.md`, `analysis_options.yaml`, and `.gitignore` were untouched (checksum diff before/after). `flutter analyze` is clean, `flutter build web` succeeds, and `flutter test` (7 tests, incl. drawer/bottom-nav navigation coverage) passes. Building for Android/iOS/Windows still needs their native toolchains (Android SDK; Visual Studio "Desktop development with C++" workload) installed on the dev machine — not available in the environment this was verified in.
- [x] Wire `url_launcher` for the Discord nav link and build the Chat side-panel/screen in `lib/core/widgets/app_shell.dart` (`_handleTap`): Discord now opens via an injectable `UrlOpener` (`lib/core/services/url_opener.dart`), and Chat opens `lib/features/chat/chat_panel.dart` as a modal bottom sheet, matching the web's non-routed side panel. Covered by `test/feature_actions_test.dart`.
- [x] Build a native Biatec OIDC sign-in flow (`lib/core/auth/biatec_oidc_service.dart`, using `flutter_web_auth_2` — Custom Tabs/`ASWebAuthenticationSession` on Android/iOS, system browser + loopback listener on Windows/Linux) to complement the Bearer-JWT auth already wired in `lib/core/auth/auth_state.dart`. Replicates the web's implicit-flow protocol (`response_type=id_token`, `response_mode=query`) and the same client-side state/nonce/issuer/audience checks from `projects/frontend/src/stores/auth.ts`; the id_token becomes the Bearer token directly, no backend changes needed. Wired into a "Sign in with Biatec" button on the Sign In screen. Android needs the `com.linusu.flutter_web_auth_2.CallbackActivity` intent filter added to `AndroidManifest.xml` (done) for the `io.biatec.capitalism` callback scheme; iOS needs no manifest entry. Covered by `test/biatec_oidc_service_test.dart` (state/nonce/issuer/audience validation, plus a test that forces `debugDefaultTargetPlatformOverride` through Android/iOS/Windows to assert the redirect URI shape differs correctly per platform) and `test/feature_actions_test.dart` (button wiring). Still open: a real Biatec IdP to test the flow against end-to-end (validated via fakes only), and macOS/Linux weren't exercised.
- [x] Implement the Home screen (`lib/features/home/home_screen.dart`, mirrors `HomeView.vue`): hero + auth-dependent CTA ("Get Started" / "Go to Dashboard" — the web's third CTA state additionally needs `player.onboardingCompletedAtUtc`, not available until the `me` query is wired up), tick/tax-rate status cards, and a top-5 leaderboard preview, all fetched via one `HomeStatus` GraphQL query (`gameState { currentTick taxRate }`, `rankings { displayName totalWealthUsd }` — field names verified against `Api/Data/Entities/GameState.cs` and `Api/Types/Query.Types.Rankings.cs`) with loading/error/retry states. Covered by `test/navigation_test.dart`.
- [ ] Implement the Sign In screen (`lib/features/auth/auth_screens.dart` `LoginScreen`, mirrors `LoginView.vue`).
- [ ] Implement the Forgot Password screen (`lib/features/auth/auth_screens.dart` `ForgotPasswordScreen`, mirrors `ForgotPasswordView.vue`).
- [ ] Implement the Reset Password screen (`lib/features/auth/auth_screens.dart` `ResetPasswordScreen`, mirrors `ResetPasswordView.vue`).
- [ ] Implement the Auth Callback screen (`lib/features/auth/auth_screens.dart` `AuthCallbackScreen`, mirrors `AuthCallbackView.vue`).
- [ ] Implement the Onboarding screen (`lib/features/onboarding/onboarding_screen.dart`, mirrors `OnboardingView.vue`).
- [ ] Implement the Dashboard screen (`lib/features/dashboard/dashboard_screen.dart`, mirrors `DashboardView.vue`).
- [ ] Implement the News screen (`lib/features/news/news_screens.dart` `NewsScreen`, mirrors `NewsView.vue`).
- [ ] Implement the Notifications screen (`lib/features/news/news_screens.dart` `NotificationsScreen`, mirrors `NotificationsView.vue`).
- [ ] Implement the Contracts screen (`lib/features/economy/contracts_screen.dart`, mirrors `ContractsView.vue`).
- [ ] Implement the Leaderboard screen (`lib/features/leaderboard/leaderboard_screens.dart` `LeaderboardScreen`, mirrors `LeaderboardView.vue`).
- [ ] Implement the Player Profile screen (`lib/features/leaderboard/leaderboard_screens.dart` `PlayerProfileScreen`, mirrors `PlayerProfileView.vue`).
- [ ] Implement the Cities screen (`lib/features/cities/cities_screens.dart` `CitiesScreen`, mirrors `CitiesView.vue`).
- [ ] Implement the World Map screen (`lib/features/cities/cities_screens.dart` `WorldMapScreen`, mirrors `WorldMapView.vue`).
- [ ] Implement the Building Market screen (`lib/features/buildings/buildings_screens.dart` `BuildingMarketScreen`, mirrors `BuildingMarketView.vue`).
- [ ] Implement the Buy Building screen (`lib/features/buildings/buildings_screens.dart` `BuyBuildingScreen`, mirrors `BuyBuildingView.vue`).
- [ ] Implement the Building Detail screen (`lib/features/buildings/buildings_screens.dart` `BuildingDetailScreen`, mirrors `BuildingDetailView.vue`).
- [ ] Implement the Sell Building screen (`lib/features/buildings/buildings_screens.dart` `SellBuildingScreen`, mirrors `SellBuildingView.vue`).
- [ ] Implement the Encyclopedia screen (`lib/features/encyclopedia/encyclopedia_screens.dart` `EncyclopediaScreen`, mirrors `ManufacturingEncyclopediaView.vue`).
- [ ] Implement the Resource Detail screen (`lib/features/encyclopedia/encyclopedia_screens.dart` `ResourceDetailScreen`, mirrors `ResourceDetailView.vue`).
- [ ] Implement the Exchange screen (`lib/features/exchange/exchange_screens.dart` `GlobalExchangeScreen`, mirrors `GlobalExchangeView.vue`).
- [ ] Implement the Stocks screen (`lib/features/exchange/exchange_screens.dart` `StockExchangeScreen`, mirrors `StockExchangeView.vue`).
- [ ] Implement the Trade Stock screen (`lib/features/exchange/exchange_screens.dart` `StockTradingScreen`, mirrors `StockTradingView.vue`).
- [ ] Implement the Forex screen (`lib/features/exchange/exchange_screens.dart` `ForexExchangeScreen`, mirrors `ForexExchangeView.vue`).
- [ ] Implement the City Overview tab (`lib/features/city/city_tab_screens.dart` `CityOverviewScreen`, mirrors `CityOverviewTab.vue`).
- [ ] Implement the City Economy tab (`lib/features/city/city_tab_screens.dart` `CityEconomyScreen`, mirrors `CityEconomyTab.vue`).
- [ ] Implement the City Buildings tab (`lib/features/city/city_tab_screens.dart` `CityBuildingsScreen`, mirrors `CityBuildingsTab.vue`).
- [ ] Implement the City Market tab (`lib/features/city/city_tab_screens.dart` `CityMarketScreen`, mirrors `CityMarketTab.vue`).
- [ ] Implement the City Contracts tab (`lib/features/city/city_tab_screens.dart` `CityContractsScreen`, mirrors `CityContractsTab.vue`).
- [ ] Implement the City Competitors tab (`lib/features/city/city_tab_screens.dart` `CityCompetitorsScreen`, mirrors `CityCompetitorsTab.vue`).
- [ ] Implement the Ledger screen (`lib/features/company/company_screens.dart` `LedgerScreen`, mirrors `LedgerView.vue`).
- [ ] Implement the Company Contracts screen (`lib/features/company/company_screens.dart` `CompanyContractsScreen`, mirrors `CompanyContractsView.vue`).
- [ ] Implement the Company Settings screen (`lib/features/company/company_screens.dart` `CompanySettingsScreen`, mirrors `CompanySettingsView.vue`).
- [ ] Implement the Company Research screen (`lib/features/company/company_screens.dart` `CompanyResearchScreen`, mirrors `CompanyResearchView.vue`).
- [ ] Implement the Personal Ledger screen (`lib/features/company/company_screens.dart` `PersonalLedgerScreen`, mirrors `PersonalLedgerView.vue`).
- [ ] Implement the Banking (loan marketplace) screen (`lib/features/banking/banking_screens.dart` `LoanMarketplaceScreen`, mirrors `LoanMarketplaceView.vue`).
- [ ] Implement the Bank Management screen (`lib/features/banking/banking_screens.dart` `BankManagementScreen`, mirrors `BankManagementView.vue`).
- [ ] Implement the Request Loan screen (`lib/features/banking/banking_screens.dart` `BankLoanRequestScreen`, mirrors `BankLoanRequestView.vue`).
- [ ] Implement the Bank Statement screen (`lib/features/banking/banking_screens.dart` `BankStatementScreen`, mirrors `BankStatementView.vue`).
- [ ] Implement the Campaigns / Market Intelligence screen (`lib/features/market/market_screens.dart` `MarketIntelligenceScreen`, mirrors `MarketIntelligenceView.vue`).
- [ ] Implement the Market Dashboard screen (`lib/features/market/market_screens.dart` `MarketDashboardScreen`, mirrors `MarketDashboardView.vue`).
- [ ] Implement the Energy Market screen (`lib/features/market/market_screens.dart` `EnergyMarketScreen`, mirrors `EnergyMarketView.vue`).
- [ ] Implement the Global Events screen (`lib/features/market/market_screens.dart` `GlobalEventsScreen`, mirrors `GlobalEventsPanel.vue`).
- [ ] Implement the Marketing Analytics screen (`lib/features/market/market_screens.dart` `MarketingAnalyticsScreen`, mirrors `MarketingAnalyticsView.vue`).
- [ ] Implement the Trade Routes screen (`lib/features/trade/trade_screens.dart`, mirrors `TradeRoutesView.vue`).
- [ ] Implement the Tutorial screen (`lib/features/tutorial/tutorial_screen.dart`, mirrors `TutorialView.vue`).
- [ ] Implement the Operations Overview screen (`lib/features/operations/operations_screens.dart` `OperationsOverviewScreen`, mirrors `OperationsOverviewView.vue`).
- [ ] Implement the Operations Money Flow screen (`lib/features/operations/operations_screens.dart` `OperationsMoneyFlowScreen`, mirrors `OperationsStatisticsView.vue`).
- [ ] Implement the Operations Product Analytics screen (`lib/features/operations/operations_screens.dart` `OperationsProductAnalyticsScreen`, mirrors `OperationsAnalyticsView.vue`).
- [ ] Implement the Operations News screen (`lib/features/operations/operations_screens.dart` `OperationsNewsScreen`, mirrors `OperationsNewsView.vue`).
- [ ] Implement the Operations Players screen (`lib/features/operations/operations_screens.dart` `OperationsPlayersScreen`, mirrors `OperationsPlayersView.vue`).
- [ ] Implement the Operations Player Detail screen (`lib/features/operations/operations_screens.dart` `OperationsPlayerDetailScreen`, mirrors `OperationsPlayerDetailView.vue`).
