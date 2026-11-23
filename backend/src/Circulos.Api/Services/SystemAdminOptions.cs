namespace Circulos.Api.Services;

public sealed class SystemAdminOptions
{
    public string? Email { get; set; }

    public bool Matches(string? email)
    {
        return !string.IsNullOrWhiteSpace(Email)
            && !string.IsNullOrWhiteSpace(email)
            && string.Equals(Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
