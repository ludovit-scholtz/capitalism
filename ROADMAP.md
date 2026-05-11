# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Onboarding

- [x] (100%) The generated user name - the personal account name is not stored properly. Make sure to store it in the master server if the server does not already contain this information.
- [x] (100%) Generated user personal account name is not used in the ranking. Make sure to use it in the ranking.
- [x] (100%) Do not show the jwt user name anywhere to other players. The user name is generated from the user's algorand address, so for the privacy purposes it is not good to use it

### Consumable Raw Materials & Resource Scarcity Mechanics

- [x] (100%) Add a mine-side extraction history experience in building detail with a 30-day sparkline, depletion trendline, and an expanded dialog that explains reserve burn rate, expected depletion tick, and quality decay inflection points.

### Security Follow-Ups

- [ ] Replace the regex-based support markdown sanitizer with an allowlist HTML sanitizer, and add stored-XSS regression payloads that cover SVG, attribute, protocol, and malformed-markup bypass attempts before any `v-html` support preview is rendered.
- [ ] Finish `NOT_FOUND_OR_NOT_OWNED` plus balance-redaction normalization across building-market, exchange, and bank-transfer mutations so authenticated probes cannot infer foreign object existence, listing state, company linkage, or exact available funds.
- [ ] Add dedicated MasterApi security regression tests for `gameNewsFeed(includeDrafts)` and `upsertGameNewsEntry`, covering anonymous draft reads, invalid registration keys, inactive server keys, spoofed requester identity, trusted server success, and privileged admin success.
- [ ] Remove the committed NPC bot shared default password, require an environment-provided secret or API-key mode outside local development, and fail startup when the placeholder credential is still configured.
- [ ] Upgrade `postcss` in `projects/master-frontend` to `>= 8.5.10` and keep both frontends on a zero known production dependency advisory baseline in CI.
