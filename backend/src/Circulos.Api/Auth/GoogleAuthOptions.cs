namespace Circulos.Api.Auth;

public sealed class GoogleAuthOptions
{
    public const string Section = "Google";

    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
}
