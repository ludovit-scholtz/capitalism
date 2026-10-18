using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Api.Security;

/// <summary>Reads the session token from a request (auth_token cookie or Authorization header) and resolves JWT instances.</summary>
internal static class RequestTokenReader
{
    internal static bool TryReadRequestJwt(HttpContext httpContext, out JwtSecurityToken token)
    {
        token = null!;
        if (!TryReadRequestToken(httpContext, out var rawToken))
        {
            return false;
        }

        try
        {
            token = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryReadRequestToken(HttpContext httpContext, out string token)
    {
        token = string.Empty;

        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var rawToken = authorization["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(rawToken))
            {
                token = rawToken;
                return true;
            }
        }

        if (httpContext.Request.Cookies.TryGetValue(AuthSessionCookieService.AccessTokenCookieName, out var cookieToken)
            && !string.IsNullOrWhiteSpace(cookieToken))
        {
            token = cookieToken.Trim();
            return true;
        }

        return false;
    }

    internal static bool TryResolveJwtSecurityToken(SecurityToken? token, out JwtSecurityToken jwt)
    {
        jwt = null!;
        if (token is JwtSecurityToken concreteJwt)
        {
            jwt = concreteJwt;
            return true;
        }

        if (token is JsonWebToken jsonWebToken)
        {
            jwt = new JwtSecurityToken(jsonWebToken.EncodedToken);
            return true;
        }

        return false;
    }
}
