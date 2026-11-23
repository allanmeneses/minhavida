using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Circulos.Api.Auth;

public sealed class GoogleIdTokenValidator(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleAuthOptions> options) : IGoogleIdTokenValidator
{
    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        var clientId = options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient("google-auth");
        using var response = await client.GetAsync(
            $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;
        var audience = root.TryGetProperty("aud", out var aud) ? aud.GetString() : null;
        var subject = root.TryGetProperty("sub", out var sub) ? sub.GetString() : null;
        var email = root.TryGetProperty("email", out var mail) ? mail.GetString() : null;
        var name = root.TryGetProperty("name", out var display) ? display.GetString() : null;
        if (!string.Equals(audience, clientId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(subject)
            || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new GoogleIdentity(subject, email.Trim().ToLowerInvariant(), string.IsNullOrWhiteSpace(name) ? email : name.Trim());
    }
}
