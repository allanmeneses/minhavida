namespace Circulos.Api.Domain;

public enum RoutineTemplateKind
{
    List = 0,
    Reminder = 1,
}

public sealed class RoutineTemplate
{
    public Guid Id { get; set; }
    public RoutineTemplateKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public int UseCount { get; set; }
    public bool Curated { get; set; }
    public Guid? CreatorUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
