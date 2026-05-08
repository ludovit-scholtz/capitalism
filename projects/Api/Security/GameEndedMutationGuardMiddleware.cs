using System.Text.Json;
using Api.Data;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Security;

/// <summary>
/// Blocks GraphQL mutations once the game is marked as ended.
/// Keeps the API read-only and returns a user-friendly winner message.
/// </summary>
public sealed class GameEndedMutationGuardMiddleware(RequestDelegate next)
{
    private readonly ILogger<GameEndedMutationGuardMiddleware> _logger = NullLogger<GameEndedMutationGuardMiddleware>.Instance;

    public GameEndedMutationGuardMiddleware(RequestDelegate next, ILogger<GameEndedMutationGuardMiddleware> logger) : this(next)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (!IsGraphQlPost(context))
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();
        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync(context.RequestAborted);
        }
        context.Request.Body.Position = 0;

        if (!ContainsMutationOperation(requestBody))
        {
            await next(context);
            return;
        }

        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync(context.RequestAborted);
        if (gameState?.GameEnded != true)
        {
            await next(context);
            return;
        }

        var winnerName = string.IsNullOrWhiteSpace(gameState.WinnerDisplayName)
            ? "A player"
            : gameState.WinnerDisplayName;
        var message = $"The game has ended. {winnerName} has won!";

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new
            {
                data = (object?)null,
                errors =
                    new[]
                    {
                        new
                        {
                            message,
                            extensions = new { code = "GAME_ENDED" }
                        }
                    }
            },
            cancellationToken: context.RequestAborted);
    }

    private static bool IsGraphQlPost(HttpContext context)
        => HttpMethods.IsPost(context.Request.Method)
            && context.Request.Path.StartsWithSegments("/graphql", StringComparison.OrdinalIgnoreCase);

    private bool ContainsMutationOperation(string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(requestBody);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    if (JsonContainsMutation(entry))
                    {
                        return true;
                    }
                }

                return false;
            }

            return JsonContainsMutation(document.RootElement);
        }
        catch
        {
            _logger.LogDebug("Unable to parse GraphQL request payload while checking mutation gate.");
            // If parsing fails, do not block at middleware level.
            return false;
        }
    }

    private static bool JsonContainsMutation(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!root.TryGetProperty("query", out var queryElement) || queryElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var query = queryElement.GetString();
        return !string.IsNullOrWhiteSpace(query)
            && query.Contains("mutation", StringComparison.OrdinalIgnoreCase);
    }
}
