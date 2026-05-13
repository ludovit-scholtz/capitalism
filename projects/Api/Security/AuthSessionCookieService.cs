using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Api.Security;

public static class AuthSessionCookieService
{
    public const string AccessTokenCookieName = "auth_token";
    public const string RefreshTokenCookieName = "refresh_token";
    public const string LegacyExpiresCookieName = "auth_expires";

    public static void SetSessionCookies(HttpContext? httpContext, IHostEnvironment hostEnvironment, string token, DateTime expiresAtUtc)
    {
        if (httpContext is null || string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc));
        httpContext.Response.Cookies.Append(
            AccessTokenCookieName,
            token,
            BuildCookieOptions(hostEnvironment, expires));
    }

    public static void ClearSessionCookies(HttpContext? httpContext, IHostEnvironment hostEnvironment)
    {
        if (httpContext is null)
        {
            return;
        }

        var clearOptions = BuildCookieOptions(hostEnvironment, DateTimeOffset.UnixEpoch);
        clearOptions.MaxAge = TimeSpan.Zero;
        httpContext.Response.Cookies.Append(AccessTokenCookieName, string.Empty, clearOptions);
        httpContext.Response.Cookies.Append(RefreshTokenCookieName, string.Empty, clearOptions);
        httpContext.Response.Cookies.Append(LegacyExpiresCookieName, string.Empty, clearOptions);
    }

    private static CookieOptions BuildCookieOptions(IHostEnvironment hostEnvironment, DateTimeOffset expires)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !hostEnvironment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = "/",
            Expires = expires,
        };
    }
}
