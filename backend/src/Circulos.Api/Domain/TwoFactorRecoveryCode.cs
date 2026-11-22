namespace Circulos.Api.Domain;

public sealed class TwoFactorRecoveryCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
}
