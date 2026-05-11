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

### Capital Markets & Shareholder Governance

- [x] (100%) Deliver company-share ownership, shareholder registry visibility, stock buy/sell ownership updates, dividend vote governance, and dividend payout settlement with weighted shareholder voting and portfolio/net-worth tracking.

### Security Follow-Ups

- [ ] Finish `NOT_FOUND_OR_NOT_OWNED` plus balance-redaction normalization across building-market, exchange, and bank-transfer mutations so authenticated probes cannot infer foreign object existence, listing state, company linkage, or exact available funds.
- [ ] Add password-auth abuse controls across `projects/Api` and `projects/MasterApi`: account-aware login throttling or temporary lockout, endpoint rate limiting, duplicate-email response normalization, and monitoring for repeated failed attempts.
