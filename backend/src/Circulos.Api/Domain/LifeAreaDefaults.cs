namespace Circulos.Api.Domain;

public static class LifeAreaDefaults
{
    public static IReadOnlyList<LifeArea> ForUser(Guid userId, DateTime now) =>
    [
        New(userId, "Vacinas", LifeAreaKind.Vaccine, 0, now),
        New(userId, "Lembretes", LifeAreaKind.Reminder, 1, now),
        New(userId, "Documentos", LifeAreaKind.Document, 2, now),
        New(userId, "Diário", LifeAreaKind.Diary, 3, now),
    ];

    private static LifeArea New(Guid userId, string name, LifeAreaKind kind, int order, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Kind = kind,
            SortOrder = order,
            Suggested = true,
            CreatedAt = now,
        };
}
