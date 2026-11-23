namespace Circulos.Api.Auth;

public sealed record GoogleIdentity(string Subject, string Email, string Name);

public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
