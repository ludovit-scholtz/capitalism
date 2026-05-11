using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using HotChocolate;
using HotChocolate.Language;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Security;

/// <summary>
/// Deny-by-default enforcement gate for API-key-authenticated GraphQL operations.
/// </summary>
public sealed class ApiKeyScopeMiddleware(RequestDelegate next)
{
    private readonly ILogger<ApiKeyScopeMiddleware> _logger = NullLogger<ApiKeyScopeMiddleware>.Instance;

    private sealed record MutationRule(
        string? RequiredPrimaryScope,
        bool RequiresCompanyBinding,
        Func<JsonElement, AppDbContext, Guid, CancellationToken, ValueTask<IReadOnlyCollection<Guid>>>? CompanyResolver = null);

    private sealed record RequestDescriptor(
        string OperationName,
        string OperationType,
        string ScopeUsed);

    private sealed record ScopeDecision(
        bool IsAllowed,
        string OperationName,
        string OperationType,
        string ScopeUsed,
        string? DenialCode,
        string? DenialReason = null,
        string? AttemptedObjectId = null);

    private static readonly IReadOnlyDictionary<string, MutationRule> MutationRules =
        new Dictionary<string, MutationRule>(StringComparer.Ordinal)
        {
            // Trading-only operations.
            ["executeForexSwap"] = new(ApiKeyScopes.TradingOnly, true, ResolveForexCompanyIdsAsync),
            ["buyShares"] = new(ApiKeyScopes.TradingOnly, true, ResolveTradingAccountCompanyIdsAsync),
            ["sellShares"] = new(ApiKeyScopes.TradingOnly, true, ResolveTradingAccountCompanyIdsAsync),
            ["placeLimitOrder"] = new(ApiKeyScopes.TradingOnly, true, ResolveActiveTradingCompanyIdsAsync),
            ["cancelLimitOrder"] = new(ApiKeyScopes.TradingOnly, true, ResolveLimitOrderOwnerCompanyIdsAsync),

            // Bot-only automation / company management operations.
            ["createCompany"] = new(ApiKeyScopes.BotOnly, false),
            ["updateCompanySettings"] = new(ApiKeyScopes.BotOnly, true, ResolveDirectCompanyIdsAsync),
            ["switchAccountContext"] = new(ApiKeyScopes.BotOnly, true, ResolveSwitchContextCompanyIdsAsync),
            ["storeBuildingConfiguration"] = new(ApiKeyScopes.BotOnly, true, ResolveBuildingCompanyIdsAsync),
            ["cancelBuildingConfiguration"] = new(ApiKeyScopes.BotOnly, true, ResolveBuildingCompanyIdsAsync),
            ["buyFromExchange"] = new(ApiKeyScopes.BotOnly, true, ResolveExchangeBuyCompanyIdsAsync),
            ["sellToExchange"] = new(ApiKeyScopes.BotOnly, true, ResolveExchangeSellCompanyIdsAsync),
            ["acceptLoan"] = new(ApiKeyScopes.BotOnly, true, ResolveAcceptLoanCompanyIdsAsync),
            ["repayLoanDebt"] = new(ApiKeyScopes.BotOnly, true, ResolveLoanBorrowerCompanyIdsAsync),
            ["setBuildingForSale"] = new(ApiKeyScopes.BotOnly, true, ResolveBuildingCompanyIdsAsync),
            ["makeOfferOnBuilding"] = new(ApiKeyScopes.BotOnly, true, ResolveMakeOfferBuyerCompanyIdsAsync),
            ["acceptBuildingOffer"] = new(ApiKeyScopes.BotOnly, true, ResolveBuildingOfferSellerCompanyIdsAsync),
            ["cancelBuildingOffer"] = new(ApiKeyScopes.BotOnly, true, ResolveBuildingOfferSellerCompanyIdsAsync),
            ["destroyBuilding"] = new(ApiKeyScopes.BotOnly, true, ResolveBuildingCompanyIdsAsync),
            ["setRentPerSqm"] = new(ApiKeyScopes.BotOnly, true, ResolveBuildingCompanyIdsAsync),
            ["fundBuildingBankAccount"] = new(ApiKeyScopes.BotOnly, true, ResolveBuildingCompanyIdsAsync),
            ["assignBuildingBankAccount"] = new(ApiKeyScopes.BotOnly, true, ResolveBuildingCompanyIdsAsync),
            ["createCompanyBankAccount"] = new(ApiKeyScopes.BotOnly, true, ResolveDirectCompanyIdsAsync),
            ["closeCompanyBankAccount"] = new(ApiKeyScopes.BotOnly, true, ResolveBankAccountCompanyIdsAsync),
            ["transferFunds"] = new(ApiKeyScopes.BotOnly, true, ResolveTransferFundsCompanyIdsAsync),
            ["openBankAccount"] = new(ApiKeyScopes.BotOnly, false),
            ["closeBankAccount"] = new(ApiKeyScopes.BotOnly, false),
            ["unlockCity"] = new(ApiKeyScopes.BotOnly, false),
            ["createPersonalBankAccount"] = new(ApiKeyScopes.BotOnly, false),

            // Explicit deny-by-default gates for privileged or dangerous operations.
            ["placeBuilding"] = new(null, true),
            ["purchaseLot"] = new(null, true),
            ["setPlayerInvisibleInChat"] = new(null, false),
            ["setLocalGameAdminRole"] = new(null, false),
            ["assignGlobalGameAdminRole"] = new(null, false),
            ["removeGlobalGameAdminRole"] = new(null, false),
            ["updateRealWorldBillionaire"] = new(null, false),
            ["upsertGameNewsEntry"] = new(null, false),
            ["markGameNewsRead"] = new(null, false),
            ["markAllGameNewsRead"] = new(null, false),
            ["generateApiKey"] = new(null, false),
            ["revokeApiKey"] = new(null, false),
            ["forceRevokeApiKey"] = new(null, false),
            ["revokeAllPlayerApiKeys"] = new(null, false),
        };

    public ApiKeyScopeMiddleware(RequestDelegate next, ILogger<ApiKeyScopeMiddleware> logger) : this(next)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db, BotOwnershipGuard ownershipGuard)
    {
        if (!ShouldInspect(context)
            || context.Items[ApiKeyRequestContext.HttpContextItemKey] is not ApiKeyRequestContext apiKeyContext)
        {
            await next(context);
            return;
        }

        var normalizedScopes = apiKeyContext.Scopes
            .Select(ApiKeyScopes.Normalize)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        context.Request.EnableBuffering();
        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync(context.RequestAborted);
        }
        context.Request.Body.Position = 0;

        var scopeDecisions = await EvaluateRequestAsync(
            requestBody,
            normalizedScopes,
            apiKeyContext,
            db,
            ownershipGuard,
            context.RequestAborted);

        if (scopeDecisions.Count == 0)
        {
            await next(context);
            return;
        }

        var deniedDecision = scopeDecisions.FirstOrDefault(decision => !decision.IsAllowed);
        if (deniedDecision is not null)
        {
            await PersistAuditLogsAsync(context, db, apiKeyContext, [deniedDecision], context.RequestAborted);
            await WriteForbiddenAsync(context, deniedDecision.DenialCode ?? "API_KEY_SCOPE_FORBIDDEN");
            return;
        }

        await next(context);
        await PersistAuditLogsAsync(context, db, apiKeyContext, scopeDecisions, context.RequestAborted);
    }

    private static bool ShouldInspect(HttpContext context)
        => HttpMethods.IsPost(context.Request.Method)
            && context.Request.Path.Equals("/graphql", StringComparison.OrdinalIgnoreCase);

    private async Task<List<ScopeDecision>> EvaluateRequestAsync(
        string requestBody,
        IReadOnlyCollection<string> normalizedScopes,
        ApiKeyRequestContext apiKeyContext,
        AppDbContext db,
        BotOwnershipGuard ownershipGuard,
        CancellationToken cancellationToken)
    {
        var results = new List<ScopeDecision>();
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return results;
        }

        try
        {
            using var document = JsonDocument.Parse(requestBody);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    var decision = await EvaluateSingleRequestAsync(entry, normalizedScopes, apiKeyContext, db, ownershipGuard, cancellationToken);
                    if (decision is not null)
                    {
                        results.AddRange(decision);
                    }
                }

                return results;
            }

            var singleResult = await EvaluateSingleRequestAsync(document.RootElement, normalizedScopes, apiKeyContext, db, ownershipGuard, cancellationToken);
            if (singleResult is not null)
            {
                results.AddRange(singleResult);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to parse GraphQL request payload while checking API key scopes.");
        }

        return results;
    }

    private async Task<List<ScopeDecision>?> EvaluateSingleRequestAsync(
        JsonElement requestRoot,
        IReadOnlyCollection<string> normalizedScopes,
        ApiKeyRequestContext apiKeyContext,
        AppDbContext db,
        BotOwnershipGuard ownershipGuard,
        CancellationToken cancellationToken)
    {
        if (requestRoot.ValueKind != JsonValueKind.Object
            || !requestRoot.TryGetProperty("query", out var queryElement)
            || queryElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var query = queryElement.GetString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        if (normalizedScopes.Count == 0)
        {
            var deniedDescriptor = await DescribeOperationAsync(requestRoot, query, cancellationToken);
            return
            [
                new ScopeDecision(
                    false,
                    deniedDescriptor.OperationName,
                    deniedDescriptor.OperationType,
                    "none",
                    "API_KEY_SCOPE_FORBIDDEN")
            ];
        }

        var variables = requestRoot.TryGetProperty("variables", out var variablesElement) && variablesElement.ValueKind == JsonValueKind.Object
            ? variablesElement
            : default;

        var graphqlDocument = Utf8GraphQLParser.Parse(query);
        var operation = ResolveOperation(graphqlDocument, requestRoot);
        if (operation is null)
        {
            return null;
        }

        var rootFields = operation.SelectionSet.Selections
            .OfType<FieldNode>()
            .Select(field => field.Name.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (operation.Operation == OperationType.Query)
        {
            return
            [
                new ScopeDecision(
                    true,
                    string.Join(", ", rootFields),
                    "query",
                    normalizedScopes.Contains(ApiKeyScopes.ReadOnly, StringComparer.Ordinal)
                        ? ApiKeyScopes.ReadOnly
                        : normalizedScopes.First(),
                    null)
            ];
        }

        var decisions = new List<ScopeDecision>();
        foreach (var rootField in rootFields)
        {
            if (!MutationRules.TryGetValue(rootField, out var rule)
                || string.IsNullOrWhiteSpace(rule.RequiredPrimaryScope)
                || !normalizedScopes.Contains(rule.RequiredPrimaryScope, StringComparer.Ordinal))
            {
                decisions.Add(new ScopeDecision(
                    false,
                    rootField,
                    "mutation",
                    rule?.RequiredPrimaryScope ?? "none",
                    "API_KEY_SCOPE_FORBIDDEN"));
                continue;
            }

            var scopeUsed = rule.RequiredPrimaryScope;
            if (rule.RequiresCompanyBinding
                && normalizedScopes.Contains(ApiKeyScopes.CompanyBound, StringComparer.Ordinal))
            {
                var companyIds = rule.CompanyResolver is null
                    ? []
                    : await rule.CompanyResolver(variables, db, apiKeyContext.PlayerId, cancellationToken);

                if (companyIds.Count == 0
                    || companyIds.Any(companyId => !apiKeyContext.CompanyIds.Contains(companyId)))
                {
                    decisions.Add(new ScopeDecision(
                        false,
                        rootField,
                        "mutation",
                        ApiKeyScopes.CompanyBound,
                        "API_KEY_SCOPE_FORBIDDEN"));
                    continue;
                }

                scopeUsed = $"{rule.RequiredPrimaryScope},{ApiKeyScopes.CompanyBound}";
            }

            try
            {
                await ownershipGuard.EnsureMutationOwnershipAsync(rootField, variables, apiKeyContext.PlayerId, cancellationToken);
            }
            catch (GraphQLException ex) when (TryGetErrorCode(ex) == BotOwnershipGuard.NotOwnedOrNotFoundCode)
            {
                var denialReason = TryGetErrorExtension(ex, "authorizationReason");
                var attemptedObjectId = TryGetErrorExtension(ex, "attemptedObjectId");
                decisions.Add(new ScopeDecision(
                    false,
                    rootField,
                    "mutation",
                    scopeUsed,
                    BotOwnershipGuard.NotOwnedOrNotFoundCode,
                    denialReason,
                    attemptedObjectId));
                continue;
            }

            decisions.Add(new ScopeDecision(true, rootField, "mutation", scopeUsed, null));
        }

        return decisions;
    }

    private static OperationDefinitionNode? ResolveOperation(DocumentNode document, JsonElement requestRoot)
    {
        var requestedOperationName = requestRoot.TryGetProperty("operationName", out var operationNameElement)
            && operationNameElement.ValueKind == JsonValueKind.String
            ? operationNameElement.GetString()
            : null;

        var operations = document.Definitions.OfType<OperationDefinitionNode>().ToList();
        if (!string.IsNullOrWhiteSpace(requestedOperationName))
        {
            return operations.FirstOrDefault(operation =>
                string.Equals(operation.Name?.Value, requestedOperationName, StringComparison.Ordinal));
        }

        return operations.FirstOrDefault();
    }

    private async Task<RequestDescriptor> DescribeOperationAsync(
        JsonElement requestRoot,
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = Utf8GraphQLParser.Parse(query);
            var operation = ResolveOperation(document, requestRoot);
            if (operation is null)
            {
                return new RequestDescriptor("unknown", "unknown", "none");
            }

            var rootFields = operation.SelectionSet.Selections
                .OfType<FieldNode>()
                .Select(field => field.Name.Value)
                .Distinct(StringComparer.Ordinal);
            return new RequestDescriptor(
                string.Join(", ", rootFields),
                operation.Operation == OperationType.Query ? "query" : "mutation",
                "none");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to describe GraphQL request while checking API key scopes.");
            return new RequestDescriptor("unknown", "unknown", "none");
        }
    }

    private static async Task PersistAuditLogsAsync(
        HttpContext context,
        AppDbContext db,
        ApiKeyRequestContext apiKeyContext,
        IReadOnlyCollection<ScopeDecision> decisions,
        CancellationToken cancellationToken)
    {
        if (decisions.Count == 0)
        {
            return;
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        foreach (var decision in decisions)
        {
            db.PlayerApiKeyAuditLogs.Add(new Data.Entities.PlayerApiKeyAuditLog
            {
                Id = Guid.NewGuid(),
                PlayerApiKeyId = apiKeyContext.KeyId,
                PlayerId = apiKeyContext.PlayerId,
                OperationName = decision.OperationName,
                OperationType = decision.OperationType,
                ScopeUsed = decision.ScopeUsed,
                WasAllowed = decision.IsAllowed,
                DenialCode = decision.DenialCode,
                DenialReason = decision.DenialReason,
                AttemptedObjectId = decision.AttemptedObjectId,
                IpAddress = ipAddress,
                SessionContext = context.TraceIdentifier,
                OccurredAtUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task WriteForbiddenAsync(HttpContext context, string code)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new
            {
                data = (object?)null,
                errors = new[]
                {
                    new
                    {
                        message = "Forbidden.",
                        extensions = new { code }
                    }
                }
            },
            cancellationToken: context.RequestAborted);
    }

    private static string? TryGetErrorCode(GraphQLException ex)
        => ex.Errors.FirstOrDefault()?.Code;

    private static string? TryGetErrorExtension(GraphQLException ex, string key)
    {
        var error = ex.Errors.FirstOrDefault();
        if (error?.Extensions is null)
        {
            return null;
        }

        if (!error.Extensions.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static ValueTask<IReadOnlyCollection<Guid>> ResolveDirectCompanyIdsAsync(
        JsonElement variables,
        AppDbContext _,
        Guid __,
        CancellationToken ___)
    {
        var ids = new List<Guid>();
        AddGuidIfPresent(ids, variables, "input", "companyId");
        AddGuidIfPresent(ids, variables, "input", "borrowerCompanyId");
        AddGuidIfPresent(ids, variables, "input", "tradeAccountCompanyId");
        return ValueTask.FromResult<IReadOnlyCollection<Guid>>(ids);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveBuildingCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid _,
        CancellationToken cancellationToken)
    {
        var buildingIds = new List<Guid>();
        AddGuidIfPresent(buildingIds, variables, "input", "buildingId");
        AddGuidIfPresent(buildingIds, variables, "input", "collateralBuildingId");
        if (buildingIds.Count == 0)
        {
            return [];
        }

        return await db.Buildings
            .AsNoTracking()
            .Where(building => buildingIds.Contains(building.Id))
            .Select(building => building.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static ValueTask<IReadOnlyCollection<Guid>> ResolveMakeOfferBuyerCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
        => ResolveDirectCompanyIdsAsync(variables, db, playerId, cancellationToken);

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveBankAccountCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid _,
        CancellationToken cancellationToken)
    {
        var accountIds = new List<Guid>();
        AddGuidIfPresent(accountIds, variables, "input", "bankAccountId");
        AddGuidIfPresent(accountIds, variables, "input", "fromBankAccountId");
        AddGuidIfPresent(accountIds, variables, "input", "toBankAccountId");
        AddGuidIfPresent(accountIds, variables, "bankAccountId");

        if (accountIds.Count == 0)
        {
            return [];
        }

        return await db.BankAccounts
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id) && account.CompanyId.HasValue)
            .Select(account => account.CompanyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveExchangeBuyCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var companyIds = new HashSet<Guid>();
        foreach (var bankCompanyId in await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            companyIds.Add(bankCompanyId);
        }

        var unitIds = new List<Guid>();
        AddGuidIfPresent(unitIds, variables, "input", "targetBuildingUnitId");
        if (unitIds.Count > 0)
        {
            var unitCompanyIds = await db.BuildingUnits
                .AsNoTracking()
                .Where(unit => unitIds.Contains(unit.Id))
                .Select(unit => unit.Building.CompanyId)
                .Distinct()
                .ToListAsync(cancellationToken);
            companyIds.UnionWith(unitCompanyIds);
        }

        return companyIds.ToList();
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveExchangeSellCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var companyIds = new HashSet<Guid>();
        foreach (var bankCompanyId in await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            companyIds.Add(bankCompanyId);
        }

        var unitIds = new List<Guid>();
        AddGuidIfPresent(unitIds, variables, "input", "sourceBuildingUnitId");
        if (unitIds.Count > 0)
        {
            var unitCompanyIds = await db.BuildingUnits
                .AsNoTracking()
                .Where(unit => unitIds.Contains(unit.Id))
                .Select(unit => unit.Building.CompanyId)
                .Distinct()
                .ToListAsync(cancellationToken);
            companyIds.UnionWith(unitCompanyIds);
        }

        return companyIds.ToList();
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveAcceptLoanCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        foreach (var companyId in await ResolveDirectCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(companyId);
        }

        foreach (var buildingCompanyId in await ResolveBuildingCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(buildingCompanyId);
        }

        foreach (var bankCompanyId in await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(bankCompanyId);
        }

        return ids.ToList();
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveLoanBorrowerCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid _,
        CancellationToken cancellationToken)
    {
        var loanIds = new List<Guid>();
        AddGuidIfPresent(loanIds, variables, "input", "loanId");
        if (loanIds.Count == 0)
        {
            return [];
        }

        return await db.Loans
            .AsNoTracking()
            .Where(loan => loanIds.Contains(loan.Id))
            .Select(loan => loan.BorrowerCompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveBuildingOfferSellerCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid _,
        CancellationToken cancellationToken)
    {
        var offerIds = new List<Guid>();
        AddGuidIfPresent(offerIds, variables, "input", "offerId");
        if (offerIds.Count == 0)
        {
            return [];
        }

        return await db.BuildingSaleOffers
            .AsNoTracking()
            .Where(offer => offerIds.Contains(offer.Id))
            .Select(offer => offer.Building.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveTransferFundsCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        foreach (var bankCompanyId in await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(bankCompanyId);
        }

        return ids.ToList();
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveForexCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        return await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveTradingAccountCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        foreach (var companyId in await ResolveDirectCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(companyId);
        }

        foreach (var companyId in await ResolveBankAccountCompanyIdsAsync(variables, db, playerId, cancellationToken))
        {
            ids.Add(companyId);
        }

        if (ids.Count > 0)
        {
            return ids.ToList();
        }

        return await ResolveActiveTradingCompanyIdsAsync(variables, db, playerId, cancellationToken);
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveActiveTradingCompanyIdsAsync(
        JsonElement _,
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var activeCompanyId = await db.Players
            .AsNoTracking()
            .Where(player => player.Id == playerId && player.ActiveAccountType == AccountContextType.Company)
            .Select(player => player.ActiveCompanyId)
            .FirstOrDefaultAsync(cancellationToken);

        return activeCompanyId.HasValue ? [activeCompanyId.Value] : [];
    }

    private static async ValueTask<IReadOnlyCollection<Guid>> ResolveLimitOrderOwnerCompanyIdsAsync(
        JsonElement variables,
        AppDbContext db,
        Guid _,
        CancellationToken cancellationToken)
    {
        var orderIds = new List<Guid>();
        AddGuidIfPresent(orderIds, variables, "orderId");
        if (orderIds.Count == 0)
        {
            return [];
        }

        return await db.LimitOrders
            .AsNoTracking()
            .Where(order => orderIds.Contains(order.Id) && order.OwnerCompanyId.HasValue)
            .Select(order => order.OwnerCompanyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static ValueTask<IReadOnlyCollection<Guid>> ResolveSwitchContextCompanyIdsAsync(
        JsonElement variables,
        AppDbContext _,
        Guid __,
        CancellationToken ___)
    {
        var accountType = GetStringPath(variables, "input", "accountType");
        if (!string.Equals(accountType, AccountContextType.Company, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<IReadOnlyCollection<Guid>>([]);
        }

        var ids = new List<Guid>();
        AddGuidIfPresent(ids, variables, "input", "companyId");
        return ValueTask.FromResult<IReadOnlyCollection<Guid>>(ids);
    }

    private static void AddGuidIfPresent(List<Guid> target, JsonElement root, params string[] path)
    {
        var guid = GetGuidPath(root, path);
        if (guid.HasValue)
        {
            target.Add(guid.Value);
        }
    }

    private static Guid? GetGuidPath(JsonElement root, params string[] path)
    {
        var element = root;
        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var value)
            ? value
            : null;
    }

    private static string? GetStringPath(JsonElement root, params string[] path)
    {
        var element = root;
        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
