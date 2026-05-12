# Security Audit Report — 2026-W22 (2026-05-12)

## Auditor
GitHub Copilot using Claude Sonnet 4.6, acting as Emil Security Auditor. Audit conducted 2026-05-12.

## Audited Projects
All projects at repository revision `ea66fb3164297bd0d89b7691977c458bb5e76f5f` (HEAD of `main`):
- `projects/Api` — ASP.NET Core 10 game backend, HotChocolate 15.1.15
- `projects/MasterApi` — ASP.NET Core 10 master-server backend, HotChocolate 15.1.15
- `projects/Shared` — Shared class library
- `projects/Api.Tests` — Integration test suite
- `projects/MasterApi.Tests` — Master API integration tests
- `projects/NPCBot` — NPC bot console application
- `projects/frontend` — Vue 3 + Vite game frontend (nginx-served)
- `projects/master-frontend` — Vue 3 + Vite master portal frontend (no nginx.conf)

## Summary

This audit (W22) verified the closure status of all W21 open findings and performed a fresh sweep of the codebase for new security risks introduced since the last audit. All W21 Medium-severity findings have been resolved. Several Low-severity items that were open in W21 are also now mitigated.

**W21 items now resolved:**
- ✅ GraphQL query depth/complexity limits — fully implemented (`AddMaxExecutionDepth`, `AddCostAnalyzer`, `GraphQlRequestSecurityMiddleware`)
- ✅ Chat rate limiting and message length cap — `ChatRateLimitService` + 500-char limit
- ✅ Password-auth throttling and non-enumerating registration — `LoginThrottleService` + `AuthRateLimitMiddleware`
- ✅ Object-level authorization normalization — `NOT_FOUND_OR_NOT_OWNED` + balance redaction applied
- ✅ GraphQL Nitro IDE gated to `IsDevelopment()` in both APIs
- ✅ HSTS added to `nginx.conf`
- ✅ CSP `unsafe-inline` removed; replaced with SHA-256 hash for the theme bootstrap script
- ✅ JWT signing-key startup guard added to both APIs
- ✅ CORS open fallback restricted to `IsDevelopment()` only
- ✅ Password reset flow implemented in MasterApi

**New findings (W22):**
- **Medium**: SSL certificate validation bypass for the master-server HTTP client is conditioned on the URL containing `"masterapi"` rather than on `IsDevelopment()`. Docker Compose production deployments would bypass TLS for MasterApi registration traffic.
- **Low**: `projects/master-frontend` has no nginx.conf or equivalent deployment-level security headers (HSTS, CSP, X-Frame-Options).
- **Low**: No JWT session revocation mechanism; stolen tokens remain valid for up to 120 minutes.
- **Low (carried)**: Root admin email, default DB password, and `AdminPassword` placeholder remain committed to source.

**Dependency scan results:** All zero vulnerabilities — no changes since W21.

## Findings

### 1) SSL Certificate Validation Bypass for Master-Server HTTP Client
- **Severity:** Medium
- **Affected:** `projects/Api/Program.cs` lines 141–151
- **Description:**
  ```csharp
  if (builder.Configuration["MasterServer:ApiUrl"]?.Contains("masterapi") == true)
  {
      builder.Services.AddHttpClient("master-server").ConfigurePrimaryHttpMessageHandler(() =>
          new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true });
  }
  ```
  The SSL bypass is conditioned on the URL containing the hostname `"masterapi"` — the Docker Compose container name. The comment says "ignore ssl issues in local dev" but Docker Compose production deployments also use `masterapi` as the container hostname. In a production compose stack, the game API silently accepts any TLS certificate from the master API server, making registration and telemetry traffic susceptible to MITM attacks.
- **OWASP:** API10 — Unsafe Consumption of APIs
- **Recommended fix:** Replace with an environment-based or explicit opt-in flag:
  ```csharp
  var disableMasterSsl = builder.Configuration.GetValue<bool>("MasterServer:DisableSslValidation")
      || builder.Environment.IsDevelopment();
  if (disableMasterSsl)
  {
      builder.Services.AddHttpClient("master-server").ConfigurePrimaryHttpMessageHandler(() =>
          new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true });
  }
  else
  {
      builder.Services.AddHttpClient("master-server");
  }
  ```
  Default `MasterServer:DisableSslValidation` should be `false`; only local `appsettings.Development.json` or Docker Compose override sets it to `true`.
- **Status:** Open <!-- issue: #441 -->

### 2) Master-Frontend Has No Deployment-Level Security Headers
- **Severity:** Low
- **Affected:** `projects/master-frontend/` — no nginx.conf exists; no Dockerfile found
- **Description:** The game frontend (`projects/frontend`) serves security headers via `nginx.conf`: HSTS, CSP (with SHA hash), X-Frame-Options DENY, X-Content-Type-Options nosniff, X-XSS-Protection, and Referrer-Policy. The master portal (`projects/master-frontend`) has no equivalent configuration. When deployed, the master portal will be served without these browser protections, making it vulnerable to clickjacking, MIME-sniffing, and SSL-stripping attacks.
- **OWASP:** A05 — Security Misconfiguration
- **Recommended fix:** Add an `nginx.conf` (or a `Dockerfile` that includes one) for the master-frontend with the same security header set as the game frontend. The CSP may need to be adapted for any inline scripts in the master portal's build.
- **Status:** Open <!-- issue: #441 -->

### 3) No JWT Session Revocation Mechanism
- **Severity:** Low
- **Affected:** Both `projects/Api` and `projects/MasterApi` — stateless JWT authentication
- **Description:** Both APIs issue JWTs with a 120-minute TTL and do not maintain any server-side revocation list. Once a token is issued it cannot be invalidated before expiry. Scenarios where this is a problem: (a) player suspects credential compromise and wants to immediately invalidate all sessions; (b) admin wants to force-logout a player under investigation or after a ban; (c) account password change does not invalidate existing sessions.
- **OWASP:** API2 — Broken Authentication
- **Recommended fix:** Implement a server-side token revocation set. At minimum: a database table `RevokedTokens (jti TEXT PK, expiresAtUtc TIMESTAMP)` with a background cleanup job. Add `jti` claim to issued JWTs. On `UseAuthentication`, validate that the `jti` is not in the revoked set. Expose a `logout` mutation that inserts the current token's `jti` into the revoked set.
- **Status:** Open <!-- issue: #441 -->

### 4) Root Admin Email, Default DB Password, and Seed Admin Password in Committed Source
- **Severity:** Low (partially mitigated by startup guards)
- **Affected:** `projects/Api/appsettings.json`, `projects/MasterApi/appsettings.json`
- **Description:**
  - `MasterApi/appsettings.json`: `"RootAdministratorEmails": ["scholtzandcojsa@gmail.com"]` — real email committed to source, high-value social engineering target.
  - `Api/appsettings.json`: `"AdminPassword": "ChangeMe123!"` — default seed admin password. Mitigated by `PasswordAuthEnabled: false` default, but the value should not be committed.
  - Both `appsettings.json` files: `"Password=password"` in connection strings — default DB password committed to source.
  - The JWT placeholder signing key startup guard partially mitigates the JWT risk, but DB passwords and admin emails have no equivalent runtime guard.
- **OWASP:** A02 — Cryptographic Failures, A05 — Security Misconfiguration
- **Recommended fix:** Move `RootAdministratorEmails`, `AdminPassword`, and DB passwords out of committed configuration into environment variables or a secrets manager. Document the required environment variable names in `README.md` and deployment runbooks.
- **Status:** Open <!-- issue: #438 -->

## Previously Open Findings — Now Resolved

The following findings from prior audits (W19, W20, W21) have been verified as resolved:

| Finding | Resolution |
|---|---|
| GraphQL query depth/complexity resource exhaustion | `AddMaxExecutionDepth(10)` + `AddCostAnalyzer` + `GraphQlRequestSecurityMiddleware` in both APIs |
| Chat spam (no rate limit, no length cap) | `ChatRateLimitService` (20/60s) + 500-char limit in `Mutation.Chat.cs` |
| Password-auth throttling and account enumeration | `LoginThrottleService` (5-failure lockout) + `AuthRateLimitMiddleware` (10/min/IP) + neutral duplicate-email response |
| Object-level authorization response leakage | `NOT_FOUND_OR_NOT_OWNED` + balance redaction in building-market, exchange, forex, bank-transfer |
| GraphQL Nitro IDE exposed in production | `Tool.Enable = IsDevelopment()` + `EnableSchemaRequests = IsDevelopment()` in both APIs |
| Missing HSTS header | `Strict-Transport-Security "max-age=31536000; includeSubDomains" always` in `nginx.conf` |
| CSP `unsafe-inline` for scripts | Replaced with `sha256-Rh8m...` hash-based allowlisting in `nginx.conf` |
| Placeholder JWT signing key | `JwtSigningKeyStartupGuard` throws `InvalidOperationException` in non-Development |
| CORS open fallback in production | `CorsPolicyHelper.IsDevelopmentOpenPolicy()` restricts `AllowAnyOrigin()` to Development |
| No password reset flow | Time-limited email reset via `/auth/forgot-password` + `/auth/reset-password` in MasterApi |
| npm frontend vulnerabilities (`ajv`, `minimatch`) | Fixed via `npm audit fix`; both frontends now report 0 advisories |

## Dependency Scan Results

### npm audit (game frontend — `projects/frontend`)
- **`npm audit --omit=dev`:** 0 vulnerabilities ✅
- **Full `npm audit`:** 0 vulnerabilities ✅

### npm audit (master frontend — `projects/master-frontend`)
- **`npm audit --omit=dev`:** 0 vulnerabilities ✅
- **Full `npm audit`:** 0 vulnerabilities ✅

### dotnet list package --vulnerable (all .NET projects via `projects/Api/Api.slnx`)
- `Api`: No vulnerable packages ✅
- `MasterApi`: No vulnerable packages ✅
- `Api.Tests`: No vulnerable packages ✅
- `Shared`: No vulnerable packages ✅
- `NPCBot`: No vulnerable packages ✅

**Note:** CVE-2026-40324 (HotChocolate < 15.1.14) is still not flagged by `dotnet list --vulnerable` despite being in the GitHub Advisory Database. The current version (15.1.15) is patched. Continue monitoring the NuGet Advisory DB for propagation.

## Recommendations

1. **Fix the SSL bypass condition** in `Api/Program.cs` master-server client: use `IsDevelopment()` or a configurable `MasterServer:DisableSslValidation` flag rather than a URL-string match. Medium priority.
2. **Add an nginx.conf (or Dockerfile) to `master-frontend`** with the same security headers as the game frontend: HSTS, CSP (hash-based), X-Frame-Options, X-Content-Type-Options, Referrer-Policy. Low priority.
3. **Implement JWT session revocation** (`jti`-based token blacklist with EF or Redis, `logout` mutation). Low priority but improves incident response capability.
4. **Move committed secrets to environment variables**: `RootAdministratorEmails`, `AdminPassword`, DB password. Use a secrets manager for production. Low priority (startup guard already blocks JWT placeholder misuse).
5. **Continue monitoring dependencies** weekly with `npm audit` and `dotnet list --vulnerable`. The NuGet Advisory DB still hasn't flagged CVE-2026-40324 despite the fix being in a newer version — verify the advisory propagates during W23.

## Conclusion

The W21 open findings have all been resolved in the commits between W21 and W22. The security posture has improved significantly: all major GraphQL attack surfaces are now protected by depth/complexity budgets, production introspection is locked, rate limiting is in place for both auth and chat, CORS is correctly scoped, security headers are set for the game frontend, and password reset is available. The remaining open items are Low severity (one Medium for the SSL bypass in container deployments). No new High or Critical findings were identified. Dependency scanning remains clean at all levels.
