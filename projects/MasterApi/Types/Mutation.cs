using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed class Mutation
{
    public async Task<GameServerSummary> RegisterGameServer(
        RegisterGameServerInput input,
        [Service] MasterDbContext db,
        [Service] IOptions<MasterServerOptions> options)
    {
        var expectedKey = options.Value.RegistrationKey.Trim();
        if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(expectedKey, input.RegistrationKey?.Trim(), StringComparison.Ordinal))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid game server registration key.")
                    .SetCode("INVALID_REGISTRATION_KEY")
                    .Build());
        }

        var serverKey = input.ServerKey.Trim();
        if (string.IsNullOrWhiteSpace(serverKey))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Server key is required.")
                    .SetCode("SERVER_KEY_REQUIRED")
                    .Build());
        }

        var backendUrl = NormalizeRequiredUrl(input.BackendUrl, "BACKEND_URL_REQUIRED");
        var graphqlUrl = NormalizeRequiredUrl(input.GraphqlUrl, "GRAPHQL_URL_REQUIRED");
        var frontendUrl = NormalizeRequiredUrl(input.FrontendUrl, "FRONTEND_URL_REQUIRED");

        var now = DateTime.UtcNow;
        var server = await db.GameServers.FirstOrDefaultAsync(candidate => candidate.ServerKey == serverKey);

        if (server is null)
        {
            server = new GameServerNode
            {
                Id = Guid.NewGuid(),
                ServerKey = serverKey,
                RegisteredAtUtc = now,
            };

            db.GameServers.Add(server);
        }

        server.DisplayName = input.DisplayName.Trim();
        server.Description = input.Description?.Trim() ?? string.Empty;
        server.Region = input.Region.Trim();
        server.Environment = input.Environment.Trim();
        server.BackendUrl = backendUrl;
        server.GraphqlUrl = graphqlUrl;
        server.FrontendUrl = frontendUrl;
        server.Version = input.Version.Trim();
        server.PlayerCount = Math.Max(0, input.PlayerCount);
        server.CompanyCount = Math.Max(0, input.CompanyCount);
        server.CurrentTick = Math.Max(0, input.CurrentTick);
        server.LastHeartbeatAtUtc = now;
        server.UpdatedAtUtc = now;

        await db.SaveChangesAsync();

        return Query.ToSummary(server, now.AddSeconds(-Math.Max(5, options.Value.ActiveThresholdSeconds)));
    }

    private static string NormalizeRequiredUrl(string url, string errorCode)
    {
        var trimmedUrl = url.Trim();
        if (string.IsNullOrWhiteSpace(trimmedUrl)
            || !Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A valid absolute URL is required.")
                    .SetCode(errorCode)
                    .Build());
        }

        return trimmedUrl.TrimEnd('/');
    }
}