using Api.Security;
using HotChocolate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Api.Tests;

public sealed class ObjectAuthorizationDenialParityTests
{
    private sealed class ParityLogger : ILogger<ObjectAuthorizationService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception) + " " + state));
    }

    private static (ObjectAuthorizationService Service, ParityLogger Logger) Create()
    {
        var logger = new ParityLogger();
        return (new ObjectAuthorizationService(logger, new HttpContextAccessor()), logger);
    }

    [Theory]
    [InlineData(ObjectAuthorizationService.NotFoundReason)]
    [InlineData(ObjectAuthorizationService.NotOwnedReason)]
    public void Deny_ReturnsIdenticalClientErrorAndLogsRealReason(string reason)
    {
        var (service, logger) = Create();
        var objectId = Guid.NewGuid();

        var ex = service.Deny(Guid.NewGuid(), "building", objectId, reason);

        var error = Assert.Single(ex.Errors);
        Assert.Equal(ObjectAuthorizationService.NotFoundOrNotOwnedCode, error.Code);
        Assert.Equal(ObjectAuthorizationService.FriendlyMessage, error.Message);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(reason, entry.Message);
        Assert.Contains(objectId.ToString(), entry.Message);
    }

    [Fact]
    public void Deny_BenignStateReason_LogsInformationInsteadOfSecurityWarning()
    {
        var (service, logger) = Create();

        var ex = service.Deny(Guid.NewGuid(), "building", Guid.NewGuid(), ObjectAuthorizationService.NotAvailableReason);

        Assert.Equal(ObjectAuthorizationService.NotFoundOrNotOwnedCode, Assert.Single(ex.Errors).Code);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(ObjectAuthorizationService.NotAvailableReason, entry.Message);
    }

    [Fact]
    public void Deny_UnknownReason_FailsSafeToWarning()
    {
        var (service, logger) = Create();

        service.Deny(Guid.NewGuid(), "building", Guid.NewGuid(), "some_future_reason");

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task RequireOwnedAsync_MissingAndForeignObjectsYieldSameClientError()
    {
        var (service, logger) = Create();
        var actor = Guid.NewGuid();

        var missing = await Assert.ThrowsAsync<GraphQLException>(() => service.RequireOwnedAsync<object>(
            actor, "company", Guid.NewGuid(), _ => Task.FromResult<object?>(null), _ => true));
        var foreign = await Assert.ThrowsAsync<GraphQLException>(() => service.RequireOwnedAsync<object>(
            actor, "company", Guid.NewGuid(), _ => Task.FromResult<object?>(new object()), _ => false));

        Assert.Equal(missing.Errors[0].Code, foreign.Errors[0].Code);
        Assert.Equal(missing.Errors[0].Message, foreign.Errors[0].Message);
        Assert.Equal(2, logger.Entries.Count);
        Assert.Contains(ObjectAuthorizationService.NotFoundReason, logger.Entries[0].Message);
        Assert.Contains(ObjectAuthorizationService.NotOwnedReason, logger.Entries[1].Message);
    }
}
