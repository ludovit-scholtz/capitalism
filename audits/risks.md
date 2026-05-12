# Risk Register

## Identity and Credential Boundaries

The repository relies on JWTs, scoped API keys, password login, and service credentials across multiple applications. The main risk in this category is letting a caller act outside the identity or abuse limits that the server intended.

- **Password-auth abuse and account enumeration**: Mitigated. `LoginThrottleService` enforces 5-failure lockout per account, `AuthRateLimitMiddleware` enforces 10 requests/IP/minute, and registration now returns a neutral non-enumerating response on duplicate email. Controls are disabled in Development/Testing environments. Residual requirement: keep throttle settings in sync as Auth configuration evolves.
- **API key overreach**: Mitigated by `ApiKeyScopes`, deny-by-default `ApiKeyScopeMiddleware`, `BotOwnershipGuard`, and server-side ownership resolution. Residual requirement: every newly added sensitive mutation must be registered in the middleware and covered by negative tests.
- **Master versus game token confusion**: Mitigated by shared token-boundary claims, explicit issuer and token-type checks, and privileged admin resolution from authenticated claims instead of caller-supplied email fields.
- **NPC bot credential reuse**: Mitigated by the empty committed default password, `BotStartupValidator`, and API-key mode support. Residual requirement: non-Development deployments must provide either a real secret or an API key.
- **No JWT session revocation**: Open. JWTs issued by both APIs are stateless with 120-minute TTL. There is no token revocation list or logout mechanism that invalidates existing tokens. A stolen JWT remains valid for up to 2 hours after compromise is detected. Mitigation: implement a server-side revocation set (Redis or DB) for explicit logout and admin-initiated session termination.

## Authorization and Economy Surfaces

Most player-impacting game mutations already resolve ownership on the server, but the remaining risk is inconsistent behavior in older mutation paths that still expose extra state through their errors.

- Cross-owner mutation abuse: Mitigated by `[Authorize]`, `ObjectAuthorizationService`, `BotOwnershipGuard`, account-context resolution from the authenticated principal, and ownership checks in finance, lending, building, and shareholder mutations.
- **Object enumeration and balance disclosure**: Mitigated. `NOT_FOUND_OR_NOT_OWNED` normalization plus balance-redaction has been applied across building-market, exchange, forex, and bank-transfer mutations. Residual risk: older CRUD mutations (`TradeRoutes`, `CompanyMerge`) use `ACCESS_DENIED` codes which still enforce ownership but do not collapse to the normalized pattern.
- **Stale-quote or replayed FX execution**: Mitigated by quote nonces, TTL validation, slippage bounds, and concurrency handling in the forex mutation path.
- **Market and collateral race conditions**: Mitigated by optimistic offer versions, collateral locks, and commit-time revalidation, but these paths remain sensitive and need continued regression coverage when new secondary-market features are added.

## Rich Content and Browser Surfaces

This codebase intentionally renders HTML in several places, which makes sanitization and dependency hygiene part of the security boundary.

- **Player news rendering**: Mitigated by `DOMPurify` in the game frontend before `v-html` renders localized news HTML.
- **Support markdown preview rendering**: Mitigated by the shared `AllowlistHtmlSanitizer` in MasterApi plus integration tests that strip dangerous payloads and preserve safe formatting. Residual requirement: keep expanding the payload corpus when new rich-text features or sinks are introduced.
- **Inline SVG flag rendering**: Mitigated because `CountryFlag.vue` renders SVGs from the static `country-flag-icons` library rather than user-supplied SVG input. This should stay isolated from user-controlled content.
- **Admin rich-text authoring**: The editor intentionally uses `innerHTML` for authoring convenience. This is acceptable only while the loaded content stays trusted and downstream display paths remain sanitized.

## Dependency and Supply-Chain Hygiene

Package-level security posture is clean. Dependency drift remains an operational risk.

- **Game frontend production dependencies**: Mitigated. `npm audit` (both `--omit=dev` and full) reports zero advisories as of 2026-W22.
- **Master frontend dependencies**: Mitigated. `npm audit` (both `--omit=dev` and full) reports zero advisories as of 2026-W22.
- **.NET package vulnerabilities**: Mitigated. `dotnet list package --vulnerable --include-transitive` scans for `Api`, `Api.Tests`, `MasterApi`, `MasterApi.Tests`, `Shared`, `NPCBot` report no known vulnerable packages as of 2026-W22.
- **CVE-2026-40324 — HotChocolate stack overflow (patched)**: Mitigated. CVE-2026-40324 / GHSA-qr3m-xw4c-jqw3 (Critical, CVSS 9.1). Affects `Utf8GraphQLParser` in HotChocolate `< 15.1.14`. Current version is 15.1.15. No action required. Note: NuGet Advisory DB may lag GitHub Advisory DB; monitor for delayed propagation.

## Security Assurance and Regression Control

Several major security fixes are now live in code. The remaining risk is silent regression when those boundaries are not locked in by focused tests.

- **Authentication abuse regression**: Mitigated. `LoginThrottleService`, `AuthRateLimitMiddleware`, and neutral duplicate-email registration responses are implemented. Residual requirement: add dedicated automated backend tests for lockout triggering and non-enumerating response behavior.
- **MasterApi news trust boundary regression**: Mitigated by trusted server or privileged admin identity resolution together with dedicated integration tests for spoofed requester rejection, draft visibility rules, and service-derived author identity. Residual requirement: keep those tests aligned with future auth changes.
- **GraphQL auth and ownership drift**: Mitigated in part by the generated `audits/graphql-surface-report.md`. Residual gap: older CRUD mutations still use `ACCESS_DENIED` rather than `NOT_FOUND_OR_NOT_OWNED` response codes.
- **Rich-content sink drift**: Mitigated by the generated `audits/frontend-sink-inventory.json`, which keeps `v-html` and `innerHTML` surfaces visible for review before they silently expand.

## GraphQL and API Infrastructure Security

GraphQL's flexible query language introduces DoS and schema-disclosure risks that require explicit server-side configuration.

- **Query depth and complexity attacks**: Mitigated. Both `Api` and `MasterApi` configure `AddMaxExecutionDepth` (default 10) and `AddCostAnalyzer` (MaxFieldCost and MaxTypeCost = 1000). The `GraphQlRequestSecurityMiddleware` pre-parses requests and rejects depth and complexity violations with structured error codes (`MAX_DEPTH_EXCEEDED`, `MAX_COMPLEXITY_EXCEEDED`).
- **Chat spam and abuse**: Mitigated. `ChatRateLimitService` enforces 20 messages/60 seconds per user. Maximum message length is 500 characters. `RATE_LIMITED` and `MESSAGE_TOO_LONG` error codes are returned on violation.
- **GraphQL schema introspection in production**: Mitigated. Both APIs gate Nitro IDE (`Tool.Enable = IsDevelopment()`) and schema requests (`EnableSchemaRequests = IsDevelopment()`). The `GraphQlRequestSecurityMiddleware` additionally rejects raw introspection queries in non-Development environments with `FORBIDDEN`.
- **CORS open fallback**: Mitigated. `CorsPolicyHelper.IsDevelopmentOpenPolicy()` checks the environment before applying `AllowAnyOrigin()`. Non-Development deployments with an empty `Cors:AllowedOrigins` list reject all cross-origin requests with a 403 and a startup warning log.

## Infrastructure and Secrets Management

Default configuration values committed to source create a risk path if production deployments accidentally use those defaults.

- **Placeholder JWT signing keys in source**: Partially mitigated. Both APIs have a startup guard (`JwtSigningKeyStartupGuard`) that throws `InvalidOperationException` in non-Development environments when `SigningKey` matches the placeholder. The placeholder keys remain in committed `appsettings.json` as documentation anchors for local development. Production deployments must inject via environment variables.
- **Default database password in source**: Open. The committed `appsettings.json` connection strings use `Password=password`. Mitigation: document required secret rotation; ensure production deployments use environment variables or a secrets manager.
- **Root administrator email committed to source**: Open. `MasterApi/appsettings.json` contains the root admin's real email address. This discloses a high-value target for phishing or social engineering. Mitigation: move `RootAdministratorEmails` to environment-variable configuration or a secrets manager.
- **Admin seed password in source**: Open. `Api/appsettings.json` contains `"AdminPassword": "ChangeMe123!"`. The `PasswordAuthEnabled: false` default reduces immediate risk, but the value should not be committed. Mitigation: remove from source; use environment-variable injection.

## HTTP Security Headers

Browser-security HTTP headers are configured for the game frontend. The master portal is not yet hardened.

- **HSTS header (game frontend)**: Mitigated. `projects/frontend/nginx.conf` includes `add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;`.
- **CSP `unsafe-inline` for scripts (game frontend)**: Mitigated. The CSP in `nginx.conf` uses `sha256-...` hash-based allowlisting for the inline theme-bootstrap script instead of `unsafe-inline`.
- **Missing security headers for master-frontend**: Open. `projects/master-frontend` has no nginx.conf or deployment-level security header configuration. HSTS, CSP, X-Frame-Options, X-Content-Type-Options, and Referrer-Policy are not set. Mitigation: add an nginx.conf (or equivalent deployment config) mirroring the game frontend header set.

## Account Lifecycle and Recovery

- **Password reset flow**: Mitigated. MasterApi implements a time-limited email-based password reset flow (`/auth/forgot-password`, `/auth/reset-password`) with `PasswordResetThrottleService` rate limiting and token expiry. Game API has no independent password reset (relies on MasterApi or OIDC re-linkage).
- **No JWT session revocation**: Open. JWTs issued by both APIs are stateless with 120-minute TTL. There is no token revocation list or logout mechanism. A stolen JWT remains valid for up to 2 hours after compromise is detected. Mitigation: implement a server-side revocation set (Redis or DB) for explicit logout and admin-initiated session termination.

## SSL/TLS and Outbound Connection Security

- **SSL certificate validation bypass for master-server client**: Open. In `Api/Program.cs`, the HTTP client for `master-server` disables SSL validation when `MasterServer:ApiUrl` contains the string `"masterapi"` (the Docker Compose container hostname). This condition is URL-string-based rather than environment-based, meaning Docker Compose production deployments also bypass certificate validation, making MasterApi registration traffic susceptible to MITM attacks. Mitigation: replace the URL-string check with an explicit `IsDevelopment()` or a dedicated `MasterServer:DisableSslValidation` opt-in flag.
