using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Circulos.Api.Tests;

internal static class AuthHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<AuthResult> RegisterAsync(
        HttpClient client,
        string? email = null,
        string password = "senha-segura-1",
        string name = "Maria Silva")
    {
        email ??= $"pessoa-{Guid.NewGuid():N}@exemplo.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            name,
        });

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Cadastro falhou ({(int)response.StatusCode}): {body}");
        }

        var parsed = JsonSerializer.Deserialize<AuthResponseDto>(body, JsonOptions)
            ?? throw new InvalidOperationException("Resposta de cadastro inválida.");

        return new AuthResult(parsed.Token!, parsed.RefreshToken, parsed.User!.Id, parsed.User.Email, parsed.User.Name);
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public sealed record AuthUserDto(Guid Id, string Email, string Name);

    public sealed record AuthResponseDto(
        string? Token,
        string? RefreshToken,
        AuthUserDto? User,
        bool RequiresTwoFactor,
        string? TwoFactorToken);

    public sealed record AuthResult(string Token, string? RefreshToken, Guid UserId, string Email, string Name);
}
