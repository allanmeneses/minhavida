namespace Circulos.Api.Domain;

public enum LivingProfileAccess
{
    View = 0,
    Edit = 1,
}

public sealed class LivingProfileGrant
{
    public Guid Id { get; set; }
    public Guid LivingProfileId { get; set; }
    public Guid UserId { get; set; }
    public LivingProfileAccess Access { get; set; } = LivingProfileAccess.View;

    public LivingProfile LivingProfile { get; set; } = null!;
}
