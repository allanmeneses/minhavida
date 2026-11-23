namespace Circulos.Api.Domain;

public enum CircleVisibility
{
    Private = 0,
    Public = 1,
}

public enum CircleKind
{
    Lasting = 0,
    Temporary = 1,
    Daily = 2,
}

public enum CircleStatus
{
    Active = 0,
    Archived = 1,
    LockedNoOwner = 2,
    Deleted = 3,
    Blocked = 4,
}

public enum CircleRole
{
    Viewer = 0,
    Commenter = 1,
    Editor = 2,
    Admin = 3,
    Owner = 4,
}
