# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Onboarding

- [x] (100%) Personal account name is generated in the onboarding process before user signs in. The game server now resolves public player labels from the stored player profile across rankings, chat, account ownership labels, and player GraphQL surfaces instead of exposing JWT auth names.

### Security Follow-Ups

- [ ] Move `RootAdministratorEmails` and database credentials out of committed `appsettings.json` into environment-variable configuration or a secrets manager.
- [ ] Implement JWT session revocation: maintain a server-side token revocation set (Redis or DB) to support explicit logout and admin-initiated session termination. Currently stateless JWTs remain valid for up to 120 minutes after compromise is detected.
- [ ] Fix SSL certificate validation bypass for master-server HTTP client: the bypass is conditioned on URL containing "masterapi" (container hostname) rather than on `IsDevelopment()`, meaning it activates in Docker Compose production deployments. Replace with an environment-based or explicit development-only bypass.
- [ ] Add security headers to `projects/master-frontend` deployment: the master portal has no nginx.conf and relies on the Vite dev server or static hosting without HSTS, CSP, X-Frame-Options, or X-Content-Type-Options headers.
