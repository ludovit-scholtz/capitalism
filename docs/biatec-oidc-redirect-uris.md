# Biatec OIDC (PKCE) — redirect URIs

Client configuration used across this repo (see `OIDC_INTEGRATION_GUIDE.md`):

| Setting | Value |
|---|---|
| Authorization endpoint | `https://google.biatec.io/authorize` |
| Token endpoint | `https://google.biatec.io/token` |
| Client ID | `capitalism-pkce` |
| Code challenge method | `S256` |
| Scope | `openid profile email` |
| Client authentication | none (public client) — `client_id` + `code_verifier` in the token request body, no `client_secret` |

Because `capitalism-pkce` is a **public client** (`ClientSecret: null` server-side), every
redirect URI it will ever use must be added to that client's `RedirectUris` allowlist on the
Biatec IdP. A request with an unlisted `redirect_uri` is rejected outright — there's no
wildcard-everything fallback. Allowlist entries support `*` wildcards in host and path, but
**scheme and port must match exactly** (`http` vs `https`, `:5173` vs no port, are all distinct
entries).

## What to register, per surface

### Game frontend (`projects/frontend`)

- **Production**: `https://capitalism.de-4.biatec.io/auth/callback`
  (set via `APP_BIATEC_OIDC_REDIRECT_URI` in `projects/frontend/deploy/k8s/deployment.yaml`).
- **Local dev**: `http://localhost:5173/auth/callback`
  (default in `projects/frontend/.env`'s `VITE_BIATEC_OIDC_REDIRECT_URI`).

If you deploy additional environments (staging, preview URLs), either register each one
explicitly or use a host wildcard your infra can guarantee, e.g. `https://*.de-4.biatec.io/auth/callback`.

### Master frontend (`projects/master-frontend`)

- **Production**: `https://<your-master-frontend-domain>/auth/callback` — there's no committed
  k8s manifest for this app yet, so set `APP_BIATEC_OIDC_REDIRECT_URI` (build/runtime env) or
  `VITE_BIATEC_OIDC_REDIRECT_URI` to whatever domain you deploy it to, and register that exact
  value with Biatec.
- **Local dev**: `http://localhost:5174/auth/callback`
  (default in `projects/master-frontend/.env`).

### Flutter app (`projects/flutter_app`)

Native platforms don't have an origin to redirect back to, so each one uses a different
redirect URI shape (see `lib/core/auth/biatec_oidc_service.dart` and
`lib/core/auth/biatec_oidc_config.dart`):

- **Android / iOS**: a custom URL scheme, `io.biatec.capitalism://oidc-callback`
  (`BiatecOidcConfig.mobileCallbackScheme`, wired into `AndroidManifest.xml`'s
  `CallbackActivity` intent filter; iOS needs no manifest entry —
  `ASWebAuthenticationSession` intercepts it at the OS level). Register this literal string
  with Biatec — it is not a real host, so wildcards don't apply.
- **Windows / Linux (desktop)**: a loopback URL, `http://localhost:42815`
  (`BiatecOidcConfig.desktopCallbackPort`; `flutter_web_auth_2` opens the system browser and
  binds a local listener on this port instead of using a URL scheme). Because this is `http://`
  on localhost, the Biatec IdP config needs `AllowHttpForLoopbackRedirectUris: true` — that
  setting is scoped to loopback addresses only, so it does not weaken `https://` requirements
  elsewhere.
- Both are also registered as **post-logout redirect URIs** (RP-initiated logout), since the
  federated sign-out flow returns to the same place sign-in did.
- If you change the mobile scheme or desktop port (`BIATEC_OIDC_CALLBACK_SCHEME` /
  `BIATEC_OIDC_DESKTOP_PORT` `--dart-define`s), update the Biatec allowlist to match — a
  mismatch fails the same way an unlisted web redirect_uri does.

## Why PKCE here (no client_secret to leak)

`capitalism-pkce` is shared across a browser SPA and a native/desktop app, none of which can
keep a secret confidential (it would ship inside the JS bundle or the app binary). PKCE
(`code_challenge`/`code_verifier`) replaces the client secret as proof that whoever exchanges
the authorization code is the same party that started the `/authorize` request — see
`startBiatecOidcSignIn`/`getTokenFromCallback` in `projects/frontend/src/stores/auth.ts` (and
the mirrored `projects/master-frontend/src/stores/auth.ts`, and
`BiatecOidcService._exchangeAuthorizationCode` in the Flutter app) for the client-side
implementation.
