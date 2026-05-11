# Risk Register

## Identity and Credential Boundaries

The repository relies on JWTs, scoped API keys, password login, and service credentials across multiple applications. The main risk in this category is letting a caller act outside the identity or abuse limits that the server intended.

- Password-auth abuse and account enumeration: Still partially open. `projects/Api/Types/Mutation.Auth.cs` and `projects/MasterApi/Types/Mutation.Auth.cs` perform password `Login` and `Register` without failed-attempt counters, temporary lockouts, CAPTCHA step-up, or ASP.NET Core rate-limiter policies in either backend `Program.cs`. Both registration paths still return explicit duplicate-email errors, which lowers the cost of account discovery and credential stuffing. Mitigation: add account-aware throttling or exponential lockout, endpoint rate limiting, suspicious-auth logging, and non-enumerating duplicate-account responses.
- API key overreach: Mitigated by `ApiKeyScopes`, deny-by-default `ApiKeyScopeMiddleware`, `BotOwnershipGuard`, and server-side ownership resolution. Residual requirement: every newly added sensitive mutation must be registered in the middleware and covered by negative tests.
- Master versus game token confusion: Mitigated by shared token-boundary claims, explicit issuer and token-type checks, and privileged admin resolution from authenticated claims instead of caller-supplied email fields.
- NPC bot credential reuse: Mitigated by the empty committed default password, `BotStartupValidator`, and API-key mode support. Residual requirement: non-Development deployments must provide either a real secret or an API key.

## Authorization and Economy Surfaces

Most player-impacting game mutations already resolve ownership on the server, but the remaining risk is inconsistent behavior in older mutation paths that still expose extra state through their errors.

- Cross-owner mutation abuse: Mitigated by `[Authorize]`, `ObjectAuthorizationService`, `BotOwnershipGuard`, account-context resolution from the authenticated principal, and ownership checks in finance, lending, building, and shareholder mutations.
- Object enumeration and balance disclosure: Still partially open. `MakeOfferOnBuilding`, `BuyFromExchange`, `SellToExchange`, and adjacent banking flows still reveal foreign-object state or precise available balances instead of collapsing to `NOT_FOUND_OR_NOT_OWNED` plus redacted funding errors.
- Stale-quote or replayed FX execution: Mitigated by quote nonces, TTL validation, slippage bounds, and concurrency handling in the forex mutation path.
- Market and collateral race conditions: Mitigated by optimistic offer versions, collateral locks, and commit-time revalidation, but these paths remain sensitive and need continued regression coverage when new secondary-market features are added.

## Rich Content and Browser Surfaces

This codebase intentionally renders HTML in several places, which makes sanitization and dependency hygiene part of the security boundary.

- Player news rendering: Mitigated by `DOMPurify` in the game frontend before `v-html` renders localized news HTML.
- Support markdown preview rendering: Mitigated by the shared `AllowlistHtmlSanitizer` in MasterApi plus integration tests that strip dangerous payloads and preserve safe formatting. Residual requirement: keep expanding the payload corpus when new rich-text features or sinks are introduced.
- Inline SVG flag rendering: Mitigated because `CountryFlag.vue` renders SVGs from the static `country-flag-icons` library rather than user-supplied SVG input. This should stay isolated from user-controlled content.
- Admin rich-text authoring: The editor intentionally uses `innerHTML` for authoring convenience. This is acceptable only while the loaded content stays trusted and downstream display paths remain sanitized.

## Dependency and Supply-Chain Hygiene

Package-level security posture has improved, but dependency drift remains an operational risk because both frontends and the bot runner rely on external ecosystems.

- Game frontend production dependencies: Currently mitigated. `npm audit --omit=dev` and full `npm audit` for `projects/frontend` both report zero advisories as of 2026-W21 (fixed `ajv` ReDoS and `minimatch` ReDoS by running `npm audit fix`; the `@vercel/node` dependency was removed in a prior commit).
- Master frontend dependencies: Currently mitigated. Full `npm audit` and `npm audit --omit=dev` results for `projects/master-frontend` both report zero advisories.
- .NET package vulnerabilities: Currently mitigated. Local `dotnet list ... package --vulnerable --include-transitive` scans for `Api`, `Api.Tests`, `MasterApi`, `MasterApi.Tests`, `Shared`, `NPCBot`, and `NPCBot.Tests` report no known vulnerable packages. Note: CVE-2026-40324 (HotChocolate stack overflow) affects `< 15.1.14`; current project uses 15.1.15 and is patched. NuGet Advisory DB may lag GitHub Advisory DB — monitor for delayed propagation.
- Known CVE — HotChocolate stack overflow (patched): CVE-2026-40324 / GHSA-qr3m-xw4c-jqw3 — Critical (CVSS 9.1). `Utf8GraphQLParser` allows deeply nested GraphQL documents to overflow the call stack (uncatchable `StackOverflowException`, kills process). Patched in HotChocolate 15.1.14. Project uses 15.1.15. No action required; pin build minimum to 15.1.14+ in CI.

## Security Assurance and Regression Control

Several major security fixes are now live in code. The remaining risk is silent regression when those boundaries are not locked in by focused tests.

- Authentication abuse regression: Still partially open. Neither backend has dedicated automated coverage for login throttling, temporary lockout, or non-enumerating registration responses, so future auth changes can preserve scalable password-guessing paths until those controls and tests exist.
- MasterApi news trust boundary regression: Mitigated by trusted server or privileged admin identity resolution together with dedicated integration tests for spoofed requester rejection, draft visibility rules, and service-derived author identity. Residual requirement: keep those tests aligned with future auth changes.
- GraphQL auth and ownership drift: Mitigated in part by the generated `audits/graphql-surface-report.md`, which currently shows no newly added sensitive operations missing required coverage. Residual gap: older economy-sensitive mutations still have inconsistent negative-test depth and response redaction.
- Rich-content sink drift: Mitigated by the generated `audits/frontend-sink-inventory.json`, which keeps `v-html` and `innerHTML` surfaces visible for review before they silently expand.

## GraphQL and API Infrastructure Security

GraphQL's flexible query language introduces DoS and schema-disclosure risks that require explicit server-side configuration.

- Query depth and complexity attacks: Open. Neither `projects/Api` nor `projects/MasterApi` configure `AddMaxAllowedComplexity` or `AddMaxExecutionDepth`. An authenticated user can submit deeply joined or large-field-count queries that exhaust server resources. The parser-level recursion crash (CVE-2026-40324) is patched, but resource-exhaustion via valid queries is not mitigated. Mitigation: add HotChocolate cost-analysis rules to both `Program.cs` files.
- Chat spam and abuse: Open. `Mutation.Chat.cs` `SendChatMessage` has no per-user rate limit and no maximum message length. An authenticated player can flood the shared chat feed at high frequency with arbitrarily long content, degrading experience and growing the database unboundedly. Mitigation: add a `MaxLength` constant (e.g., 500 chars) and per-user in-memory or Redis rate throttle.
- GraphQL schema introspection in production: Open. `app.MapGraphQL()` exposes the full schema and the HotChocolate Nitro IDE browser to all environments including production with no authentication gate. This allows anonymous enumeration of all query/mutation signatures and type metadata. Mitigation: gate Nitro to `IsDevelopment()` only; optionally disable introspection in production via `DisableIntrospection()`.
- CORS open fallback: Open. When `Cors:AllowedOrigins` is missing from configuration, `Program.cs` falls back to `AllowAnyOrigin()`. A misconfigured deployment would silently accept cross-origin authenticated requests from any web origin. Mitigation: restrict the open fallback to `IsDevelopment()` only and log a warning in other environments.

## Infrastructure and Secrets Management

Default configuration values committed to source create a risk path if production deployments accidentally use those defaults.

- Placeholder JWT signing keys in source: Open. Both `Api/appsettings.json` and `MasterApi/appsettings.json` contain `"SigningKey": "ChangeThisSigningKeyBeforeProduction123!"` and the Api has `"AdminPassword": "ChangeMe123!"`. If production configuration injection fails silently, these keys could be used to forge valid JWTs or gain admin access. Mitigation: add a startup guard that throws `InvalidOperationException` in non-Development environments when the key matches the known placeholder.
- Default database password in source: Open. The committed `appsettings.json` connection strings use `Password=password`. Mitigation: document required secret rotation, ensure production deployments use environment variables or a secrets manager.
- Root administrator email committed to source: Open. `MasterApi/appsettings.json` contains the root admin's real email address. This discloses a high-value target for phishing or social engineering. Mitigation: move `RootAdministratorEmails` to environment-variable configuration or a secrets manager.

## HTTP Security Headers

Browser-security HTTP headers are partially configured. Gaps allow downgrade attacks and weaken XSS mitigations.

- Missing HSTS header: Open. `projects/frontend/nginx.conf` sets `X-Frame-Options`, `X-Content-Type-Options`, `X-XSS-Protection`, `Referrer-Policy`, and CSP but omits `Strict-Transport-Security`. Without HSTS, browsers do not enforce HTTPS on future visits and are vulnerable to SSL stripping on first load. Mitigation: add `add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;`.
- CSP `unsafe-inline` for scripts: Open. The CSP header in `nginx.conf` includes `script-src 'self' 'unsafe-inline'`. This significantly weakens XSS protection since any injected inline script can execute. Mitigation: test whether the Vite production build works without `unsafe-inline` (compiled apps typically do not need it) and remove it; consider nonce-based CSP if inline scripts are unavoidable.

## Account Lifecycle and Recovery

Missing account lifecycle flows create user lock-out risks and session management gaps.

- No password reset flow: Open. The game has no "forgot password" or password-reset endpoint in either `Api` or `MasterApi`. A player who loses their password and has no active OIDC link is permanently locked out of their account. This also means compromised accounts cannot easily be recovered by the legitimate owner. Mitigation: implement a time-limited email-based password reset flow or document that OIDC re-linkage is the recovery path.
- No session revocation: Open. JWTs issued by both APIs are stateless with 120-minute TTL. There is no token revocation list or logout mechanism that invalidates existing tokens. A stolen JWT remains valid for up to 2 hours after compromise is detected. Mitigation: consider maintaining a server-side revocation set (Redis or DB) for explicit logout and admin-initiated session termination.
