namespace Circulos.Api.Domain;

public sealed class DiaryPage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LifeAreaId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
