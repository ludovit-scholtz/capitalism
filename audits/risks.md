# Risk Register

## Identity and Credential Boundaries

The repository relies on JWTs, scoped API keys, and service credentials across multiple applications. The main risk in this category is letting a caller act outside the identity or scope that the server intended.

- API key overreach: Mitigated by `ApiKeyScopes`, deny-by-default `ApiKeyScopeMiddleware`, `BotOwnershipGuard`, and server-side ownership resolution. Residual requirement: every newly added mutation must be registered in the middleware and covered by negative tests.
- Master versus game token confusion: Mitigated by shared token-boundary claims, explicit issuer and token-type checks, and privileged admin resolution from authenticated claims instead of caller-supplied email fields.
- NPC bot credential reuse: Only partially mitigated today. The bot runner supports API key mode and environment overrides, but it still ships with a committed shared password placeholder that should be removed so deployments cannot accidentally start with predictable credentials.

## Authorization and Economy Surfaces

Most player-impacting game mutations already resolve ownership on the server, but the remaining risk is inconsistent behavior in older mutation paths that still expose extra state through their errors.

- Cross-owner mutation abuse: Mitigated by `[Authorize]`, `ObjectAuthorizationService`, `BotOwnershipGuard`, account-context resolution from the authenticated principal, and ownership checks in finance, lending, building, and shareholder mutations.
- Object enumeration and balance disclosure: Still partially open. Several building-market and exchange mutations reveal foreign-object state or precise available balances instead of collapsing to `NOT_FOUND_OR_NOT_OWNED` plus redacted funding errors.
- Stale-quote or replayed FX execution: Mitigated by quote nonces, TTL validation, slippage bounds, and concurrency handling in the forex mutation path.
- Market and collateral race conditions: Mitigated by optimistic offer versions, collateral locks, and commit-time revalidation, but these paths remain sensitive and need continued regression coverage when new secondary-market features are added.

## Rich Content and Browser Surfaces

This codebase intentionally renders HTML in several places, which makes sanitization and dependency hygiene part of the security boundary.

- Player news rendering: Mitigated by `DOMPurify` in the game frontend before `v-html` renders localized news HTML.
- Support markdown preview rendering: Only partially mitigated. Support tickets are converted to HTML server-side, but the current sanitizer is regex-based rather than an allowlist DOM sanitizer, so this remains a stored-XSS risk surface.
- Inline SVG flag rendering: Mitigated because `CountryFlag.vue` renders SVGs from the static `country-flag-icons` library rather than user-supplied SVG input. This should stay isolated from user-controlled content.
- Admin rich-text authoring: The editor intentionally uses `innerHTML` for authoring convenience. This is acceptable only while the loaded content stays trusted and downstream display paths remain sanitized.

## Dependency and Supply-Chain Hygiene

Package-level security posture has improved, but dependency drift remains an operational risk because both frontends and the bot runner rely on external ecosystems.

- Game frontend production dependencies: Currently mitigated. The latest `npm audit --omit=dev` result for `projects/frontend` reports zero production advisories.
- Master frontend production dependencies: Still partially open. `projects/master-frontend` currently reports one moderate `postcss` advisory and should be brought back to a zero known-advisory state.
- .NET package vulnerabilities: Currently mitigated. Local `dotnet list ... package --vulnerable --include-transitive` scans for `Api` and `MasterApi` report no known vulnerable packages.

## Security Assurance and Regression Control

Several major security fixes are now live in code. The remaining risk is silent regression when those boundaries are not locked in by focused tests.

- MasterApi news trust boundary regression: Only partially mitigated. The code now requires trusted server credentials or privileged admin identity, but there is still no dedicated regression suite proving anonymous draft reads and spoofed requester identity stay rejected.
- GraphQL auth and ownership drift: Mitigated in part by the generated `audits/graphql-surface-report.md`, which currently shows no newly added sensitive operations missing required coverage. Residual gap: older sensitive operations still have inconsistent negative-test depth.
- Rich-content sink drift: Mitigated by the generated `audits/frontend-sink-inventory.json`, which keeps `v-html` and `innerHTML` surfaces visible for review before they silently expand.