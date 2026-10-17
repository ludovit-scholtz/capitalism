# Economy invariant suite

`EconomyInvariantTests` drives the real `TickProcessor` plus seeded random player actions
(`transferFunds`, supply-contract propose/accept/reject/cancel, `executeForexSwap` between a company's accounts in two currencies, and the building sale flow `setBuildingForSale` -> `makeOfferOnBuilding` -> `acceptBuildingOffer`) over several players and asserts, every 10 steps
and at the end (`EconomyInvariantChecker`):

1. **No negative balances** on non-government bank accounts.
2. **Building ownership** - every building has exactly one existing owning company.
3. **Matched ledger** - each ledger row naming a bank account points at an existing account of the same company.
4. **Conservation** - per company, the change in total bank balances since the baseline equals the sum of its
   new ledger rows (no money appears or vanishes without a bank-statement row).

Run: `dotnet test projects/Api.Tests --filter FullyQualifiedName~EconomyInvariantTests`.
Add a seed to the `[InlineData]` list to widen coverage. A failure prints `seed`, `step`, `tick` and the offending
entity ids; re-run that single seed to reproduce. The action sequence is fully determined by `new Random(seed)`; tick-phase internals (NPC phases, `Random.Shared` account numbers, wall-clock timestamps) are not seeded, so a rare failure may need a few re-runs. A scripted propose/accept/buyer-cancel, one forex swap and one building sale run first in every seed (each asserts success), so those money paths are always covered. The seeded NPC companies run in every tick and are covered by the per-company conservation check, but they take no part in the random actions.
The `Checker_Detects*` tests prove each invariant fails on a deliberately corrupted state.
