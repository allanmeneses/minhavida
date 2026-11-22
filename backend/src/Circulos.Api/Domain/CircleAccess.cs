namespace Circulos.Api.Domain;

public static class CircleAccess
{
    public static bool HiddenSurprise(Circle circle, Guid userId) =>
        circle.IsSurprise && circle.RevealedAt is null && circle.SurpriseSubjectUserId == userId;

    public static bool CanSeeAfterReveal(Circle circle, Guid userId) =>
        circle.IsSurprise
        && circle.RevealedAt is not null
        && circle.SurpriseSubjectUserId == userId;
}
