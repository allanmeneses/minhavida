using Microsoft.AspNetCore.Http;

namespace Circulos.Api.Auth;

public static class AuthCookie
{
    public const string Name = "circulos.refresh";
    public const string ClientHeader = "X-Client";

    public static bool IsWeb(HttpRequest request) =>
        string.Equals(request.Headers[ClientHeader], "web", StringComparison.OrdinalIgnoreCase);

    public static void Set(HttpResponse response, string refreshToken, bool production, int refreshDays)
    {
        response.Cookies.Append(Name, refreshToken, Options(production, refreshDays));
    }

    public static void Clear(HttpResponse response, bool production)
    {
        var options = Options(production, 7);
        options.Expires = DateTimeOffset.UnixEpoch;
        options.MaxAge = TimeSpan.Zero;
        response.Cookies.Delete(Name, options);
    }

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static CookieOptions Options(bool production, int refreshDays) =>
        new()
        {
            HttpOnly = true,
            Secure = production,
            SameSite = production ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(refreshDays),
            IsEssential = true,
        };
}
