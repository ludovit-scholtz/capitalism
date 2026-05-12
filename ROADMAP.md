# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Onboarding

- [x] (100%) Personal account name is generated in the onboarding process before user signs in. The game server now resolves public player labels from the stored player profile across rankings, chat, account ownership labels, and player GraphQL surfaces instead of exposing JWT auth names.

### Security Follow-Ups

- [x] (100%) Finished `NOT_FOUND_OR_NOT_OWNED` plus balance-redaction normalization across building-market, exchange, and bank-transfer mutations so authenticated probes cannot infer foreign object existence, listing state, company linkage, or exact available funds.
- [x] Add password-auth abuse controls across `projects/Api` and `projects/MasterApi`: account-aware login throttling or temporary lockout, endpoint rate limiting, duplicate-email response normalization, and monitoring for repeated failed attempts. *(100% — LoginThrottleService with 5-failure lockout, AuthRateLimitMiddleware with 10 req/IP/min, neutral duplicate-email message, structured lockout warning logs; disabled in Development/Testing)*
- [x] (100%) Added HotChocolate query-budget enforcement in both `Api` and `MasterApi` (`GraphQL:MaxDepth`, `GraphQL:MaxComplexity`, `GraphQL:MaxPageSize`), wired cost analyzer + weighted `[Cost]` fields, and standardized `MAX_DEPTH_EXCEEDED` / `MAX_COMPLEXITY_EXCEEDED` responses with security warning logs.
- [x] (100%) Rate-limit the `SendChatMessage` mutation per authenticated user (20 messages/60 seconds) and enforce a maximum message length of 500 characters to prevent chat spam and database bloat. *(ChatRateLimitService with sliding-window IMemoryCache counter, structured RATE_LIMITED/MESSAGE_TOO_LONG errors, WARNING-level violation logs; frontend character counter at 450 chars, red highlight, toast on rate-limit)*
- [x] (100%) HotChocolate Nitro IDE and schema introspection are now gated to `IsDevelopment()` only in both APIs, with non-development introspection requests returning `FORBIDDEN`.
- [x] (100%) Added startup guard in both APIs that throws `InvalidOperationException` when `Jwt:SigningKey` is placeholder/insecure (null, whitespace, short, or known placeholder) outside Development and logs a critical startup-block event with `Jwt__SigningKey` override guidance.
- [ ] Restrict CORS open fallback (`AllowAnyOrigin()`) to `IsDevelopment()` only; non-Development deployments with an empty `Cors:AllowedOrigins` list should reject all cross-origin requests with a warning log.
- [ ] Add `Strict-Transport-Security` header to `projects/frontend/nginx.conf` with `max-age=31536000; includeSubDomains`.
- [ ] Remove `unsafe-inline` from `script-src` in `projects/frontend/nginx.conf` CSP header; verify production Vite bundle works without it and implement nonce-based CSP if inline scripts are required.
- [ ] Implement a time-limited email-based password reset flow (or document OIDC re-linkage as the only recovery path) to prevent permanent player lock-out on credential loss.
- [ ] Move `RootAdministratorEmails` and database credentials out of committed `appsettings.json` into environment-variable configuration or a secrets manager.
