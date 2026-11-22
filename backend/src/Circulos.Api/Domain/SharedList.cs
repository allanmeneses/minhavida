namespace Circulos.Api.Domain;

public enum SharedListKind
{
    Shopping = 0,
    Wishes = 1,
    Materials = 2,
    Other = 3,
}

public enum SharedListAccess
{
    View = 0,
    Add = 1,
    Complete = 2,
    Manage = 3,
}

public sealed class SharedList
{
    public Guid Id { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? CircleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SharedListKind Kind { get; set; } = SharedListKind.Other;
    public SharedListAccess MemberAccess { get; set; } = SharedListAccess.View;
    public DateTime CreatedAt { get; set; }

    public Circle? Circle { get; set; }
    public ICollection<SharedListItem> Items { get; set; } = new List<SharedListItem>();
    public ICollection<SharedListGrant> Grants { get; set; } = new List<SharedListGrant>();
}

public sealed class SharedListItem
{
    public Guid Id { get; set; }
    public Guid ListId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Quantity { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
    public bool IsDone { get; set; }
    public Guid? AddedByUserId { get; set; }
    public Guid? DoneByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DoneAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public SharedList List { get; set; } = null!;
}

public sealed class SharedListGrant
{
    public Guid Id { get; set; }
    public Guid ListId { get; set; }
    public Guid UserId { get; set; }
    public SharedListAccess Access { get; set; }

    public SharedList List { get; set; } = null!;
}
