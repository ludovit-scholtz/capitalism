using Api.Security;
using HotChocolate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Api.Tests;

public sealed class ObjectAuthorizationServiceTests
{
    [Fact]
    public async Task RequireOwnedAsync_NotFound_ThrowsUnifiedCode_AndLogsNotFoundReason()
    {
        var logger = new CapturingLogger<ObjectAuthorizationService>();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var service = new ObjectAuthorizationService(logger, accessor);
        var objectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            service.RequireOwnedAsync(
                actorId,
                "limit_order",
                objectId,
                _ => Task.FromResult<TestOwnedEntity?>(null),
                _ => true));

        Assert.Equal(ObjectAuthorizationService.NotFoundOrNotOwnedCode, ex.Errors.Single().Code);
        Assert.Equal(ObjectAuthorizationService.FriendlyMessage, ex.Errors.Single().Message);

        var securityEvent = logger.SingleSecurityEvent();
        Assert.Equal(actorId, GetProperty<Guid>(securityEvent, "actorUserId"));
        Assert.Equal(objectId, GetProperty<Guid>(securityEvent, "requestedObjectId"));
        Assert.Equal("limit_order", GetProperty<string>(securityEvent, "requestedObjectType"));
        Assert.Equal(ObjectAuthorizationService.NotFoundReason, GetProperty<string>(securityEvent, "actualFailureReason"));
        Assert.Null(GetNullableGuidProperty(securityEvent, "apiKeyId"));
    }

    [Fact]
    public async Task RequireOwnedAsync_NotOwned_ThrowsUnifiedCode_AndLogsNotOwnedReasonWithApiKeyContext()
    {
        var logger = new CapturingLogger<ObjectAuthorizationService>();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var apiKeyId = Guid.NewGuid();
        accessor.HttpContext.Items[ApiKeyRequestContext.HttpContextItemKey] = new ApiKeyRequestContext(apiKeyId, Guid.NewGuid(), [], []);

        var service = new ObjectAuthorizationService(logger, accessor);
        var objectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            service.RequireOwnedAsync(
                actorId,
                "bank_account",
                objectId,
                _ => Task.FromResult<TestOwnedEntity?>(new TestOwnedEntity(Guid.NewGuid())),
                entity => entity.OwnerId == actorId));

        Assert.Equal(ObjectAuthorizationService.NotFoundOrNotOwnedCode, ex.Errors.Single().Code);

        var securityEvent = logger.SingleSecurityEvent();
        Assert.Equal(ObjectAuthorizationService.NotOwnedReason, GetProperty<string>(securityEvent, "actualFailureReason"));
        Assert.Equal(apiKeyId, GetNullableGuidProperty(securityEvent, "apiKeyId"));
    }

    private static T GetProperty<T>(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName)
            ?? throw new ArgumentException($"Property '{propertyName}' not found on {source.GetType().Name}.", nameof(propertyName));
        var value = property.GetValue(source)
            ?? throw new ArgumentException($"Property '{propertyName}' value is null.", nameof(propertyName));
        return (T)value;
    }

    private static Guid? GetNullableGuidProperty(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName)
            ?? throw new ArgumentException($"Property '{propertyName}' not found on {source.GetType().Name}.", nameof(propertyName));
        var value = property.GetValue(source);
        if (value is null)
        {
            return null;
        }

        if (value is Guid guid)
        {
            return guid;
        }

        throw new InvalidOperationException($"Property '{propertyName}' is not a Guid.");
    }

    private sealed record TestOwnedEntity(Guid OwnerId);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<object> _securityEvents = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Warning || state is not IEnumerable<KeyValuePair<string, object?>> structuredState)
            {
                return;
            }

            var securityEvent = structuredState
                .FirstOrDefault(item => item.Key is not "{OriginalFormat}" && item.Value is not null)
                .Value;
            if (securityEvent is not null && securityEvent is not string)
            {
                _securityEvents.Add(securityEvent);
            }
        }

        public object SingleSecurityEvent() => Assert.Single(_securityEvents);

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
