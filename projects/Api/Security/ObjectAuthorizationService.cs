using HotChocolate;
using Microsoft.Extensions.Logging;

namespace Api.Security;

public sealed class ObjectAuthorizationService(
    ILogger<ObjectAuthorizationService> logger,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly ILogger<ObjectAuthorizationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

    public const string NotFoundOrNotOwnedCode = "NOT_FOUND_OR_NOT_OWNED";
    public const string NotFoundReason = "not_found";
    public const string NotOwnedReason = "not_owned";
    public const string FriendlyMessage = "This item could not be found or you don't have permission to access it.";

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
            throw BuildAndLogDeniedException(
                actorUserId,
                requestedObjectType,
                requestedObjectId,
                NotFoundReason);
        }

        if (!isOwnedByActor(entity))
        {
            throw BuildAndLogDeniedException(
                actorUserId,
                requestedObjectType,
                requestedObjectId,
                NotOwnedReason);
        }

        return entity;
    }

    private GraphQLException BuildAndLogDeniedException(
        Guid actorUserId,
        string requestedObjectType,
        Guid requestedObjectId,
        string actualFailureReason)
    {
        var apiKeyContext = _httpContextAccessor.HttpContext?.Items[ApiKeyRequestContext.HttpContextItemKey] as ApiKeyRequestContext;
        _logger.LogWarning(
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

        return new GraphQLException(
            ErrorBuilder.New()
                .SetMessage(FriendlyMessage)
                .SetCode(NotFoundOrNotOwnedCode)
                .Build());
    }
}
