# Security Audit Report - 2026-05-11

## Audited Projects
- `projects/Api` at repository revision `a445cc0e28ee4d6c982cc3eb61028f3bbe71efa7`
- `projects/MasterApi` at repository revision `a445cc0e28ee4d6c982cc3eb61028f3bbe71efa7`
- `projects/frontend` at repository revision `a445cc0e28ee4d6c982cc3eb61028f3bbe71efa7`
- `projects/master-frontend` at repository revision `a445cc0e28ee4d6c982cc3eb61028f3bbe71efa7`
- Dependency baselines checked during this audit: HotChocolate `15.1.15`, `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.7`, Vue `3.5.29`, Vite `7.3.1`, DOMPurify `3.3.3`, `@vercel/node` `5.6.18`

## Summary
- Reviewed GraphQL authorization boundaries, service-input trust boundaries, object-level error disclosures, HTML-rendering sinks, and dependency advisories across both APIs and both frontends.
- Verified strong existing controls in the game API: token issuer/audience separation, API-key scope issuance plus deny-by-default enforcement, impersonation detection based on actor vs effective player IDs, and server-side ownership checks on most economy-sensitive mutations.
- Verified package-vulnerability baseline with local dependency scans. `dotnet list CapitalismBackend.slnx package --vulnerable --include-transitive` reported no known vulnerable NuGet packages in `Api` or `Api.Tests`. `npm audit --audit-level=high --omit=dev` reported `14` vulnerabilities (`7` high, `7` moderate) in `projects/frontend` and `1` moderate vulnerability in `projects/master-frontend`.
- The highest-severity gap found in the current code is the MasterApi news-service trust boundary: draft reads and news-entry writes are controlled by caller-supplied email fields instead of trusted service credentials or authenticated administrator identity.

## Findings

### 1. Critical - MasterApi draft news feed can be read without trusted service credentials or authenticated admin identity
- Evidence: `projects/MasterApi/Types/Query.cs` calls `EnsureServiceAccess(input, masterServerOptions, false, false)` for `GetGameNewsFeed`, which disables both registration-key and server-key enforcement.
- Evidence: when `includeDrafts` is `true`, the code only requires a non-empty `RequesterEmail` string and does not verify that the requester is a root or global game administrator before returning draft entries.
- Impact: an unauthenticated caller who can reach MasterApi can enumerate unpublished drafts, live-ops announcements, and future changelog content by sending `includeDrafts: true` with any non-empty `requesterEmail` value.
- OWASP mapping: API5:2023 Broken Function Level Authorization and API1:2023 Broken Object Level Authorization.

### 2. Critical - MasterApi news upsert mutation trusts spoofable `RequesterEmail` and a caller-controlled `ServerKey`
- Evidence: `projects/MasterApi/Types/Mutation.News.cs` also calls `EnsureServiceAccess(input, masterServerOptions, false, false)` for `UpsertGameNewsEntry`.
- Evidence: authorization is derived from `BuildGameAdministrationAccessAsync(..., requesterEmail)` where `requesterEmail` comes directly from the mutation input, not from an authenticated JWT claim or a validated game-server credential.
- Impact: any caller can create or edit server-scoped news entries for an arbitrary `serverKey` by supplying their own email, and a caller who guesses a privileged administrator email can potentially publish global or cross-server news content.
- Integrity risk: this is not only an information-disclosure issue; it is a direct content-integrity issue against the player-facing news and changelog system.

### 3. Medium - Object-level authorization responses still leak foreign-object existence and balance detail
- Evidence: `projects/Api/Types/Mutation.BuildingMarket.cs` distinguishes `COMPANY_NOT_FOUND`, `OFFER_NOT_FOUND`, `BUILDING_NOT_FOR_SALE`, and `BUILDING_NOT_FOUND`, and returns exact available-balance text such as `Available: ...` in `INSUFFICIENT_FUNDS` errors.
- Evidence: similar exact-balance disclosures exist in `projects/Api/Types/Mutation.BankAccountTransfer.cs`, `projects/Api/Types/Mutation.Exchange.cs`, and other economy-sensitive mutations.
- Impact: these responses reduce the cost of object enumeration and competitive-intelligence gathering, especially when a caller can probe foreign companies, offers, or accounts and learn whether an object exists, is listed, or has sufficient funds.
- Status relative to roadmap: this aligns with the still-open roadmap item to standardize responses toward `not found or not owned` semantics.

### 4. Medium - Game frontend production dependency graph contains unresolved advisories on a reachable HTML-rendering path
- Evidence: `projects/frontend/package.json` ships `dompurify` `^3.3.3` and `@vercel/node` `^5.6.18` in production dependencies.
- Evidence: `npm audit --audit-level=high --omit=dev` for `projects/frontend` reported high-severity advisories through `undici`, `path-to-regexp`, `minimatch`, and moderate advisories including `dompurify` and `postcss`.
- Reachability: `projects/frontend/src/views/NewsView.vue` imports `DOMPurify`, sanitizes server-supplied HTML, and renders the result with `v-html`, so the sanitization dependency is on an active player-facing surface.
- Impact: while this audit did not confirm exploitability in the app, the shipped production dependency tree now contains known advisories on code that participates in rendered rich-content flows.

## Recommendations
1. Close the MasterApi news-service trust-boundary gap first. Require either validated game-server credentials or authenticated root/global admin claims for `GetGameNewsFeed(includeDrafts: true)` and `UpsertGameNewsEntry`, and stop authorizing these flows from caller-supplied email fields.
2. Add focused regression tests in `projects/MasterApi.Tests` proving that anonymous callers cannot read draft news, cannot spoof `requesterEmail`, and cannot publish or edit server/global news without real admin or trusted-service credentials.
3. Finish the object-authorization response hardening pass in the game API. Use generic `NOT_FOUND_OR_NOT_OWNED`-style outcomes for unauthorized foreign objects and remove precise available-balance disclosures from paths that should not leak competitor state.
4. Remediate frontend dependency advisories. Upgrade `dompurify`, `postcss`, and the `@vercel/node` transitive chain, and reevaluate whether `@vercel/node` belongs in shipped frontend production dependencies at all.
5. Add a recurring dependency-audit gate for both frontends so new production advisories are caught before release rather than during manual audit work.
6. Keep the already-strong controls in place: token-boundary validation, API-key scope enforcement, server-side ownership resolution, and replay/idempotency protections should remain part of every future weekly audit baseline.

## Conclusion
The repository's core server-side authorization posture is materially better than it was in earlier audits: the game API shows solid ownership checks, strict token-boundary handling, and no currently known vulnerable NuGet packages from the local dependency scan. The main remaining risk is concentrated in the MasterApi news-service surface, where a service-style API drifted into trusting caller-supplied identity fields without trusted credentials. That issue should be treated as the next security hardening priority, followed by dependency remediation in the game frontend and completion of the object-level error-response normalization already tracked on the roadmap.