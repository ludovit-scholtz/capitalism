# Gap: no real token-refresh grant for the Flutter app's Biatec OIDC session

## Symptom this documents

Players who sign in via Biatec OIDC on the Flutter app get logged out /
start seeing "not authenticated" errors after their token expires (Biatec
tokens have historically been issued with short lifetimes, ~1-2 hours),
even though they were successfully authenticated earlier the same day.
There was no code in the app that noticed a token was about to expire and
renewed it — the app just kept sending an expired Bearer token until every
request failed.

This has now been mitigated client-side (see "What was implemented"
below), but the underlying gap in the OIDC provider contract is still
worth an issue, because the mitigation is a workaround, not a
spec-compliant fix.

## What was implemented (client-side workaround)

- `AuthState` (`lib/core/auth/auth_state.dart`) now persists `expiresAtUtc`
  and the auth `provider` (`local` vs `biatec_oidc`) alongside the token
  (`lib/core/auth/token_storage.dart`).
- `GraphQlService.request()` (`lib/core/graphql/graphql_service.dart`)
  calls `AuthState.ensureFreshToken()` before every request, which renews
  the token if it's within 60 seconds of expiry (or already expired)
  before the request goes out. This is checked on demand per request
  rather than on a background `Timer`, deliberately: the app has no single
  owner that reliably `dispose()`s a long-lived `AuthState` (it's a
  `provider`-scoped singleton for the whole app lifetime, and widget tests
  construct/discard many short-lived instances that never call
  `dispose()`), so a background timer either leaks past disposal or needs
  lifecycle wiring threaded through every screen. An on-demand check has
  no such lifecycle to manage, at the cost of only renewing when a request
  actually happens (acceptable — a genuinely idle app has no requests to
  protect).
- Renewal itself (`AuthState._doRenewSilently`) calls
  `BiatecOidcService.signIn(silent: true)`
  (`lib/core/auth/biatec_oidc_service.dart`), which repeats the full
  authorization-code + PKCE flow with `prompt=none` added to the
  `/authorize` request (OpenID Connect Core 1.0 §3.1.2.1). This mirrors
  what the web app already does in `startBiatecOidcSignIn(path, {
  silentPrompt: true })` (`projects/frontend/src/stores/auth.ts`).
- As a last-resort net, `GraphQlService` now also detects an
  authentication failure reactively (HTTP 401 session-revoked responses,
  and `AUTH_NOT_AUTHORIZED` GraphQL errors corroborated by a locally
  tracked expired token) and forces a local logout so the app doesn't keep
  hammering the backend with a dead token.

## Why this is a workaround, not a real fix

A `prompt=none` re-authorization round trip is **not** the standard OAuth
2.0 / OIDC mechanism for renewing an access token. The standard mechanism
is the **refresh token grant** (RFC 6749 §6): the client requests the
`offline_access` scope at initial sign-in, the token endpoint returns a
`refresh_token` alongside the access/id token, and the client later POSTs
`grant_type=refresh_token&refresh_token=...` directly to the token
endpoint — no browser round trip, no dependency on the system browser
still holding a live IdP session cookie, and it works when the device is
briefly offline-then-online.

`prompt=none` instead depends on:

- The OIDC provider actually supporting `prompt=none` and honoring it by
  checking for an existing IdP session rather than always showing a login
  page.
- The mobile OS's in-app browser component (Custom Tabs on Android,
  `ASWebAuthenticationSession` on iOS) sharing a persistent cookie jar
  with the system browser, so the IdP's session cookie from the original
  interactive sign-in is still there. This is generally true, but is a
  platform implementation detail, not a protocol guarantee — and
  `ASWebAuthenticationSession` in particular can be configured (by the
  IdP or the OS) to use an ephemeral, non-shared session.
- On Windows/Linux (`flutter_web_auth_2`'s `useWebview: false` /
  system-browser + loopback-listener path, see
  `BiatecOidcService._authenticator.authenticate`), a full system browser
  window still gets asked to navigate, which can be visually janky (a
  flash of a browser window) compared to a silent background HTTP POST.
- If the provider does **not** support `prompt=none` and instead always
  shows an interactive login page, this renewal path silently degrades
  into "pop a login page in front of the player every hour," which is
  worse than doing nothing.

## What to check with Biatec / the OIDC provider team before filing an issue

1. **Does the `google.biatec.io` authorize endpoint support
   `prompt=none`** per spec (respond immediately with either a redirect
   containing `code=...` if there's a live session, or
   `error=login_required` / `error=interaction_required` if not — never
   show interactive UI)? The web app already assumes this
   (`scheduleTokenRenewal` in `projects/frontend/src/stores/auth.ts`), so
   if it doesn't actually work today, the web app has the same silent
   bug, just less visible because a background `window.location.assign`
   redirect is invisible until it fails, whereas Flutter's browser tab is
   more visually obvious.
2. **Does the token endpoint (`BiatecOidcConfig.tokenUrl`) support
   `grant_type=refresh_token`, and can the client request the
   `offline_access` scope** to receive a `refresh_token` in the initial
   authorization-code exchange response? If yes, both the web and Flutter
   apps should be migrated off `prompt=none` re-auth and onto a proper
   refresh-token exchange — no browser round trip needed at all, which
   also fixes the visible-browser-flash issue on desktop.
3. **What is the actual configured access-token lifetime**, and is it
   likely to change? `BiatecOidcService._exchangeAuthorizationCode`
   currently falls back to a 120-minute assumption if neither the `exp`
   JWT claim nor an `expiresIn` field is present in the token response —
   worth confirming that fallback is never actually hit in practice.

## Related, separate gap: local (email/password) sessions have no renewal at all

This document is scoped to the Biatec OIDC gap the user asked about, but
while investigating this, note that **local password-authenticated
sessions (`AuthPayload.token`/`expiresAtUtc` from the `login`/`register`
mutations in `projects/Api/Types/Mutation.Auth.cs`) have no renewal
mechanism on either the web or Flutter app** — there is no refresh-token
mutation or endpoint anywhere in `projects/Api` or `projects/MasterApi`.
A local session simply expires and the player must log in again. If
that's also worth fixing, it would need a new backend mutation (e.g.
`refreshToken(token: String!): AuthPayload`) issuing a new JWT for a
still-valid-but-expiring one, which is out of scope for this Flutter-side
change.
