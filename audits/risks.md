# Risk Register

## Identity and Credential Boundaries

The repository relies on JWTs, scoped API keys, password login, OIDC, bot automation credentials, and service-to-service secrets. The main risk in this category is allowing a caller to act outside the identity boundary or to abuse authentication flows until an account is compromised.

- **Password-auth abuse and rate-limit bypass**: Partially mitigated. `LoginThrottleService` still enforces account-aware lockout and `AuthRateLimitMiddleware` applies per-IP throttling outside Development/Testing. Residual risk: the rate limiter trusts client-supplied `X-Forwarded-For`, only deserializes single JSON-object GraphQL bodies, and skips auth operations when `operationName` is not literally `login` or `register` even if the selected root field is a login/register mutation. Mitigation: resolve client IP only through trusted proxy configuration, parse selected GraphQL root fields, and inspect or reject batched request bodies.
- **Account enumeration through duplicate registration codes**: Partially mitigated. Duplicate registration uses a neutral human-readable message, but both APIs still return the machine-readable extension code `DUPLICATE_EMAIL`. A scripted caller can use that code as an account-existence oracle when password auth is enabled. Mitigation: normalize the public error code and timing while keeping internal telemetry for abuse response.
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

The bank-account migration introduced strong local-currency rules in some paths, but this audit confirmed that a few high-value business flows still mix “nominal amount” handling with multi-currency company balances. That creates a distinct risk category: a player does not need auth bypass to cheat if the economic settlement rules themselves can be routed through the wrong asset, account, or currency.

- **Off-floor building transfers through accepted offers**: Open. `setBuildingForSale` enforces the valuation floor on the public asking price, but `makeOfferOnBuilding` and `acceptBuildingOffer` still allow any positive accepted offer. A colluding seller and buyer can therefore transfer a building below the intended minimum floor after listing it compliantly.
- **Defaulted-collateral lender strip via cheap friendly repurchase**: Open. Defaulted loans are intentionally allowed to list the collateral building so the borrower can resolve the debt. Because accepted offers are not floor-protected, a borrower can default, sell the collateral to a friendly company at a token price, and leave the lender with residual unsecured principal after the lien is cleared.
- **Loan origination funded from the wrong lender currency**: Open. `acceptLoan` still checks and debits aggregate lender-company balances with no currency filter. A bank in one city can therefore disburse a loan denominated in its own city currency while actually consuming nominal balances held in other city currencies.
- **Loan repayment credited into the wrong lender currency**: Open. Scheduled loan repayment and manual debt repayment still have lender-credit paths that use `TryCredit(..., null)` rather than the loan currency, allowing repayment to land in whichever lender company account is currently preferred.
- **Defaulted-loan repayment after closing the scheduled account**: Open. `closeCompanyBankAccount` blocks repayment-account closure only for `Active` and `Overdue` loans, not `Defaulted` ones. Once the designated account is closed, `repayLoanDebt` falls back to aggregate company balances with no FX normalization.
- **Editable pledged buildings**: Open. Collateral locks currently protect sale, transfer, and demolition, but not building-configuration changes. A borrower can still modify a pledged building after origination unless another domain-specific check happens to block the exact action.
- **Pending building offers are non-reserved**: Open. Buyers can post multiple pending offers across different buildings using the same money because the market does not reserve funds at offer time.
- **Defaulted principal omitted from fresh lending-capacity checks**: Partially mitigated. Actual lender cash still constrains some abuse, but `acceptLoan` excludes defaulted unpaid principal from the deposit-capacity calculation, so regulatory-style capacity can reopen too early.
- **Local-currency lot purchase enforcement**: Mitigated. `PrepareLotPurchaseAsync` debits a company account already in the lot city currency and returns `INSUFFICIENT_LOCAL_CURRENCY_FUNDS` when that balance is missing.
- **Same-currency transfer and account-context enforcement**: Mitigated. `transferFunds` allows only same-currency moves and confines transfers to the active personal or company context.
- **Company-context forex routing**: Mitigated. `executeForexSwap` requires explicit matching-currency source and destination accounts and rejects mismatches with `CURRENCY_MISMATCH`.
- **Forced-sale building currency and FX debt settlement**: Mitigated. defaulted collateral is listed in the building city currency, while debt settlement converts internally into the lending bank currency.

## GraphQL and API Infrastructure Security

GraphQL's flexible query language introduces DoS, schema-disclosure, batching, and pre-execution middleware risks that need explicit server-side controls.

- **Query depth and complexity attacks**: Mitigated. Both `Api` and `MasterApi` configure HotChocolate max execution depth, cost analysis, max page size, and `GraphQlRequestSecurityMiddleware` rejection responses for excessive depth or complexity.
- **GraphQL schema introspection in production**: Mitigated for single-request bodies. Both APIs gate Nitro IDE and schema requests to Development and reject raw introspection fields in non-Development environments. Residual risk: the custom middleware currently extracts only the `query` property from JSON-object request bodies, so JSON-array batched bodies may not receive the same pre-execution inspection if HotChocolate accepts batching.
- **GraphQL auth operation detection**: Open. `AuthRateLimitMiddleware` trusts the request `operationName` before inspecting the selected field. A named operation can execute `login` or `register` with a non-auth operation name and avoid the per-IP limiter. Mitigation: parse the GraphQL document and selected operation root fields before deciding whether the request is auth-sensitive.
- **Chat spam and abuse**: Mitigated. `ChatRateLimitService` enforces 20 messages per 60 seconds per user, maximum message length is 500 characters, and structured rate-limit errors are returned.
- **CORS open fallback**: Mitigated. `CorsPolicyHelper.IsDevelopmentOpenPolicy()` checks the environment before applying `AllowAnyOrigin()`. Non-Development deployments with an empty `Cors:AllowedOrigins` list reject cross-origin requests with a 403 and startup warning log.

## Rich Content and Browser Surfaces

The frontends intentionally render rich HTML in news and support flows. Browser-side token storage also makes XSS prevention part of the authentication boundary.

- **Player news rendering**: Mitigated by `DOMPurify` in the game frontend before `v-html` renders localized news HTML.
- **Support markdown preview rendering**: Mitigated by the shared MasterApi allowlist sanitizer plus frontend DOMPurify before previews are rendered. Residual requirement: keep expanding the payload corpus when new rich-text features or sinks are introduced.
- **Inline SVG flag rendering**: Mitigated because `CountryFlag.vue` renders SVGs from the static `country-flag-icons` library rather than user-supplied SVG input. This must stay isolated from user-controlled content.
- **Browser JWT storage in `localStorage`**: Open. Both frontends persist bearer tokens in `localStorage`, so a successful XSS could read tokens directly. CSP, DOMPurify, revocation, and short token lifetimes reduce likelihood and blast radius, but the safer target is HttpOnly SameSite cookies or a backend-for-frontend session pattern.
- **Admin rich-text authoring**: Acceptable with controls. The editor is allowed for authoring convenience only while loaded content remains trusted and downstream display paths remain sanitized.

## Dependency and Supply-Chain Hygiene

Package-level security posture is currently clean. Dependency drift remains an operational risk because both frontend and backend ecosystems move quickly.

- **Game frontend dependencies**: Mitigated. `npm audit --omit=dev` and full `npm audit` both report zero vulnerabilities as of 2026-05-13.
- **Master frontend dependencies**: Mitigated. `npm audit --omit=dev` and full `npm audit` both report zero vulnerabilities as of 2026-05-13.
- **.NET package vulnerabilities**: Mitigated. Individual `dotnet list package --vulnerable --include-transitive` scans for `Api`, `Api.Tests`, `MasterApi`, `MasterApi.Tests`, `Shared`, `NPCBot`, and `NPCBot.Tests` report no known vulnerable packages as of 2026-05-13. Solution-level scanning still has tooling friction because the compose project uses legacy package configuration.
- **CVE-2026-40324 / GHSA-qr3m-xw4c-jqw3 HotChocolate stack overflow**: Mitigated. The advisory affects HotChocolate versions before 15.1.14; current projects use 15.1.15.

## Security Assurance and Regression Control

Several major security fixes are now live in code. The remaining risk is silent regression when these boundaries are not locked in by focused tests and generated inventories.

- **Authentication abuse regression**: Partially mitigated. Login lockout and auth rate limiting are implemented, but tests should cover named-operation, batched-body, and spoofed-forwarded-header bypass attempts.
- **MasterApi news trust boundary regression**: Mitigated by trusted server or privileged admin identity resolution together with integration tests for spoofed requester rejection, draft visibility rules, and service-derived author identity.
- **GraphQL auth and ownership drift**: Mitigated in part by generated `audits/graphql-surface-inventory.json`. Residual gap: keep adding boundary tests whenever a sensitive GraphQL operation is added or an older operation's response contract is normalized.
- **Rich-content sink drift**: Mitigated by generated `audits/frontend-sink-inventory.json`, which keeps `v-html` and `innerHTML` surfaces visible for review before they silently expand.

## Infrastructure and Secrets Management

Default configuration values committed to source create a risk path if production deployments accidentally use local defaults.

- **Placeholder JWT signing keys in source**: Mitigated for non-Development. Both APIs have a startup guard that throws `InvalidOperationException` outside Development/Testing when `Jwt:SigningKey` is missing, short, or matches a placeholder.
- **Database connection strings and root administrator emails in source**: Mitigated for non-Development. `appsettings.json` now uses `__SET_IN_ENV__` connection-string placeholders and `MasterApi` blocks non-Development startup when root administrator emails are missing or placeholder values.
- **Admin seed password in source**: Open. `projects/Api/appsettings.json` and `SeedDataOptions` still default the seed admin password to `ChangeMe123!`. `PasswordAuthEnabled=false` reduces immediate exposure, but a production deployment that enables password auth without overriding `SeedData__AdminPassword` would seed a known administrator credential.
- **Local Docker Compose credentials**: Accepted local-development risk. Compose defaults are still human-readable and must not be reused for production deployments. The production startup guards cover the application-level connection-string path.

## HTTP Security Headers

Browser-security HTTP headers are now configured for both shipped frontend deployments.

- **Game frontend security headers**: Mitigated. `projects/frontend/nginx.conf` sets HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, and Permissions-Policy.
- **Master frontend security headers**: Mitigated. `projects/master-frontend/nginx.conf` now sets HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, and Permissions-Policy.

## Account Lifecycle and Recovery

Account lifecycle controls reduce the impact of credential loss and token compromise.

- **Password reset flow**: Mitigated. MasterApi implements time-limited email reset endpoints with `PasswordResetThrottleService` rate limiting and token expiry. Game API has no independent password reset and relies on MasterApi or OIDC re-linkage.
- **Explicit logout and admin session termination**: Mitigated. Both APIs expose current-session logout, logout-all, active session listing, and admin revoke-all endpoints backed by persistent session/revocation data.

## SSL/TLS and Outbound Connection Security

Outbound calls to the master server and external dependencies must not weaken TLS validation outside local development.

- **SSL certificate validation bypass for master-server client**: Mitigated. `MasterServerHttpClientRegistration` now enables `DangerousAcceptAnyServerCertificateValidator` only in Development. Non-Development registrations use the default HTTP client certificate validation path.
- **OIDC metadata HTTPS requirement**: Partially mitigated. OIDC token validation enforces issuer, audience, lifetime, and signature. Residual requirement: keep `RequireHttpsMetadata` enabled outside local development and cover configuration drift in startup tests.
