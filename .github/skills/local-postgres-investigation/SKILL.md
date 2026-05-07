---
name: local-postgres-investigation
description: "Use when: investigating local gameplay state in PostgreSQL, checking building upgrades, inspecting pending plans, or querying the local game1 database on localhost:5432. Includes the exact connection string and a safe read-only workflow."
---

# Local PostgreSQL Investigation

Use this skill when you need to inspect the local Capitalism game database directly during debugging.

## Connection string

Use this exact connection string:

```text
Host=localhost;Port=5432;Database=game1;Username=postgres;Password=password;SSL Mode=Disable
```

## Safety rules

- Default to read-only SQL.
- Start with `SELECT` queries only.
- Do not run `UPDATE`, `DELETE`, `INSERT`, `TRUNCATE`, or schema changes unless the user explicitly asks.
- For gameplay investigations, check `GameStates`, `Buildings`, `BuildingConfigurationPlans`, `BuildingConfigurationPlanUnits`, `BuildingConfigurationPlanRemovals`, `BankAccounts`, and `Cities` first.

## Preferred workflow

1. Confirm PostgreSQL is listening on `localhost:5432`.
2. Use a PostgreSQL client if available.
3. If `psql` is not installed, use an existing .NET or Npgsql-based helper.
4. For building-upgrade issues, inspect all of:
   - the building row
   - the pending configuration plan
   - pending plan units and removals
   - the building's assigned bank account
   - the city currency
   - the current game tick

## Example read-only queries

### Current game tick

```sql
select "CurrentTick"
from "GameStates"
limit 1;
```

### Building and assigned bank account

```sql
select
  b."Id",
  b."Name",
  b."Type",
  b."CityId",
  b."BankAccountId",
  c."Name" as "CityName",
  c."CurrencyCode" as "CityCurrency",
  ba."AccountNumber",
  ba."CurrencyCode" as "BankCurrency",
  ba."Balance"
from "Buildings" b
left join "Cities" c on c."Id" = b."CityId"
left join "BankAccounts" ba on ba."Id" = b."BankAccountId"
where b."Id" = '<building-id>';
```

### Pending building configuration plan

```sql
select
  p."Id",
  p."BuildingId",
  p."SubmittedAtTick",
  p."AppliesAtTick",
  p."TotalTicksRequired"
from "BuildingConfigurationPlans" p
where p."BuildingId" = '<building-id>';
```

### Pending plan units

```sql
select
  u."GridX",
  u."GridY",
  u."UnitType",
  u."IsChanged",
  u."StartedAtTick",
  u."AppliesAtTick",
  u."TicksRequired",
  u."MinPrice",
  u."MaxPrice"
from "BuildingConfigurationPlanUnits" u
join "BuildingConfigurationPlans" p on p."Id" = u."BuildingConfigurationPlanId"
where p."BuildingId" = '<building-id>'
order by u."GridY", u."GridX";
```

### Pending removals

```sql
select
  r."GridX",
  r."GridY",
  r."StartedAtTick",
  r."AppliesAtTick",
  r."TicksRequired",
  r."IsReverting"
from "BuildingConfigurationPlanRemovals" r
join "BuildingConfigurationPlans" p on p."Id" = r."BuildingConfigurationPlanId"
where p."BuildingId" = '<building-id>'
order by r."GridY", r."GridX";
```

## Upgrade-specific interpretation guide

If a queued building upgrade keeps moving to `currentTick + 1` every tick, inspect the funding path:

- If the building account currency does not match the building city currency, the upgrade will be deferred.
- If the assigned building account balance is below the activation cost of the due unit changes, the upgrade will be deferred.
- Link-only changes cost no construction money, but adding a new unit or changing unit type does.

Key backend files involved:

- `projects/Api/Utilities/BuildingConfigurationService.cs`
- `projects/Api/Utilities/BuildingConfigurationService.Plan.cs`
- `projects/Api/Utilities/BuildingConfigurationService.Blockers.cs`
- `projects/Api/Engine/Phases/BuildingUpgradePhase.cs`
- `projects/Api/Utilities/BuildingConfigurationEconomics.cs`

## Recommended investigation summary format

When reporting findings, include:

- current tick
- building id and name
- pending plan id
- plan applies-at tick
- each pending unit with grid position and applies-at tick
- building account currency and balance
- city currency
- whether the upgrade is blocked by insufficient funds or a currency mismatch