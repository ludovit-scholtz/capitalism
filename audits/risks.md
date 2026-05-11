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

- Game frontend production dependencies: Currently mitigated. The latest `npm audit --omit=dev` result for `projects/frontend` reports zero production advisories.
- Game frontend build and deployment tooling: Still partially open. The latest full `npm audit` result for `projects/frontend` reports 9 advisories (6 high, 3 moderate) rooted in direct devDependency `@vercel/node@5.7.17` and transitive `undici`, `path-to-regexp`, `minimatch`, `ajv`, and `smol-toml`. Residual requirement: upgrade or remove the Vercel builder toolchain if it is not required by the current deployment path.
- Master frontend dependencies: Currently mitigated. The latest full `npm audit` and `npm audit --omit=dev` results for `projects/master-frontend` both report zero advisories.
- .NET package vulnerabilities: Currently mitigated. Local `dotnet list ... package --vulnerable --include-transitive` scans for `Api`, `Api.Tests`, `MasterApi`, `MasterApi.Tests`, `Shared`, `NPCBot`, and `NPCBot.Tests` report no known vulnerable packages.

## Security Assurance and Regression Control

Several major security fixes are now live in code. The remaining risk is silent regression when those boundaries are not locked in by focused tests.

- Authentication abuse regression: Still partially open. Neither backend has dedicated automated coverage for login throttling, temporary lockout, or non-enumerating registration responses, so future auth changes can preserve scalable password-guessing paths until those controls and tests exist.
- MasterApi news trust boundary regression: Mitigated by trusted server or privileged admin identity resolution together with dedicated integration tests for spoofed requester rejection, draft visibility rules, and service-derived author identity. Residual requirement: keep those tests aligned with future auth changes.
- GraphQL auth and ownership drift: Mitigated in part by the generated `audits/graphql-surface-report.md`, which currently shows no newly added sensitive operations missing required coverage. Residual gap: older economy-sensitive mutations still have inconsistent negative-test depth and response redaction.
- Rich-content sink drift: Mitigated by the generated `audits/frontend-sink-inventory.json`, which keeps `v-html` and `innerHTML` surfaces visible for review before they silently expand.
