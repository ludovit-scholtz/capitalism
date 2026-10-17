using HotChocolate;
using Microsoft.Extensions.Logging;

namespace Api.Security;

public sealed class ObjectAuthorizationService(
    ILogger<ObjectAuthorizationService> logger,
    IHttpContextAccessor httpContextAccessor,
    ObjectDenialRecorder denialRecorder)
{
    private readonly ILogger<ObjectAuthorizationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

    public const string NotFoundOrNotOwnedCode = "FORBIDDEN";
    public const string NotFoundReason = "not_found";
    public const string NotOwnedReason = "not_owned";

    /// <summary>The caller owns the object but not in the account context the request runs in (personal vs company). Benign: audited at Information level.</summary>
    public const string WrongContextReason = "wrong_account_context";

    /// <summary>The object exists and is visible but is in a state that forbids the action (for example a delisted building). Benign: audited at Information level, not as a probe.</summary>
    public const string NotAvailableReason = "not_available";

    public const string FriendlyMessage = "You don't have permission to perform this action.";

    public async Task<TEntity> RequireOwnedAsync<TEntity>(
        Guid actorUserId,
        string requestedObjectType,
        Guid requestedObjectId,
        Func<CancellationToken, Task<TEntity?>> loadEntityAsync,
        Func<TEntity, bool> isOwnedByActor,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var entity = await loadEntityAsync(cancellationToken);
        if (entity is null)
        {
            throw Deny(
                actorUserId,
                requestedObjectType,
                requestedObjectId,
                NotFoundReason);
        }

        if (!isOwnedByActor(entity))
        {
            throw Deny(
                actorUserId,
                requestedObjectType,
                requestedObjectId,
                NotOwnedReason);
        }

        return entity;
    }

    /// <summary>
    /// Logs a structured security audit event with the real failure reason and returns the
    /// uniform client-facing error. Use at resolver sites that load an object and then check
    /// ownership, so "missing" and "not yours" are indistinguishable to the caller.
    /// </summary>
    public GraphQLException Deny(
        Guid actorUserId,
        string requestedObjectType,
        Guid requestedObjectId,
        string actualFailureReason)
    {
        LogDenied(actorUserId, requestedObjectType, requestedObjectId, actualFailureReason);
        return BuildDeniedException();
    }

    /// <summary>
    /// Writes the structured security audit event only, for call sites that report the denial
    /// through a payload instead of throwing.
    /// </summary>
    public void LogDenied(
        Guid actorUserId,
        string requestedObjectType,
        Guid requestedObjectId,
        string actualFailureReason)
    {
        var apiKeyContext = _httpContextAccessor.HttpContext?.Items[ApiKeyRequestContext.HttpContextItemKey] as ApiKeyRequestContext;
        // Fail safe: every reason is a Warning (probe signal) unless it is explicitly known to be benign.
        var level = ObjectDenialRecorder.IsProbeSignal(actualFailureReason) ? LogLevel.Warning : LogLevel.Information;
        _logger.Log(
            level,
            "Object authorization denied {@SecurityAuditEvent}",
            new
            {
                timestamp = DateTime.UtcNow,
                actorUserId,
                apiKeyId = apiKeyContext?.KeyId,
                requestedObjectType,
                requestedObjectId,
                actualFailureReason,
            });
        denialRecorder.Record(new ObjectDenialEvent(
            DateTime.UtcNow,
            actorUserId,
            apiKeyContext?.KeyId,
            requestedObjectType,
            requestedObjectId,
            actualFailureReason,
            ObjectDenialRecorder.IsProbeSignal(actualFailureReason)));
    }

    private static GraphQLException BuildDeniedException() =>
        new(
            ErrorBuilder.New()
                .SetMessage(FriendlyMessage)
                .SetCode(NotFoundOrNotOwnedCode)
                .Build());
}
