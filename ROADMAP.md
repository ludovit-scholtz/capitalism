# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Onboarding

- [x] (100%) Personal account name is generated in the onboarding process before user signs in. The game server now resolves public player labels from the stored player profile across rankings, chat, account ownership labels, and player GraphQL surfaces instead of exposing JWT auth names.

### Security Follow-Ups

- [x] (100%) Finished `NOT_FOUND_OR_NOT_OWNED` plus balance-redaction normalization across building-market, exchange, and bank-transfer mutations so authenticated probes cannot infer foreign object existence, listing state, company linkage, or exact available funds.
- [ ] Add password-auth abuse controls across `projects/Api` and `projects/MasterApi`: account-aware login throttling or temporary lockout, endpoint rate limiting, duplicate-email response normalization, and monitoring for repeated failed attempts.
- [ ] Add GraphQL query depth and complexity limits to both `Api` and `MasterApi` `Program.cs` using HotChocolate cost-analysis to prevent resource-exhaustion via deeply joined or large-field queries.
- [ ] Rate-limit the `SendChatMessage` mutation per authenticated user (e.g., 20 messages/minute) and enforce a maximum message length (e.g., 500 characters) to prevent chat spam and database bloat.
- [ ] Gate HotChocolate Nitro IDE to `IsDevelopment()` only; disable schema introspection in production environments to reduce automated enumeration attack surface.
- [ ] Add startup guard in both APIs that throws `InvalidOperationException` when `Jwt:SigningKey` matches the committed placeholder value in non-Development environments.
- [ ] Restrict CORS open fallback (`AllowAnyOrigin()`) to `IsDevelopment()` only; non-Development deployments with an empty `Cors:AllowedOrigins` list should reject all cross-origin requests with a warning log.
- [ ] Add `Strict-Transport-Security` header to `projects/frontend/nginx.conf` with `max-age=31536000; includeSubDomains`.
- [ ] Remove `unsafe-inline` from `script-src` in `projects/frontend/nginx.conf` CSP header; verify production Vite bundle works without it and implement nonce-based CSP if inline scripts are required.
- [ ] Implement a time-limited email-based password reset flow (or document OIDC re-linkage as the only recovery path) to prevent permanent player lock-out on credential loss.
- [ ] Move `RootAdministratorEmails` and database credentials out of committed `appsettings.json` into environment-variable configuration or a secrets manager.
