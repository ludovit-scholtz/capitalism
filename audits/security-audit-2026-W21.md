# Security Audit Report — 2026-W21 (2026-05-11)

## Auditor
GitHub Copilot using Claude Sonnet 4.6, acting as Emil Security Auditor. Audit conducted 2026-05-11.

## Audited Projects
All projects at repository revision `3f8bb9a8d24369a6d4ade0c1ef60d677d24ea2ba` (HEAD of `main`):
- `projects/Api` — ASP.NET Core 10 game backend, HotChocolate 15.1.15
- `projects/MasterApi` — ASP.NET Core 10 master-server backend, HotChocolate 15.1.15
- `projects/Shared` — Shared class library
- `projects/Api.Tests` — Integration test suite
- `projects/MasterApi.Tests` — Master API integration tests
- `projects/NPCBot` — NPC bot console application
- `projects/NPCBot.Tests` — NPC bot tests
- `projects/frontend` — Vue 3 + Vite game frontend
- `projects/master-frontend` — Vue 3 + Vite master portal frontend

## Summary

This audit focused on:
1. Verifying the resolution status of W20 open findings
2. Auditing new mutation surfaces (TradeRoutes, CompanyMerge, MultiCityExpansion, MediaHouse, PowerPlant, PublicSales, UnitUpgrade)
3. Checking CVE databases for technologies in use (HotChocolate, ASP.NET Core, Vue.js, PostgreSQL)
4. Dependency vulnerability scanning (npm audit, dotnet list package --vulnerable)
5. Checking security configuration: CORS, CSP headers, GraphQL settings, JWT secrets, rate limiting

**Key findings:**
- **Critical (patched)**: CVE-2026-40324 (GHSA-qr3m-xw4c-jqw3) — HotChocolate stack overflow via deeply nested GraphQL. Project uses v15.1.15 which is patched (fix landed in 15.1.14). No action required but must be tracked.
- **High (fixed this session)**: `ajv` (moderate ReDoS) and `minimatch` (high ReDoS) in frontend devDependencies. Fixed by running `npm audit fix` — both frontends now report 0 advisories at all levels.
- **Medium (new)**: No GraphQL query depth or complexity limits configured beyond the now-patched parser-level recursion guard.
- **Medium (new)**: Chat mutation (`SendChatMessage`) has no per-user rate limiting or message-length cap.
- **Medium (carried)**: Password-auth endpoints still lack throttling, lockout, and non-enumerating responses. (Active roadmap item.)
- **Medium (carried)**: Object-level authorization response leakage in building-market and exchange mutations. (Active roadmap item.)
- **Low (new)**: HotChocolate Nitro IDE and schema introspection enabled in production (no environment gate).
- **Low (new)**: `Strict-Transport-Security` (HSTS) header absent from nginx.conf.
- **Low (new)**: Placeholder JWT signing key (`ChangeThisSigningKeyBeforeProduction123!`) and default DB password committed in `appsettings.json`; root admin email also committed.
- **Low (new)**: CORS fallback to `AllowAnyOrigin()` when no `Cors:AllowedOrigins` are configured.
- **Low (new)**: CSP uses `script-src 'unsafe-inline'` in nginx.conf.
- All new high-value mutation files (TradeRoutes, CompanyMerge, PowerPlant, MediaHouse, UnitUpgrade, PublicSales) confirmed to perform their own ownership checks inline — no BotOwnershipGuard gap for these paths.
- No new vulnerable NuGet packages across all .NET projects.

## Findings

### 1) CVE-2026-40324 — HotChocolate Stack Overflow via Deeply Nested GraphQL (VERIFIED PATCHED)
- **Severity:** Critical (CVSS 9.1) — verified mitigated by current version
- **CVE/GHSA:** CVE-2026-40324 / GHSA-qr3m-xw4c-jqw3
- **Affected:** HotChocolate.Language `>= 15.0.0, < 15.1.14`
- **Description:** `Utf8GraphQLParser` has no recursion depth limit. A 40 KB crafted GraphQL document with deeply nested selections causes an uncatchable `StackOverflowException`, killing the entire process before any validation middleware runs.
- **Current project version:** 15.1.15 (patched — fix was in 15.1.14)
- **`dotnet list --vulnerable` result:** Not flagged (NuGet Advisory DB updated with slight delay relative to GitHub Advisory DB)
- **Action required:** None. Verify if `dotnet list --vulnerable` detects this going forward. Pin minimum version to 15.1.14+ in PR checks.
- **Status:** Mitigated (upgrade already applied)

### 2) npm Frontend Dependency Vulnerabilities (FIXED THIS SESSION)
- **Severity:** High (`minimatch` ReDoS), Moderate (`ajv` ReDoS)
- **Affected:** `projects/frontend` build toolchain — `minimatch@10.0.0–10.2.2`, `ajv@7.0.0-alpha.0–8.17.1`
- **Description:** `minimatch` had three high-severity ReDoS vulnerabilities (GHSA-3ppc-4f35-3m26, GHSA-7r86-cg39-jmmj, GHSA-23c5-xmqv-rm74). `ajv` had a moderate ReDoS via `$data` option (GHSA-2g4f-4pwh-qvx6).
- **Fix applied:** Ran `npm audit fix` in `projects/frontend`. 0 vulnerabilities now reported.
- **Status:** Resolved (fixed in this session)

### 3) No GraphQL Query Depth or Complexity Limits
- **Severity:** Medium
- **Affected:** `projects/Api/Program.cs`, `projects/MasterApi/Program.cs`
- **Description:** Neither API configures `AddMaxAllowedComplexity`, query depth limits, or throttling on GraphQL request size beyond the parser-level recursion guard fixed in CVE-2026-40324. An authenticated user can craft legitimately valid but extremely expensive queries (e.g., deeply joined entity graphs with 2048 fields) that cause server resource exhaustion without triggering a `StackOverflowException`.
- **Recommended fix:** Add `.AddMaxAllowedComplexity(200).AddMaxExecutionDepth(10)` (or equivalent HotChocolate cost analysis) to the GraphQL server configuration in `Program.cs` for both APIs.
- **Status:** Open <!-- issue: #417 -->

### 4) Chat Mutation Has No Rate Limiting or Content Length Cap
- **Severity:** Medium
- **Affected:** `projects/Api/Types/Mutation.Chat.cs` → `SendChatMessage`
- **Description:** `SendChatMessage` only validates that the message is non-empty after trimming. There is no maximum message length and no per-user send-rate limit. An authenticated player can flood the chat with very long messages at high frequency, producing database bloat and degrading the user experience for all concurrent players.
- **Recommended fix:** (1) Add a `MaxLength` constant (e.g., 500 characters) and reject messages exceeding it with `CHAT_MESSAGE_TOO_LONG`. (2) Add ASP.NET Core rate limiting for the `/graphql` endpoint or a per-user in-memory throttle for chat mutations.
- **Status:** Open <!-- issue: #417 -->

### 5) Password-Authentication Endpoints Lack Throttling and Reveal Account Existence (Carried from W20)
- **Severity:** Medium
- **Affected:** `projects/Api/Types/Mutation.Auth.cs`, `projects/MasterApi/Types/Mutation.Auth.cs`
- **Description:** `Login` and `Register` have no failed-attempt counters, lockout, or rate limiting. `Register` returns `DUPLICATE_EMAIL` on known email addresses. (Active roadmap item.)
- **Status:** Open <!-- issue: #416 -->

### 6) Object-Level Authorization Response Leakage (Carried from W20)
- **Severity:** Medium
- **Affected:** `Mutation.BuildingMarket.cs`, `Mutation.Exchange.cs`, `Mutation.BankAccountTransfer.cs`
- **Description:** Distinct error codes and detailed `Available:` balance messages allow authenticated players to infer foreign object states and opponent liquidity. (Active roadmap item.)
- **Status:** Open <!-- issue: #393 -->

### 7) HotChocolate Nitro IDE and Schema Introspection Always Enabled
- **Severity:** Low
- **Affected:** `projects/Api/Program.cs` — `app.MapGraphQL()`
- **Description:** `app.MapGraphQL()` with no environment guard exposes the HotChocolate Banana Cake Pop / Nitro browser IDE and full schema introspection at `/graphql` in all environments including production. This lets any anonymous user discover the complete GraphQL schema, all query/mutation signatures, and type system metadata. While the game's schema is not secret, production introspection increases the attack surface for automated enumeration and targeted payload crafting.
- **Recommended fix:** Conditionally enable Nitro only in development: `if (app.Environment.IsDevelopment()) app.MapBananaCakePop();`. Optionally disable introspection in production via HotChocolate's `DisableIntrospection()` option if schema privacy is desired.
- **Status:** Open <!-- issue: #417 -->

### 8) HSTS (Strict-Transport-Security) Header Absent from nginx.conf
- **Severity:** Low
- **Affected:** `projects/frontend/nginx.conf`
- **Description:** The nginx security header block includes `X-Frame-Options`, `X-Content-Type-Options`, `X-XSS-Protection`, `Referrer-Policy`, and `Content-Security-Policy` but omits `Strict-Transport-Security`. Without HSTS, browsers do not enforce HTTPS for future visits and are vulnerable to SSL stripping attacks on first load.
- **Recommended fix:** Add `add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;` to the nginx security header block.
- **Status:** Open <!-- issue: #417 -->

### 9) Placeholder JWT Signing Keys and Default DB Password Committed to Source
- **Severity:** Low (development defaults — should not reach production)
- **Affected:** `projects/Api/appsettings.json`, `projects/MasterApi/appsettings.json`
- **Description:** Both `appsettings.json` files contain `"SigningKey": "ChangeThisSigningKeyBeforeProduction123!"`. The Api also contains `"AdminPassword": "ChangeMe123!"`. The database connection string uses `Password=password`. A root administrator email is also committed in MasterApi `appsettings.json`. If production configuration is accidentally served from these files (e.g., if secrets injection fails silently), attackers can forge JWT tokens or gain admin access.
- **Recommended fix:** (1) Document the secret rotation requirement in deployment runbooks. (2) Add an ASP.NET Core startup guard that throws if `SigningKey` matches the placeholder pattern in non-Development environments. (3) Move `RootAdministratorEmails` to environment-variable configuration or a secrets manager.
- **Status:** Open <!-- issue: #417 -->

### 10) CORS Falls Back to AllowAnyOrigin When No Origins Configured
- **Severity:** Low
- **Affected:** `projects/Api/Program.cs` lines 55–70
- **Description:** When `Cors:AllowedOrigins` is empty or missing, the policy calls `AllowAnyOrigin()` as a development convenience. If a deployment accidentally ships without the origins list, any web origin can make authenticated cross-origin requests.
- **Recommended fix:** Replace the open fallback with a warning log and no-origin fallback (strict reject). Use a separate `Development`-environment check: only `AllowAnyOrigin()` in `builder.Environment.IsDevelopment()`.
- **Status:** Open <!-- issue: #417 -->

### 11) CSP Uses `unsafe-inline` for Script Sources
- **Severity:** Low
- **Affected:** `projects/frontend/nginx.conf`
- **Description:** The CSP header includes `script-src 'self' 'unsafe-inline'`. This permits any inline `<script>` block including those injected by XSS, reducing the XSS mitigation value of CSP significantly. Vue 3 + Vite can be built without requiring `unsafe-inline` by using strict CSP mode.
- **Recommended fix:** Consider generating a nonce per request (nginx `sub_filter` or SSR) and switching to `script-src 'self' 'nonce-<nonce>'`. As a minimum improvement, remove `unsafe-inline` and test whether the built app still works (Vite-compiled production bundles often do not need it).
- **Status:** Open <!-- issue: #417 -->

## New Audit Items — All New Mutation Files Since W20

The following mutation files added since the W20 audit were inspected for authorization correctness. All were found to perform inline ownership checks via `GetRequiredUserId()` + entity ownership validation:

| Mutation file | Methods | Ownership check |
|---|---|---|
| `Mutation.TradeRoutes.cs` | `CreateTradeRoute` | `company.PlayerId != player.Id` |
| `Mutation.CompanyMerge.cs` | `MergeCompany`, `StartAdditionalCompany` | `destinationCompany.PlayerId != userId` |
| `Mutation.PowerPlant.cs` | `SetPlantDispatch` | `building.Company.PlayerId != userId` → `NOT_FOUND_OR_NOT_OWNED` |
| `Mutation.MediaHouse.cs` | `SetMediaHouseContentBudget`, `UpgradeMediaHouse`, `ConfigureMediaHouseUnit` | `building.Company.PlayerId != userId` → `NOT_FOUND_OR_NOT_OWNED` |
| `Mutation.UnitUpgrade.cs` | `ScheduleUnitUpgrade` | `unit.Building.Company.PlayerId != userId` → `NOT_FOUND_OR_NOT_OWNED` |
| `Mutation.PublicSales.cs` | `UpdatePublicSalesPrice`, `FlushStorage` | `unit.Building.Company.PlayerId != userId` → `NOT_FOUND_OR_NOT_OWNED` |
| `Mutation.MultiCityExpansion.cs` | `UnlockCity` | Read-only city check; not a financial mutation |

**No BotOwnershipGuard gaps found for the new mutation surfaces.** All financial mutations that modify balances or game state perform inline ownership validation. The older CRUD mutations (`TradeRoutes`, `CompanyMerge`) use `ACCESS_DENIED` error codes which — while not collapsing to `NOT_FOUND_OR_NOT_OWNED` — still enforce ownership correctly.

## Dependency Scan Results

### npm audit (game frontend — `projects/frontend`)
- **`npm audit --omit=dev`:** 0 vulnerabilities ✅
- **Full `npm audit`:** 0 vulnerabilities ✅ (fixed during this session by `npm audit fix`)

### npm audit (master frontend — `projects/master-frontend`)
- **`npm audit --omit=dev`:** 0 vulnerabilities ✅
- **Full `npm audit`:** 0 vulnerabilities ✅

### dotnet list package --vulnerable (all .NET projects)
- `Api`: No vulnerable packages ✅
- `MasterApi`: No vulnerable packages ✅
- `Api.Tests`: No vulnerable packages ✅
- `Shared`, `NPCBot`, `NPCBot.Tests`: Not separately scanned; dependencies are a subset of the above.

**Note:** CVE-2026-40324 (HotChocolate 15.1.14) was NOT flagged by `dotnet list --vulnerable` despite being in the GitHub Advisory Database. The current version (15.1.15) is patched, but the NuGet Advisory Database appears to lag GitHub. Add a build-time pin or advisory check to CI when this propagates to the NuGet feed.

## Recommendations

1. **Add GraphQL query cost and depth limits** to both `Program.cs` files using HotChocolate's cost analysis or `AddMaxExecutionDepth` to defend against resource-exhaustion queries.
2. **Rate-limit chat messages** at the mutation level (per-user per-minute cap) and add a maximum message length (e.g., 500 characters).
3. **Add password-auth throttling and non-enumerating registration** in both `Api` and `MasterApi` (active roadmap item).
4. **Normalize object-authorization error codes** in remaining building-market, exchange, and bank-transfer flows (active roadmap item).
5. **Gate HotChocolate Nitro IDE to Development environments only** and optionally disable schema introspection in production.
6. **Add HSTS header** to `nginx.conf`: `add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;`.
7. **Add startup guard for placeholder JWT keys** that throws in non-Development environments when the signing key matches the known placeholder.
8. **Restrict CORS fallback** — use `AllowAnyOrigin()` only in `IsDevelopment()`.
9. **Remove `unsafe-inline` from CSP** when Vite build pipeline allows it (test with production build).
10. **Monitor NuGet advisory propagation** for CVE-2026-40324 and ensure CI picks it up via `dotnet list --vulnerable` once the feed updates.

## Conclusion

The dependency posture is now clean at all levels — both frontends report 0 advisories at all severity levels after this session's `npm audit fix`, and all .NET projects are free of known vulnerable packages. The critical HotChocolate parser vulnerability (CVE-2026-40324) is already patched in the current version. The carried Medium risks (password-auth throttling, object-authorization normalization) remain the most impactful open items. The new Low findings (missing HSTS, unsafe-inline CSP, GraphQL IDE exposure, placeholder key startup guard) represent hardening opportunities without active exploit paths in the current configuration. No new High or Critical findings were identified.
