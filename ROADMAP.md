# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Onboarding

- [x] (100%) The generated user name - the personal account name is not stored properly. Make sure to store it in the master server if the server does not already contain this information.
- [ ] Personal account name is generated in the onboarding process before user signs in. However in the game server the name displayed is different. It is the JWT auth name, not the generated personal account name. Make sure to use proper personal account name in the ranking in the game server. Do not use the jwt name anywhere. 
- [x] (100%) Generated user personal account name is not used in the ranking. Make sure to use it in the ranking.
- [x] (100%) Do not show the jwt user name anywhere to other players. The user name is generated from the user's algorand address, so for the privacy purposes it is not good to use it

### Consumable Raw Materials & Resource Scarcity Mechanics

- [x] (100%) Add a mine-side extraction history experience in building detail with a 30-day sparkline, depletion trendline, and an expanded dialog that explains reserve burn rate, expected depletion tick, and quality decay inflection points.

### Power Plant & Energy Management

- [x] (100%) Deliver foundational city power-grid gameplay with buildable power plants, per-building power demand/online states, tick-based production and fuel economics, city energy planning UI, and full backend/frontend regression coverage.

### Security Fairness Hardening

- [x] (100%) Introduce API key scopes and enforcement gates so leaked keys cannot act as full-account impersonation; support read-only, bot-only, trading-only, and company-bound scopes with deny-by-default middleware enforcement and audit logging.
- [x] (100%) Implement shard-verified ranking telemetry by validating `serverKey` against active server registration metadata, rejecting unknown or stale shard keys, and logging replay or duplicate event signatures for moderation review.
- [x] (100%) Harden FX execution fairness by adding quote nonce, quote-issued timestamp, strict expiration, and explicit slippage tolerance so stale quotes and replayed execution payloads cannot extract better-than-market settlement.
- [x] (100%) Expand building secondary market race-condition defenses using optimistic concurrency tokens on offer accept/cancel/buy paths and add parallel integration tests proving no double-fill or stale ownership transfer can occur.
- [x] (100%) Strengthen loan collateral and foreclosure invariants by revalidating ownership, collateral lock, and payable balance at commit-time on every refinance, sale, destroy, and default transition path with overdue lifecycle tests.
- [x] (100%) Add strict token-boundary tests and middleware assertions so MasterApi privilege cannot be granted from game-issued tokens, and impersonation is recognized only when actor and effective-player claims differ.
- [x] (100%) Enforce bot company/account boundaries in every bot-eligible mutation by resolving ownership server-side, rejecting foreign-company identifiers with `NOT_FOUND_OR_NOT_OWNED`, auditing rejected API-key attempts, and covering forex, building, lending, and stock violations with regression tests.
- [x] (100%) Complete ranking manipulation safeguards with idempotency keys, proof-reference deduplication, suspicious-pattern moderation queues, and admin review tooling that can quarantine telemetry batches before leaderboard publication.
- [x] (100%) Keep frontend trust non-authoritative by validating economy-sensitive stock trade ownership overrides server-side, rejecting tampered account-type/company payloads with `INVALID_CLIENT_OVERRIDE`, and adding backend plus Playwright regressions proving friendly client-error handling.
- [x] (100%) Standardize object-authorization failure responses to "not found or not owned" semantics to reduce resource enumeration risk while preserving internal audit visibility through structured security logs.
- [x] (100%) Close the MasterApi news-service trust-boundary gap by requiring validated game-server credentials or authenticated root/global admin claims for `gameNewsFeed(includeDrafts: true)` and `upsertGameNewsEntry`, rejecting caller-supplied email spoofing, and covering anonymous draft read/write rejection with regression tests.
- [x] (100%) Finish the error-surface hardening pass across building, banking, lending, and stock mutations so unauthorized probes no longer distinguish foreign-object existence, listing state, or precise available-balance details.

### Security Operations & Audit Cadence

- [x] (100%) Create a weekly security action board that mirrors `/audits/*.md` findings, tracks owner plus due tick, and blocks release sign-off when any High or Critical finding from the latest audit has no linked implementation issue.
- [x] (100%) Add an automated GraphQL surface inventory report in CI that flags newly added finance, shareholder, ranking, lending, and admin queries or mutations missing explicit auth and ownership tests.
- [x] (100%) Add a frontend dependency-audit release gate that runs `npm audit --omit=dev` for both frontends, tracks reachable rich-content sinks such as `dompurify` plus `v-html`, and blocks release validation while known high-severity production advisories remain unresolved.

### Endgame & Win Condition — "Race to the Top"

- [x] (100%) Backend win detection via `EndgamePhase` (Order=1200): wealth calculated from cash, shares, gold, LP positions; winner recorded in `GameState`.
- [x] (100%) `GameEndedMutationGuardMiddleware` blocks all GraphQL mutations with `GAME_ENDED` error code once the shard is over.
- [x] (100%) `RealWorldBillionaire` entity seeded with top-10 real-world billionaires; Elon Musk at $430B as winning threshold.
- [x] (100%) `GetEndgameStatus` GraphQL query returning game-ended state, winner details, threshold, and real-world leaderboard.
- [x] (100%) `UpdateRealWorldBillionaire` admin mutation for updating benchmark entries.
- [x] (100%) `EndShardManually` admin mutation: force-ends the shard, crowns the current bank-balance leader, publishes 3-locale newsletter.
- [x] (100%) `useEndgameStore` Pinia store with 60-second polling, `progressPercent()`, and `checkMilestones()` milestone tracker.
- [x] (100%) Milestone toast notifications at 1%, 10%, 25%, 50%, 75%, and 90% of winning threshold in `PersonalLedgerView`.
- [x] (100%) "Race to the Top" panel in `PersonalLedgerView` with ARIA-accessible progress bar, benchmark table, and gap calculation.
- [x] (100%) Winner overlay and read-only banner in `App.vue` using `useEndgameStatus` composable.
- [x] (100%) Lock icon in `AppHeader` navbar with tooltip when shard has ended.
- [x] (100%) Admin "End Shard" control in `AdminDashboardContent` with confirmation dialog and reason field.
- [x] (100%) i18n keys for all endgame UI in en, sk, and de locales.
- [x] (100%) 14 backend integration tests covering win detection, mutation guard, benchmark admin, manual shard end, unauthenticated access, validation errors, multi-currency leader selection, and post-end mutation blocking.
- [x] (100%) 11 frontend unit tests for `useEndgameStore` covering polling, milestone logic, and progress computation.
- [x] (100%) 8 E2E Playwright tests in `finance/endgame.spec.ts`: billionaire panel, ARIA progress bar, winner overlay/read-only banner, admin End Shard visibility, End Shard full confirmation flow, End Shard cancel flow, non-admin access denial, and navbar lock icon.

### Security Follow-Ups

- [x] (100%) Replace the regex-based support markdown sanitizer with an allowlist HTML sanitizer, and add stored-XSS regression payloads that cover SVG, attribute, protocol, and malformed-markup bypass attempts before any `v-html` support preview is rendered.
- [ ] Finish `NOT_FOUND_OR_NOT_OWNED` plus balance-redaction normalization across building-market, exchange, and bank-transfer mutations so authenticated probes cannot infer foreign object existence, listing state, company linkage, or exact available funds.
- [ ] Add dedicated MasterApi security regression tests for `gameNewsFeed(includeDrafts)` and `upsertGameNewsEntry`, covering anonymous draft reads, invalid registration keys, inactive server keys, spoofed requester identity, trusted server success, and privileged admin success.
- [x] (100%) Remove the committed NPC bot shared default password, require an environment-provided secret or API-key mode outside local development, and fail startup when the placeholder credential is still configured.
- [ ] Upgrade `postcss` in `projects/master-frontend` to `>= 8.5.10` and keep both frontends on a zero known production dependency advisory baseline in CI.
