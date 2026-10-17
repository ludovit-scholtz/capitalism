using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Bank lending mutations: accepting collateral-based loans from any bank (including government banks).
/// Players select a bank, pledge one of their buildings as collateral, and borrow up to 70% of its
/// appraised value — provided the bank has sufficient deposit capacity.
/// </summary>
public sealed partial class Mutation
{
    // ── Bank Lending ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Requests a collateral-backed loan from a bank building.
    /// The borrower must own the company; a collateral building is required; the bank must be open
    /// (BaseCapitalDeposited = true) and have sufficient deposit capacity (90% of TotalDeposits).
    /// Self-lending is blocked at both company and player level.
    /// API-key callers can reference only borrower companies, collateral buildings, and settlement
    /// accounts owned by the authenticated principal.
    /// </summary>
    [Authorize]
    public async Task<Loan> AcceptLoan(
        AcceptLoanInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        // Verify borrower owns the company.
        var borrower = await db.Companies
            .FirstOrDefaultAsync(c => c.Id == input.BorrowerCompanyId);

        if (borrower is null || borrower.PlayerId != userId)
        {
            throw objectAuthorization.Deny(
                userId,
                "company",
                input.BorrowerCompanyId,
                borrower is null ? ObjectAuthorizationService.NotFoundReason : ObjectAuthorizationService.NotOwnedReason);
        }

        return await AcceptLoanFromBankDirectAsync(input, borrower, db, userId, httpContextAccessor, objectAuthorization);
    }

    /// <param name="userId">Pre-extracted from the JWT claim to avoid double-extraction; also used for player-level self-lending check (bank.Company.PlayerId).</param>
    /// <param name="httpContextAccessor">Still required to pass RequestAborted to async helpers.</param>
    private async Task<Loan> AcceptLoanFromBankDirectAsync(
        AcceptLoanInput input,
        Company borrower,
        AppDbContext db,
        Guid userId,
        IHttpContextAccessor httpContextAccessor,
        ObjectAuthorizationService objectAuthorization)
    {
        var bank = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == input.LoanOfferId && b.Type == BuildingType.Bank);

        if (bank is null || !bank.BaseCapitalDeposited)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank not found or is not open for lending.")
                    .SetCode("BANK_NOT_FOUND")
                    .Build());
        }

        // Self-lending guard: borrower cannot borrow from their own bank.
        if (bank.CompanyId == borrower.Id)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A company cannot borrow from its own bank.")
                    .SetCode("SELF_LENDING_NOT_ALLOWED")
                    .Build());
        }

        if (bank.Company?.PlayerId == userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You cannot borrow from a bank you own.")
                    .SetCode("SELF_LENDING_NOT_ALLOWED")
                    .Build());
        }

        if (input.PrincipalAmount < 1_000m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Minimum loan amount is $1,000.")
                    .SetCode("INVALID_PRINCIPAL")
                    .Build());
        }

        if (!input.CollateralBuildingId.HasValue)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A collateral building is required for bank loan requests.")
                    .SetCode("COLLATERAL_REQUIRED")
                    .Build());
        }

        if (input.DurationTicks.HasValue && (input.DurationTicks.Value < 1 || input.DurationTicks.Value > 87_600))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Loan duration must be between 1 tick (1 in-game hour) and 87,600 ticks (10 in-game years).")
                    .SetCode("INVALID_DURATION")
                    .Build());
        }

        var outstandingPrincipal = await db.Loans
            .Where(l => l.BankBuildingId == bank.Id
                && l.RemainingPrincipal > 0m
                && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Defaulted))
            .SumAsync(l => (decimal?)l.RemainingPrincipal) ?? 0m;
        var availableLendingCapacity = Math.Max(0m, (bank.TotalDeposits * 0.90m) - outstandingPrincipal);

        if (input.PrincipalAmount > availableLendingCapacity)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"The bank only has {availableLendingCapacity:C0} of lending capacity available.")
                    .SetCode("INSUFFICIENT_CAPACITY")
                    .Build());
        }

        var bankCurrencyCode = bank.City?.CurrencyCode ?? "EUR";
        var lenderAccounts = await LoadActiveCompanyBankAccountsAsync(
            db,
            bank.CompanyId,
            httpContextAccessor.HttpContext!.RequestAborted);

        if (CompanyBankingService.GetCurrencyBalance(lenderAccounts, bankCurrencyCode) < input.PrincipalAmount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The lender does not have sufficient funds to cover this loan at this time.")
                    .SetCode("LENDER_INSUFFICIENT_FUNDS")
                    .Build());
        }

        var collateralBuilding = await objectAuthorization.RequireOwnedAsync(
            actorUserId: userId,
            requestedObjectType: "building",
            requestedObjectId: input.CollateralBuildingId.Value,
            loadEntityAsync: token => db.Buildings
                .Include(b => b.City)
                .FirstOrDefaultAsync(b => b.Id == input.CollateralBuildingId.Value, token),
            isOwnedByActor: building => building.CompanyId == borrower.Id,
            cancellationToken: httpContextAccessor.HttpContext!.RequestAborted);

        var alreadyPledged = await db.Loans
            .AnyAsync(l => l.CollateralBuildingId == input.CollateralBuildingId.Value
                && l.RemainingPrincipal > 0m
                && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Defaulted));
        if (alreadyPledged)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This building is already pledged as collateral for another active loan.")
                    .SetCode("COLLATERAL_ALREADY_PLEDGED")
                    .Build());
        }

        var ownershipAtCommit = await db.Buildings
            .AsNoTracking()
            .Where(b => b.Id == collateralBuilding.Id)
            .Select(b => b.CompanyId)
            .FirstOrDefaultAsync();
        if (ownershipAtCommit != borrower.Id)
        {
            LoanCollateralSecurityAuditLogger.Add(
                db,
                userId,
                action: "LOAN_ORIGINATION",
                reason: "COLLATERAL_OWNERSHIP_CONFLICT",
                buildingId: collateralBuilding.Id,
                detail: $"Expected owner {borrower.Id}, actual owner {ownershipAtCommit}.");
            await db.SaveChangesAsync();
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Collateral ownership changed before the loan could be committed.")
                    .SetCode("COLLATERAL_OWNERSHIP_CONFLICT")
                    .Build());
        }

        var collateralCityCurrencyCode = collateralBuilding.City?.CurrencyCode ?? "EUR";
        var collateralFxRates = await FxRateHelper.BuildEurRatesLookupAsync(
            db,
            [collateralCityCurrencyCode, bankCurrencyCode]);

        var baseEurAppraisedValue = WealthCalculator.GetBuildingValue(collateralBuilding);
        var collateralCityFxRate = FxRateHelper.GetEurRate(collateralFxRates, collateralCityCurrencyCode);
        var collateralValueInBuildingCurrency = decimal.Round(
            baseEurAppraisedValue * collateralCityFxRate,
            2,
            MidpointRounding.AwayFromZero);

        var collateralAppraisedValue = decimal.Round(
            FxRateHelper.ConvertAmount(
                collateralValueInBuildingCurrency,
                collateralCityCurrencyCode,
                bankCurrencyCode,
                collateralFxRates),
            2,
            MidpointRounding.AwayFromZero);
        var maxBorrowable = decimal.Round(collateralAppraisedValue * 0.70m, 2, MidpointRounding.AwayFromZero);
        if (input.PrincipalAmount > maxBorrowable)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"The requested principal of {input.PrincipalAmount:C0} exceeds the collateral lending capacity of {maxBorrowable:C0}.")
                    .SetCode("EXCEEDS_COLLATERAL_LIMIT")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultDeterministicAsync();
        var durationTicks = input.DurationTicks ?? GameConstants.TicksPerYear;
        var annualRate = await ResolveActiveAnnualRateAsync(db, currentTick, bank);

        var ticksPerPayment = 1L;
        var totalPayments = (int)Math.Max(1L, durationTicks);
        var paymentAmount = ComputeEstimatedTickPayment(input.PrincipalAmount, annualRate, totalPayments);

        var borrowerAccount = await ResolveLoanSettlementAccountAsync(
            db,
            borrower.Id,
            bankCurrencyCode,
            input.BankAccountId,
            httpContextAccessor.HttpContext.RequestAborted);

        // ── Atomic debit / credit ────────────────────────────────────────────────────────────────
        // lenderAccounts is the full account list for bank.CompanyId loaded earlier in this method
        // via LoadActiveCompanyBankAccountsAsync. TryDebit distributes the debit across those accounts
        // in order of preference and bumps each touched account's ConcurrencyToken.
        // If it returns false (balance insufficient at execution time) we abort immediately — the
        // borrower is never credited and no loan record is created, preventing money minting.
        // If a concurrent request already debited the same account and persisted first, SaveChangesAsync
        // below will detect the ConcurrencyToken mismatch and throw DbUpdateConcurrencyException,
        // which we catch and surface as CONCURRENT_MODIFICATION.
        var lenderDebitedAccount = CompanyBankingService.FindPreferredAccount(lenderAccounts, bankCurrencyCode);
        var debitSucceeded = CompanyBankingService.TryDebit(lenderAccounts, input.PrincipalAmount, bankCurrencyCode);
        if (!debitSucceeded)
        {
            LoanCollateralSecurityAuditLogger.Add(
                db,
                userId,
                action: "LOAN_ORIGINATION",
                reason: "INSUFFICIENT_BALANCE_AT_COMMIT",
                buildingId: collateralBuilding.Id,
                detail: "Lender balance dropped below requested principal before debit staging.");
            await db.SaveChangesAsync();
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The lender does not have sufficient funds to complete this loan disbursement at execution time.")
                    .SetCode("INSUFFICIENT_BALANCE_AT_COMMIT")
                    .Build());
        }

        // Credit borrower via TryCredit (mirrors how the lender was debited and bumps the token).
        // borrowerAccount was resolved and currency-validated by ResolveLoanSettlementAccountAsync.
        var creditSucceeded = CompanyBankingService.TryCredit(
            new[] { borrowerAccount },
            input.PrincipalAmount,
            borrowerAccount.CurrencyCode,
            out _);
        if (!creditSucceeded)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Failed to credit the borrower's settlement account.")
                    .SetCode("CREDIT_FAILED")
                    .Build());
        }

        var internalOffer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = bank.CompanyId,
            AnnualInterestRatePercent = annualRate,
            MaxPrincipalPerLoan = input.PrincipalAmount,
            TotalCapacity = input.PrincipalAmount,
            UsedCapacity = input.PrincipalAmount,
            DurationTicks = durationTicks,
            IsActive = false,
            CreatedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            ConcurrencyToken = Guid.NewGuid(),
            LoanOfferId = internalOffer.Id,
            BorrowerCompanyId = borrower.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = bank.CompanyId,
            OriginalPrincipal = input.PrincipalAmount,
            RemainingPrincipal = input.PrincipalAmount,
            AnnualInterestRatePercent = annualRate,
            DurationTicks = durationTicks,
            StartTick = currentTick,
            DueTick = currentTick + durationTicks,
            NextPaymentTick = currentTick + ticksPerPayment,
            PaymentAmount = paymentAmount,
            PaymentsMade = 0,
            TotalPayments = totalPayments,
            BorrowerBankAccountId = borrowerAccount.Id,
            Status = LoanStatus.Active,
            MissedPayments = 0,
            AccumulatedPenalty = 0m,
            AcceptedAtUtc = DateTime.UtcNow,
            CollateralBuildingId = collateralBuilding.Id,
            CollateralAppraisedValue = collateralAppraisedValue,
        };

        db.LoanOffers.Add(internalOffer);
        db.Loans.Add(loan);
        collateralBuilding.ConcurrencyToken = Guid.NewGuid();

        // Borrower ledger — cash inflow
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = borrower.Id,
            BankAccountId = borrowerAccount.Id,
            Category = LedgerCategory.LoanOrigination,
            Description = $"Loan received from {bank.Company!.Name} via {bank.Name} – {annualRate}% p.a. over {durationTicks} in-game hours (secured against {collateralBuilding.Name})",
            Amount = input.PrincipalAmount,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        // Lender ledger — cash outflow (negative = funds disbursed from the bank)
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = bank.CompanyId,
            BankAccountId = lenderDebitedAccount?.Id,
            Category = LedgerCategory.LoanOrigination,
            Description = $"Loan disbursed to {borrower.Name} via {bank.Name} – {annualRate}% p.a. over {durationTicks} in-game hours",
            Amount = -input.PrincipalAmount,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var lenderBalancesAtCommit = await db.BankAccounts
                .AsNoTracking()
                .Where(account => account.CompanyId == bank.CompanyId && account.ClosedAtUtc == null)
                .SumAsync(account => account.Balance);
            LoanCollateralSecurityAuditLogger.Add(
                db,
                userId,
                action: "LOAN_ORIGINATION",
                reason: "INSUFFICIENT_BALANCE_AT_COMMIT",
                loanId: loan.Id,
                buildingId: collateralBuilding.Id,
                detail: $"Commit-time lender balance {lenderBalancesAtCommit} {bankCurrencyCode}, required {input.PrincipalAmount} {bankCurrencyCode}.");
            await db.SaveChangesAsync();
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Insufficient payable balance at commit time.")
                    .SetCode("INSUFFICIENT_BALANCE_AT_COMMIT")
                    .Build());
        }

        return loan;
    }

    private static async Task<decimal> ResolveActiveAnnualRateAsync(AppDbContext db, long currentTick, Building bank)
    {
        var baseRate = bank.LendingInterestRatePercent ?? 8m;
        var activeInterestEvents = await db.MarketEvents
            .AsNoTracking()
            .Where(me => me.EventType == MarketEventType.InterestRateChange
                && me.StartsAtTick <= currentTick
                && me.ExpiresAtTick >= currentTick
                && (me.AffectedCityId == null || me.AffectedCityId == bank.CityId))
            .ToListAsync();

        if (activeInterestEvents.Count == 0)
            return baseRate;

        var multiplier = Math.Clamp(
            activeInterestEvents.Aggregate(1m, (acc, next) => acc * next.MagnitudeMultiplier),
            GameConstants.MarketEventMultiplierMin,
            GameConstants.MarketEventMultiplierMax);
        return decimal.Round(baseRate * multiplier, 4, MidpointRounding.AwayFromZero);
    }

}
