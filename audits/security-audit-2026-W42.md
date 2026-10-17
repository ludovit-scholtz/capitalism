# 2026-W42 Security Audit

Date: 2026-10-17
Revision audited: `ecbe4819` (`main`, after PRs #604, #606 and #609)
Auditor: Claude (Team A), as part of issue #597.
Scope: dependency and configuration verification, verification of every finding still open in `security-audit-2026-07-07T201327.md`, and review of the object-authorization, ledger and tick-timing changes merged since. This is not a full re-test of every economy path; the last deep fairness pass remains `security-audit-2026-06-20T204234.md`.

## Audit question

Can one player gain an unfair advantage over another player by executing an API call or exploiting unfair game mechanics, and is the shipped configuration still hardened?

## Summary

- No High or Critical findings.
- `npm audit --omit=dev` (what CI enforces) reports 0 vulnerabilities in both frontends. A full `npm audit` reports 4 high advisories in each (`braces` -> `micromatch` -> `fast-glob` -> `@vue/eslint-config-typescript`) that live only in the lint toolchain and never ship.
- `npm ls esbuild` is empty in both frontends (Vite 8 does not install it), so GHSA-g7r4-m6w7-qqqr (finding 3 of the 2026-07-07 audit) no longer applies.
- `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages for `projects/Api`.
- A real defect was found and fixed: the master portal's inline analytics bootstrap was blocked by its own CSP (finding 1).
- The object-authorization hardening started in #604 is incomplete: the remaining inline `COMPANY_NOT_FOUND` sites are tracked in #605 (finding 2).
- Two economy defects found by the new invariant suite (#606) were fixed: an early supply-contract cancellation fee that was only ledgered and never moved money, and NPC lot purchases whose ledger rows carried no bank account.

## Verification of prior-audit findings (2026-07-07)

| # | Finding | Result |
|---|---------|--------|
| 1 | OIDC `RequireHttpsMetadata=false` in base config | Fixed. Both `appsettings.json` files set `true`, `MasterApi/Security/BiatecOidcOptions.cs` defaults to `true`, and `Api/Program.cs` fails fast outside Development when OIDC is enabled with HTTP metadata. |
| 2 | Runtime patch currency depends on push cadence | Fixed. `api-ci-cd.yml` and `frontend-ci-cd.yml` rebuild weekly via `cron: '0 4 * * 1'`. |
| 3 | esbuild dev-server advisory | Moot, see summary. |
| 4 | nginx `add_header` inheritance drops security headers | Fixed. Locations that declare their own headers repeat the security set (`projects/frontend/nginx.conf`). |
| 5 | Missing `Permissions-Policy`, "stale" script hash | `Permissions-Policy` is now set. The hash claim was wrong: `sha256-Rh8mWJSD...` is the hash of the remaining inline script in `projects/frontend/index.html` and must stay. |

## Risk register

### 1) Master portal inline analytics script blocked by the CSP

- **Severity:** Medium (availability of telemetry; no confidentiality or integrity impact)
- **Affected endpoint or mechanic:** `projects/master-frontend/index.html`, `projects/master-frontend/nginx.conf`
- **Risk description:** The page contained an inline `<script>` bootstrapping gtag, while the CSP is `script-src 'self' https://www.googletagmanager.com` with no hash or `unsafe-inline`. Browsers blocked the script, so analytics silently never initialised on the master portal. The CSP itself was correct; the page violated it.
- **Recommended fix:** Serve the bootstrap from a same-origin file, as the game frontend already does, and add a regression test that fails on inline scripts.
- **Status:** Fixed in #597 (`public/gtag-init.js`, `indexHtmlCsp.test.ts`).

### 2) Object-authorization responses not yet uniform everywhere

- **Severity:** Medium
- **Affected endpoint or mechanic:** Mutations in `Mutation.BuildingBankAccount.cs`, `Mutation.Lending.cs`, banking, stock exchange and bank statement resolvers that still throw `COMPANY_NOT_FOUND`/`BANK_ACCOUNT_NOT_FOUND` for foreign ids and emit no audit event.
- **Risk description:** A caller can distinguish a missing company or account from one they do not own, which allows enumeration of competitor ids. #604 converted about 35 sites to a uniform `FORBIDDEN` with an audit event; the rest remain.
- **Recommended fix:** Convert the remaining sites to `ObjectAuthorizationService.Deny`, add missing-versus-foreign parity tests per resolver, and update the e2e mocks together with the real codes.
- **Status:** Open <!-- issue: #605 -->

### 3) Lint-toolchain advisories in both frontends

- **Severity:** Low (development only)
- **Affected endpoint or mechanic:** `projects/frontend` and `projects/master-frontend` devDependencies (`@vue/eslint-config-typescript` -> `fast-glob` -> `micromatch` -> `braces`, GHSA-vfj7-8cjw-p6xm).
- **Risk description:** Stack-exhaustion denial of service in a glob helper, reachable only when linting developer-controlled files. Nothing from this chain is in the production bundle or image. No patched release exists: `braces@3.0.3` is the latest published version and is inside the vulnerable range (`<=3.0.3`), and the only `npm audit fix` proposal is a semver-major downgrade of the lint config to 14.0.1.
- **Recommended fix:** Treated as an accepted risk (no upstream fix) until upstream publishes a fixed `braces`; re-check `npm view braces version` in each weekly audit and do not apply `npm audit fix --force`. CI keeps gating production dependencies with `npm audit --omit=dev`.
- **Status:** Open <!-- issue: #611 -->
