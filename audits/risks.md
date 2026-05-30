# Risk Register

## Identity and Credential Boundaries

The repository relies on JWTs, scoped API keys, password login, OIDC, bot automation credentials, and service-to-service secrets. The main risk in this category is allowing a caller to act outside the identity boundary or to abuse authentication flows until an account is compromised.

- **Password-auth abuse and rate-limit bypass**: Partially mitigated. `projects/Api/Security/AuthRateLimitMiddleware.cs` now parses selected GraphQL root fields across named operations and JSON-array batched bodies, and trusted-proxy regression tests cover spoofed forwarded-header rotation. Residual risk: `projects/MasterApi/Security/AuthRateLimitMiddleware.cs` still classifies auth traffic via `operationName` or string matching and only deserializes single JSON-object bodies, so named-operation and batched-body login/register requests can still avoid the MasterApi per-IP limiter.
- **Account enumeration through duplicate registration codes**: Mitigated. Both APIs now return the neutral message `Registration failed.` with normalized public code `REGISTRATION_FAILED`, and the game API regression suite covers duplicate-email timing and response normalization.
- **JWT session revocation**: Mitigated. Both APIs track issued JWT sessions, validate active sessions during token validation, expose logout and logout-all endpoints, and provide admin revocation endpoints plus cleanup hosted services.
- **API key overreach**: Mitigated by `ApiKeyScopes`, deny-by-default `ApiKeyScopeMiddleware`, `BotOwnershipGuard`, and server-side ownership resolution. Residual requirement: every newly added sensitive mutation must be registered in the middleware and covered by negative tests.
- **Master versus game token confusion**: Mitigated by shared token-boundary claims, explicit issuer and token-type checks, and privileged admin resolution from authenticated claims instead of caller-supplied email fields.
- **NPC bot credential reuse**: Mitigated by the empty committed default password, `BotStartupValidator`, and API-key mode support. Residual requirement: non-Development deployments must provide either a real secret or a scoped API key.

## Authorization and Economy Surfaces

Most player-impacting game mutations resolve ownership on the server, but the remaining risk is inconsistent behavior in legacy paths or concurrency-sensitive economy flows where small leaks can become competitive intelligence.

- **Cross-owner mutation abuse**: Mitigated by `[Authorize]`, `ObjectAuthorizationService`, `BotOwnershipGuard`, account-context resolution from the authenticated principal, and ownership checks in finance, lending, building, and shareholder mutations.
- **Object enumeration and balance disclosure**: Mostly mitigated. `NOT_FOUND_OR_NOT_OWNED` normalization plus balance redaction has been applied across building-market, exchange, forex, and bank-transfer mutations. Residual risk: older CRUD surfaces still use non-canonical `ACCESS_DENIED` or object-specific codes in some paths, which enforce ownership but do not always collapse the response contract.
- **Stale-quote or replayed FX execution**: Mitigated by quote nonces, TTL validation, slippage bounds, and concurrency handling in the forex mutation path.
- **Market and collateral race conditions**: Mitigated by optimistic offer versions, collateral locks, and commit-time revalidation. Residual requirement: keep invariant-based race tests when secondary-market, loan, or collateral rules change.
- **Demolition or sale valuation drift from construction pricing**: Mitigated. Building market valuation now derives from the exact recorded lot purchase amount plus the current building construction cost and active-unit replacement cost including upgrade steps. Residual requirement: whenever land, shell, or unit pricing changes, update the shared valuation calculator and keep destroy/sale regression tests aligned so refund arbitrage does not reappear.
- **API-key company-bound trading drift**: Mitigated by root-field scope rules and company binding resolution. Residual requirement: high-value trading mutations must continue to prove both positive scope access and foreign-company denial in tests.

## Business Logic and Currency Arbitrage

The 2026-05-14 business-logic hardening work is now live. The main residual risk in this category is regression if future lending, collateral, or secondary-market changes bypass the shared currency-scoped settlement helpers.

- **Off-floor building transfers through accepted offers**: Mitigated. `acceptBuildingOffer` now revalidates `BuildingMarketValuation.MinimumSalePrice` and rejects underfloor settlements with `OFFER_BELOW_FLOOR`.
- **Defaulted-collateral lender strip via cheap friendly repurchase**: Mitigated. Defaulted collateral sales now reject underfunded lien recovery with `COLLATERAL_LIEN_UNDERFUNDED` before the collateral is released.
- **Loan origination and repayment currency scope**: Mitigated. `acceptLoan`, `repayLoanDebt`, and `LoanRepaymentPhase` now resolve lender and borrower settlement accounts in the loan currency, and regression tests cover no foreign-currency fallback.
- **Defaulted-loan repayment after closing the scheduled account**: Mitigated. `closeCompanyBankAccount` now blocks `Active`, `Overdue`, and `Defaulted` scheduled repayment accounts with `REPAYMENT_ACCOUNT_HAS_UNPAID_LOANS`.
- **Editable pledged buildings**: Mitigated. `storeBuildingConfiguration` rejects pledged-collateral edits with `BUILDING_IS_PLEDGED_COLLATERAL`.
- **Pending building offers are escrowed**: Mitigated. `makeOfferOnBuilding` debits buyer funds immediately and stores escrow amount and currency on the pending offer.
- **Defaulted principal omitted from fresh lending-capacity checks**: Mitigated. Lending integrity regression coverage confirms defaulted unpaid principal still constrains fresh lending capacity.
- **Local-currency lot purchase enforcement**: Mitigated. `PrepareLotPurchaseAsync` debits a company account already in the lot city currency and returns `INSUFFICIENT_LOCAL_CURRENCY_FUNDS` when that balance is missing.
- **Same-currency transfer and account-context enforcement**: Mitigated. `transferFunds` allows only same-currency moves and confines transfers to the active personal or company context.
- **Company-context forex routing**: Mitigated. `executeForexSwap` requires explicit matching-currency source and destination accounts and rejects mismatches with `CURRENCY_MISMATCH`.
- **Forced-sale building currency and FX debt settlement**: Mitigated. defaulted collateral is listed in the building city currency, while debt settlement converts internally into the lending bank currency.

## GraphQL and API Infrastructure Security

GraphQL's flexible query language introduces DoS, schema-disclosure, batching, and pre-execution middleware risks that need explicit server-side controls.

- **Query depth and complexity attacks**: Mitigated. Both APIs configure HotChocolate max execution depth, cost analysis, and max page size. Residual risk: `projects/MasterApi/Security/GraphQlRequestSecurityMiddleware.cs` still only extracts `query` from single JSON-object bodies, so its custom rejection telemetry and normalized responses do not cover JSON-array batched envelopes.
- **GraphQL schema introspection in production**: Mostly mitigated. Both APIs gate Nitro IDE and raw introspection fields outside Development. `projects/Api` now inspects every batched request item in its custom middleware, while `projects/MasterApi` still relies on single-request-body extraction and therefore depends more heavily on the built-in GraphQL stack for batch-shaped requests.
- **GraphQL auth operation detection**: Partially mitigated. `projects/Api` now parses selected GraphQL root fields across named operations and JSON-array batched bodies. Residual risk: `projects/MasterApi/Security/AuthRateLimitMiddleware.cs` still trusts `operationName` and single JSON-object parsing before classifying auth traffic, so named-operation or batched login/register requests can avoid the custom per-IP limiter.
- **Chat spam and abuse**: Mitigated. `ChatRateLimitService` enforces 20 messages per 60 seconds per user, maximum message length is 500 characters, and structured rate-limit errors are returned.
- **CORS open fallback**: Mitigated. `CorsPolicyHelper.IsDevelopmentOpenPolicy()` checks the environment before applying `AllowAnyOrigin()`. Non-Development deployments with an empty `Cors:AllowedOrigins` list reject cross-origin requests with a 403 and startup warning log.

## Rich Content and Browser Surfaces

The frontends intentionally render rich HTML in news and support flows. Browser-side token storage also makes XSS prevention part of the authentication boundary.

- **Player news rendering**: Mitigated by `DOMPurify` in the game frontend before `v-html` renders localized news HTML.
- **Support markdown preview rendering**: Mitigated by the shared MasterApi allowlist sanitizer plus frontend DOMPurify before previews are rendered. Residual requirement: keep expanding the payload corpus when new rich-text features or sinks are introduced.
- **Inline SVG flag rendering**: Mitigated because `CountryFlag.vue` renders SVGs from the static `country-flag-icons` library rather than user-supplied SVG input. This must stay isolated from user-controlled content.
- **Browser JWT storage in `localStorage`**: Open. The game frontend still persists raw `auth_token` and `auth_expires` in `localStorage` during login, register, and OIDC bootstrap even though the gameplay GraphQL client already uses cookie-backed `credentials: include`. The master frontend has narrowed this pattern and no longer persists the raw bearer token the same way. CSP, DOMPurify, revocation, and short token lifetimes reduce the blast radius, but the safer target is eliminating raw token storage from the game frontend as well.
- **Admin rich-text authoring**: Acceptable with controls. The editor is allowed for authoring convenience only while loaded content remains trusted and downstream display paths remain sanitized.

## Dependency and Supply-Chain Hygiene

Package-level security posture is currently clean. Dependency drift remains an operational risk because both frontend and backend ecosystems move quickly.

- **Game frontend dependencies**: Mitigated. `npm audit --omit=dev` and full `npm audit` both report zero vulnerabilities as of 2026-05-16.
- **Master frontend dependencies**: Mitigated. `npm audit --omit=dev` and full `npm audit` both report zero vulnerabilities as of 2026-05-16.
- **.NET package vulnerabilities**: Mitigated. Individual `dotnet list package --vulnerable --include-transitive` scans for `Api`, `Api.Tests`, `MasterApi`, `MasterApi.Tests`, `Shared`, `NPCBot`, and `NPCBot.Tests` report no known vulnerable packages as of 2026-05-16. Solution-level scanning still has tooling friction because the compose project uses legacy package configuration.
- **CVE-2026-40324 / GHSA-qr3m-xw4c-jqw3 HotChocolate stack overflow**: Mitigated. The advisory affects HotChocolate versions before 15.1.14; current projects use 15.1.15.

## Security Assurance and Regression Control

Several major security fixes are now live in code. The remaining risk is silent regression when these boundaries are not locked in by focused tests and generated inventories.

- **Authentication abuse regression**: Partially mitigated. The game API now has named-operation, batched-body, and trusted-proxy coverage, and proxy-trust spoofing is covered on MasterApi. Residual gap: add MasterApi regression tests for named-operation and JSON-array batched auth-rate-limit bypass attempts so its older limiter cannot silently drift.
- **MasterApi news trust boundary regression**: Mitigated by trusted server or privileged admin identity resolution together with integration tests for spoofed requester rejection, draft visibility rules, and service-derived author identity.
- **GraphQL auth and ownership drift**: Mitigated in part by generated `audits/graphql-surface-inventory.json`. Residual gap: keep adding boundary tests whenever a sensitive GraphQL operation is added or an older operation's response contract is normalized.
- **Rich-content sink drift**: Mitigated by generated `audits/frontend-sink-inventory.json`, which keeps `v-html` and `innerHTML` surfaces visible for review before they silently expand.

## Infrastructure and Secrets Management

Default configuration values committed to source create a risk path if production deployments accidentally use local defaults.

- **Placeholder JWT signing keys in source**: Mitigated for non-Development. Both APIs have a startup guard that throws `InvalidOperationException` outside Development/Testing when `Jwt:SigningKey` is missing, short, or matches a placeholder.
- **Database connection strings and root administrator emails in source**: Mitigated for non-Development. `appsettings.json` now uses `__SET_IN_ENV__` connection-string placeholders and `MasterApi` blocks non-Development startup when root administrator emails are missing or placeholder values.
- **Admin seed password in source**: Mitigated for non-Development. `projects/Api/appsettings.json` and `SeedDataOptions` now use `__SET_IN_ENV__`, and `Program.cs` blocks non-Development startup when `Auth:PasswordAuthEnabled=true` and the admin password is missing or placeholder. Development retains warning-only behavior for local bootstrapping.
- **Local Docker Compose credentials**: Accepted local-development risk. Compose defaults are still human-readable and must not be reused for production deployments. The production startup guards cover the application-level connection-string path.

## HTTP Security Headers

Browser-security HTTP headers are now configured for both shipped frontend deployments.

- **Game frontend security headers**: Mitigated. `projects/frontend/nginx.conf` sets HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, and Permissions-Policy.
- **Master frontend security headers**: Mitigated. `projects/master-frontend/nginx.conf` now sets HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, and Permissions-Policy.

## Account Lifecycle and Recovery

Account lifecycle controls reduce the impact of credential loss and token compromise.

- **Password reset flow**: Mitigated. MasterApi implements time-limited email reset endpoints with `PasswordResetThrottleService` rate limiting and token expiry. Game API has no independent password reset and relies on MasterApi or OIDC re-linkage.
- **Explicit logout and admin session termination**: Mitigated. Both APIs expose current-session logout, logout-all, active session listing, and admin revoke-all endpoints backed by persistent session/revocation data.

## Email and Notifications

The email system sends registration, weekly report, support, and account-deletion messages. Email addresses are personal data and must stay private, and outbound mail must not become a spam or enumeration vector.

- **Player email-address disclosure**: Mitigated. Email addresses are private and exposed only to the owning player (via the authenticated `me` query) or to administrators. All cross-player email-bearing surfaces in MasterApi (`goldTokenBalances`, `goldTokenTransactions`, `goldTokenDepositRequests`, `goldTokenWithdrawalRequests`, support admin queries) gate on `BuildGameAdministrationAccessAsync(...).CanAccessEveryGameDashboard`. The public `gameNewsFeed` query previously returned the author `createdByEmail`/`updatedByEmail` to any caller; it now resolves viewer privilege on every request and redacts those author addresses for non-administrators (only trusted servers or admins receive them). The weekly-report unsubscribe surfaces never echo the address back.
- **Unsubscribe-token abuse and enumeration**: Mitigated. The unauthenticated `unsubscribeFromWeeklyReportEmail(token: UUID!)` mutation resolves players by an opaque, per-account `EmailUnsubscribeToken` (random GUID, unique index) and always returns a neutral `true` — including for empty/unknown tokens — so callers cannot distinguish valid tokens or infer that an email exists. The token only toggles the weekly-report opt-out flag and grants no other account access. Changing the per-player preference for the logged-in account uses the authenticated `setWeeklyReportEmailSubscription` mutation.
- **Unsolicited email**: Mitigated. Only the weekly report is promotional and players can opt out one-click from the email footer link or from the Account page; `WeeklyEmailReportService.SendDueWeeklyReportsAsync` skips accounts with `WeeklyReportEmailUnsubscribed == true`. Transactional emails (registration, support, account deletion) remain mandatory because they confirm account actions. All email bodies HTML-encode dynamic content via `WebUtility.HtmlEncode` to avoid injection in rendered messages.

## SSL/TLS and Outbound Connection Security

Outbound calls to the master server and external dependencies must not weaken TLS validation outside local development.

- **SSL certificate validation bypass for master-server client**: Mitigated. `MasterServerHttpClientRegistration` now enables `DangerousAcceptAnyServerCertificateValidator` only in Development. Non-Development registrations use the default HTTP client certificate validation path.
- **OIDC metadata HTTPS requirement**: Partially mitigated. OIDC token validation enforces issuer, audience, lifetime, and signature. Residual requirement: keep `RequireHttpsMetadata` enabled outside local development and cover configuration drift in startup tests.
