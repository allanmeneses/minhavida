namespace Circulos.Api.Domain;

public static class CircleAccess
{
    public const string LockedMessage = "Este círculo está travado.";

    public static bool HiddenSurprise(Circle circle, Guid userId) =>
        circle.IsSurprise && circle.RevealedAt is null && circle.SurpriseSubjectUserId == userId;

    public static bool CanSeeAfterReveal(Circle circle, Guid userId) =>
        circle.IsSurprise
        && circle.RevealedAt is not null
        && circle.SurpriseSubjectUserId == userId;

    public static bool IsWritable(Circle circle) => circle.Status == CircleStatus.Active;
}
